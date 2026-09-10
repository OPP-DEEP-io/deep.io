using System.Collections.Concurrent;
using System.Numerics;
using DeepIo.Server.Combat;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.World;

/// <summary>
/// The authoritative simulation and single entity registry.
///
/// Threading model: SignalR hub touches this from background threads only
/// through the concurrent queues (join / leave / input). ALL mutation of the entity
/// dictionary happens on the game-loop thread inside <see cref="Update"/>, so no locks are
/// needed on the hot path. This is registered as a DI singleton today; Student A formalises
/// it as the thread-safe Singleton pattern in Part 1.
/// </summary>
public sealed class GameWorld
{
    private readonly Dictionary<int, Entity> _entities = new();
    private readonly Dictionary<string, int> _connectionToTank = new();

    // Cross-thread inboxes, drained at the top of each tick.
    private readonly ConcurrentQueue<Tank> _pendingJoins = new();
    private readonly ConcurrentQueue<string> _pendingLeaves = new();
    private readonly ConcurrentDictionary<string, InputMessage> _inputs = new();

    private int _nextId;
    private long _tick;
    private readonly Random _rng = new();

    public long CurrentTick => _tick;

    public int NextId() => Interlocked.Increment(ref _nextId);

    // ---- Called from the SignalR hub (background threads) -------------------------------

    /// <summary>
    /// Creates a tank immediately (so the caller gets its id synchronously) and queues it to
    /// be inserted into the registry on the next tick.
    /// </summary>
    public Tank CreateTankForConnection(string connectionId, string name)
    {
        var tank = new Tank
        {
            Id = NextId(),
            Name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim(),
            ConnectionId = connectionId,
            Position = RandomPoint(),
            Radius = GameConstants.TankRadius,
            Hp = GameConstants.TankMaxHp,
            MaxHp = GameConstants.TankMaxHp,
        };
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
        FireWeapons(dt);
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
            switch (e)
            {
                case Tank t:
                    entities.Add(new EntityDto
                    {
                        Id = t.Id, Kind = "tank", X = t.Position.X, Y = t.Position.Y,
                        Rot = t.Aim, Hp = t.Hp, MaxHp = t.MaxHp, Team = t.Team, Name = t.Name,
                    });
                    board.Add(new LeaderboardEntry { Name = t.Name, Score = t.Score });
                    break;

                case Shape s:
                    entities.Add(new EntityDto
                    {
                        Id = s.Id, Kind = "shape", X = s.Position.X, Y = s.Position.Y,
                        Rot = s.Rotation, Hp = s.Hp, MaxHp = s.MaxHp, Shape = (int)s.Kind,
                    });
                    break;

                case Bullet b:
                    entities.Add(new EntityDto
                    {
                        Id = b.Id, Kind = "bullet", X = b.Position.X, Y = b.Position.Y,
                        Owner = b.OwnerId, Hp = 1f, MaxHp = 1f,
                    });
                    break;
            }
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

            var dir = new Vector2(input.MoveX, input.MoveY);
            if (dir.LengthSquared() > 1e-4f) dir = Vector2.Normalize(dir);

            tank.MoveDir = dir;
            tank.Aim = input.Aim;
            tank.Firing = input.Fire;
            tank.LastInputSeq = input.Seq;
        }
    }

    private void Integrate(float dt)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        foreach (var e in _entities.Values)
        {
            switch (e)
            {
                case Tank tank:
                    tank.Velocity = tank.MoveDir * GameConstants.TankSpeed;
                    tank.Position += tank.Velocity * dt;
                    tank.Position = ClampToArena(tank.Position, tank.Radius, half);
                    if (tank.Hp < tank.MaxHp)
                        tank.Hp = MathF.Min(tank.MaxHp, tank.Hp + GameConstants.TankRegenPerSec * dt);
                    break;

                case Bullet bullet:
                    bullet.Position += bullet.Velocity * dt;
                    bullet.Life -= dt;
                    break;

                case Shape shape:
                    shape.Rotation += shape.SpinSpeed * dt;
                    break;
            }
        }
    }

    private void FireWeapons(float dt)
    {
        // Collect first: we cannot add to _entities while iterating its Values view.
        List<Bullet>? spawned = null;

        foreach (var e in _entities.Values)
        {
            if (e is not Tank tank) continue;

            tank.ReloadTimer -= dt;
            if (!tank.Firing || tank.ReloadTimer > 0f) continue;
            tank.ReloadTimer = GameConstants.ReloadInterval;

            var dir = new Vector2(MathF.Cos(tank.Aim), MathF.Sin(tank.Aim));
            var muzzle = tank.Position + dir * (tank.Radius + GameConstants.BulletRadius + 2f);

            (spawned ??= new List<Bullet>()).Add(new Bullet
            {
                Id = NextId(),
                OwnerId = tank.Id,
                Position = muzzle,
                Velocity = dir * GameConstants.BulletSpeed,
                Radius = GameConstants.BulletRadius,
                Hp = 1f,
                MaxHp = 1f,
                Damage = GameConstants.BulletDamage,
                Life = GameConstants.BulletLife,
            });
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

                shape.Hp -= bullet.Damage;
                bullet.Hp = 0f;
                if (shape.Dead) AwardScore(bullet.OwnerId, shape.XpValue);
                break;
            }
            if (bullet.Dead) continue;

            foreach (var tank in tanks)
            {
                if (tank.Id == bullet.OwnerId || tank.Dead) continue;
                if (!Collision.CirclesOverlap(bullet.Position, bullet.Radius, tank.Position, tank.Radius))
                    continue;

                tank.Hp -= bullet.Damage;
                bullet.Hp = 0f;
                if (tank.Dead) AwardScore(bullet.OwnerId, 100);
                break;
            }
        }
    }

    private void AwardScore(int tankId, int amount)
    {
        if (_entities.TryGetValue(tankId, out var owner) && owner is Tank tank)
            tank.Score += amount;
    }

    private void Cleanup()
    {
        List<int>? remove = null;

        foreach (var e in _entities.Values)
        {
            switch (e)
            {
                case Bullet b when b.Dead || b.Life <= 0f:
                    (remove ??= new List<int>()).Add(b.Id);
                    break;

                case Shape s when s.Dead:
                    (remove ??= new List<int>()).Add(s.Id);
                    break;

                case Tank t when t.Dead:
                    // Prototype respawn: full heal, halve score, teleport to a fresh spot.
                    t.Hp = t.MaxHp;
                    t.Score = Math.Max(0, t.Score / 2);
                    t.Position = RandomPoint();
                    t.MoveDir = Vector2.Zero;
                    t.Firing = false;
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
            SpawnShape();
    }

    private void SpawnShape()
    {
        int roll = _rng.Next(100);
        ShapeKind kind = roll < 60 ? ShapeKind.Square
                       : roll < 90 ? ShapeKind.Triangle
                       : ShapeKind.Pentagon;

        (float radius, float hp, int xp) = kind switch
        {
            ShapeKind.Square => (18f, 30f, 10),
            ShapeKind.Triangle => (24f, 45f, 25),
            _ => (34f, 100f, 130),
        };

        var shape = new Shape
        {
            Id = NextId(),
            Kind = kind,
            Position = RandomPoint(),
            Radius = radius,
            Hp = hp,
            MaxHp = hp,
            XpValue = xp,
            Rotation = (float)(_rng.NextDouble() * MathF.Tau),
            SpinSpeed = (float)(_rng.NextDouble() * 0.6 - 0.3),
        };
        _entities[shape.Id] = shape;
    }

    private Vector2 RandomPoint()
    {
        float half = GameConstants.ArenaSize * 0.5f - 40f;
        return new Vector2(
            (float)(_rng.NextDouble() * 2 - 1) * half,
            (float)(_rng.NextDouble() * 2 - 1) * half);
    }

    private static Vector2 ClampToArena(Vector2 p, float r, float half)
    {
        p.X = Math.Clamp(p.X, -half + r, half - r);
        p.Y = Math.Clamp(p.Y, -half + r, half - r);
        return p;
    }
}
