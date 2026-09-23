using System.Collections.Concurrent;
using System.Numerics;
using DeepIo.Server.Combat;
using DeepIo.Server.Entities;
using DeepIo.Server.Factories;
using DeepIo.Shared;

namespace DeepIo.Server.World;

/// <summary>
/// The authoritative simulation and single entity registry.
///
/// SINGLETON (thread safe). There must be exactly one arena per process: the SignalR hub,
/// the game loop and any future admin endpoint all have to mutate the same registry.
/// Instantiation is guarded by <see cref="Lazy{T}"/> with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so even if several request
/// threads race on first access, the constructor runs exactly once and every thread gets the
/// same fully-published instance. The constructor is private, so no second world can be
/// created — the DI container is handed <see cref="Instance"/> rather than being allowed to
/// construct its own.
///
/// Threading model of the state itself: SignalR hub threads only ever touch the concurrent
/// inboxes (join / leave / input). ALL mutation of the entity dictionary happens on the
/// game-loop thread inside <see cref="Update"/>, so no locks are needed on the hot path.
/// </summary>
public sealed class GameWorld
{
    private static readonly Lazy<GameWorld> LazyInstance =
        new(static () => new GameWorld(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The one and only arena. Safe to touch from any thread.</summary>
    public static GameWorld Instance => LazyInstance.Value;

    private readonly Dictionary<int, Entity> _entities = new();
    private readonly Dictionary<string, int> _connectionToTank = new();

    // Cross-thread inboxes, drained at the top of each tick.
    private readonly ConcurrentQueue<Tank> _pendingJoins = new();
    private readonly ConcurrentQueue<string> _pendingLeaves = new();
    private readonly ConcurrentDictionary<string, InputMessage> _inputs = new();

    // Object creation is delegated to the factories; the world never news up a Shape or Tank.
    private readonly ShapeSpawner _shapeSpawner = new();
    private readonly TankAssembler _tankAssembler = new();

    private int _nextId;
    private long _tick;
    private readonly Random _rng = new();

    /// <summary>Private: the only way to a world is <see cref="Instance"/>.</summary>
    private GameWorld()
    {
    }

    public long CurrentTick => _tick;

    public int EntityCount => _entities.Count;

    public TankAssembler Tanks => _tankAssembler;

    public int NextId() => Interlocked.Increment(ref _nextId);

    // ---- Called from the SignalR hub (background threads) -------------------------------

    /// <summary>
    /// Assembles a tank immediately (so the caller gets its id synchronously) and queues it to
    /// be inserted into the registry on the next tick.
    /// </summary>
    public Tank CreateTankForConnection(string connectionId, string name, TankArchetype archetype)
    {
        Tank tank = _tankAssembler.Assemble(NextId(), connectionId, name, archetype, RandomPoint());
        _pendingJoins.Enqueue(tank);
        return tank;
    }

    public void EnqueueLeave(string connectionId) => _pendingLeaves.Enqueue(connectionId);

    public void SetInput(string connectionId, InputMessage input) => _inputs[connectionId] = input;

    // ---- Called from the game loop thread only -----------------------------------------

    public void Update(float dt)
    {
        if (dt > 0.1f) dt = 0.1f;   // guard against a stall producing a huge step
        _tick++;

        DrainJoins();
        DrainLeaves();
        ApplyInputs();
        Integrate(dt);
        FireWeapons();
        ResolveCollisions();
        Cleanup();
        MaintainShapes();
    }

    public Snapshot BuildSnapshot()
    {
        var entities = new List<EntityDto>(_entities.Count);
        var board = new List<LeaderboardEntry>();

        foreach (var e in _entities.Values)
        {
            // Polymorphic: each entity knows its own wire shape.
            entities.Add(e.ToDto());
            if (e is Tank t) board.Add(new LeaderboardEntry { Name = t.Name, Score = t.Score });
        }

        board.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (board.Count > 10) board.RemoveRange(10, board.Count - 10);

        return new Snapshot { Tick = _tick, Entities = entities, Leaderboard = board };
    }

    // ---- Tick stages -------------------------------------------------------------------

    private void DrainJoins()
    {
        while (_pendingJoins.TryDequeue(out var tank))
        {
            _entities[tank.Id] = tank;
            _connectionToTank[tank.ConnectionId] = tank.Id;
        }
    }

    private void DrainLeaves()
    {
        while (_pendingLeaves.TryDequeue(out var conn))
        {
            _inputs.TryRemove(conn, out _);
            if (_connectionToTank.Remove(conn, out var id))
                _entities.Remove(id);
        }
    }

    private void ApplyInputs()
    {
        foreach (var (conn, id) in _connectionToTank)
        {
            if (!_entities.TryGetValue(id, out var e) || e is not Tank tank) continue;
            if (!_inputs.TryGetValue(conn, out var input)) continue;

            tank.ApplyInput(input);
        }
    }

    private void Integrate(float dt)
    {
        foreach (var e in _entities.Values)
            e.Update(dt);
    }

    private void FireWeapons()
    {
        // Collect first: we cannot add to _entities while iterating its Values view.
        List<Bullet>? spawned = null;

        foreach (var e in _entities.Values)
        {
            if (e is not Tank tank || !tank.CanFire) continue;
            tank.BeginReload();

            // Spread comes from the barrel this build's factory produced.
            float spread = tank.Weapon.Spread;
            float angle = spread <= 0f
                ? tank.Aim
                : tank.Aim + (float)((_rng.NextDouble() * 2 - 1) * spread);

            (spawned ??= new List<Bullet>()).Add(Bullet.FromSpec(NextId(), tank, angle));
        }

        if (spawned is null) return;
        foreach (var b in spawned) _entities[b.Id] = b;
    }

    private void ResolveCollisions()
    {
        // Snapshot the typed lists once (brute force, fine for prototype counts).
        var bullets = new List<Bullet>();
        var shapes = new List<Shape>();
        var tanks = new List<Tank>();
        foreach (var e in _entities.Values)
        {
            if (e is Bullet b) bullets.Add(b);
            else if (e is Shape s) shapes.Add(s);
            else if (e is Tank t) tanks.Add(t);
        }

        foreach (var bullet in bullets)
        {
            if (bullet.Dead) continue;

            foreach (var shape in shapes)
            {
                if (shape.Dead) continue;
                if (!Collision.CirclesOverlap(bullet.Position, bullet.Radius, shape.Position, shape.Radius))
                    continue;

                if (shape.ApplyDamage(bullet.Damage)) AwardScore(bullet.OwnerId, shape.XpValue);
                bullet.Kill();
                break;
            }
            if (bullet.Dead) continue;

            foreach (var tank in tanks)
            {
                if (tank.Id == bullet.OwnerId || tank.Dead) continue;
                if (!Collision.CirclesOverlap(bullet.Position, bullet.Radius, tank.Position, tank.Radius))
                    continue;

                if (tank.ApplyDamage(bullet.Damage)) AwardScore(bullet.OwnerId, 100);
                bullet.Kill();
                break;
            }
        }
    }

    private void AwardScore(int tankId, int amount)
    {
        if (_entities.TryGetValue(tankId, out var owner) && owner is Tank tank)
            tank.AddScore(amount);
    }

    private void Cleanup()
    {
        List<int>? remove = null;

        foreach (var e in _entities.Values)
        {
            switch (e)
            {
                case Bullet b when b.Dead || b.Expired:
                    (remove ??= new List<int>()).Add(b.Id);
                    break;

                case Shape s when s.Dead:
                    (remove ??= new List<int>()).Add(s.Id);
                    break;

                case Tank t when t.Dead:
                    t.Respawn(RandomPoint());
                    break;
            }
        }

        if (remove is null) return;
        foreach (var id in remove) _entities.Remove(id);
    }

    private void MaintainShapes()
    {
        int count = 0;
        foreach (var e in _entities.Values)
            if (e is Shape) count++;

        for (; count < GameConstants.TargetShapeCount; count++)
        {
            // Factory Method: which polygon subclass gets allocated is the creators' business.
            Shape shape = _shapeSpawner.Spawn(NextId(), RandomPoint(), _rng);
            _entities[shape.Id] = shape;
        }
    }

    private Vector2 RandomPoint()
    {
        float half = GameConstants.ArenaSize * 0.5f - 40f;
        return new Vector2(
            (float)(_rng.NextDouble() * 2 - 1) * half,
            (float)(_rng.NextDouble() * 2 - 1) * half);
    }
}
