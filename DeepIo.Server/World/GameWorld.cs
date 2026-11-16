using System.Collections.Concurrent;
using System.Numerics;
using DeepIo.Server.Ai;
using DeepIo.Server.Combat;
using DeepIo.Server.Commands;
using DeepIo.Server.Entities;
using DeepIo.Server.Events;
using DeepIo.Server.Events.Observers;
using DeepIo.Server.Factories;
using DeepIo.Server.Prototypes;
using DeepIo.Server.Scripting;
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
public sealed class GameWorld : IConsoleWorld
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
    private readonly ConcurrentQueue<UpgradeRequest> _pendingUpgrades = new();
    private readonly ConcurrentQueue<ConsoleLine> _pendingConsole = new();

    // Interpreter: console lines are parsed and interpreted on the game-loop thread; the
    // replies wait here until the GameLoop sends them back to whoever typed the line.
    private readonly ConsoleInterpreter _console = new();
    private readonly ConcurrentQueue<ConsoleLine> _consoleReplies = new();

    // Object creation is delegated to the factories; the world never news up a Shape or Tank.
    private readonly ShapeSpawner _shapeSpawner = new();
    private readonly TankAssembler _tankAssembler = new();
    private readonly TankPrototypeRegistry _botPrototypes;

    // Observer: entities publish hits and kills on this bus. Everything that reacts to them
    // (XP, leaderboard, achievements, kill feed) is an attached observer, not code in the tick.
    private readonly GameEventBus _eventBus = new();
    private readonly LeaderboardObserver _leaderboard = new();
    private readonly NetworkBroadcastObserver _feed = new();

    private readonly CollisionResolver _collisions = new();

    // Strategy: one controller per bot, each swapping movement strategies at runtime.
    private static readonly string[] BotNames = ["Bot Alpha", "Bot Bravo", "Bot Charlie", "Bot Delta", "Bot Echo", "Bot Foxtrot"];
    private readonly List<BotBrain> _bots = new();
    private int _targetBotCount = GameConstants.BotCount;
    private readonly List<Tank> _tankView = new();
    private readonly List<Shape> _shapeView = new();

    private int _nextId;
    private long _tick;
    private readonly Random _rng = new();

    /// <summary>Private: the only way to a world is <see cref="Instance"/>.</summary>
    private GameWorld()
    {
        _botPrototypes = new TankPrototypeRegistry(_tankAssembler);
        // The feed goes first: observers below publish follow-up events (level-ups,
        // achievements) mid-notification, and the feed should list the cause before them.
        _eventBus.Attach(_feed);
        _eventBus.Attach(new XpAwardObserver());
        _eventBus.Attach(_leaderboard);
        _eventBus.Attach(new AchievementObserver(_eventBus));
    }

    public long CurrentTick => _tick;

    public int EntityCount => _entities.Count;

    public TankAssembler Tanks => _tankAssembler;

    /// <summary>The arena's subject; attach extra observers here.</summary>
    public IGameSubject EventBus => _eventBus;

    public int NextId() => Interlocked.Increment(ref _nextId);

    /// <summary>Connection id used for lines typed on the server's own stdin console.</summary>
    public const string ServerConsoleId = "server-console";

    /// <summary>When true (--open-console), players may run console commands on any tank, not just their own.</summary>
    public bool OpenConsole { get; set; }

    // ---- Called from the SignalR hub (background threads) -------------------------------

    /// <summary>
    /// Assembles a tank immediately (so the caller gets its id synchronously) and queues it to
    /// be inserted into the registry on the next tick.
    /// </summary>
    public Tank CreateTankForConnection(string connectionId, string name, TankArchetype archetype)
    {
        Tank tank = _tankAssembler.Assemble(NextId(), connectionId, name, archetype, SpawnPoints.Random(Random.Shared));
        _pendingJoins.Enqueue(tank);
        return tank;
    }

    public void EnqueueLeave(string connectionId) => _pendingLeaves.Enqueue(connectionId);

    public void SetInput(string connectionId, InputMessage input) => _inputs[connectionId] = input;

    /// <summary>Queues a skill-point spend; it becomes an <see cref="UpgradeStatCommand"/> on the next tick.</summary>
    public void EnqueueUpgrade(string connectionId, StatKind stat) =>
        _pendingUpgrades.Enqueue(new UpgradeRequest(connectionId, stat));

    /// <summary>Queues an undo of this player's most recent upgrade (respec).</summary>
    public void EnqueueUndoUpgrade(string connectionId) =>
        _pendingUpgrades.Enqueue(new UpgradeRequest(connectionId, null));

    /// <summary>Queues a console line; it is interpreted on the next tick.</summary>
    public void EnqueueConsole(string connectionId, string line) =>
        _pendingConsole.Enqueue(new ConsoleLine(connectionId, line));

    /// <summary>Takes the next console reply for the game loop to deliver.</summary>
    public bool TryTakeConsoleReply(out ConsoleLine reply) => _consoleReplies.TryDequeue(out reply);

    // ---- Called from the game loop thread only -----------------------------------------

    public void Update(float dt)
    {
        if (dt > 0.1f) dt = 0.1f;   // guard against a stall producing a huge step
        _tick++;

        DrainJoins();
        DrainLeaves();
        ApplyInputs();
        DrainUpgrades();
        DrainConsole();
        UpdateBots(dt);
        Integrate(dt);
        FireWeapons();
        ResolveCollisions(dt);
        Cleanup();
        MaintainShapes();
        MaintainBots();
    }

    public Snapshot BuildSnapshot()
    {
        var entities = new List<EntityDto>(_entities.Count);

        // Polymorphic: each entity knows its own wire shape.
        foreach (var e in _entities.Values)
            entities.Add(e.ToDto());

        return new Snapshot
        {
            Tick = _tick,
            Entities = entities,
            Leaderboard = _leaderboard.Current(_entities.Values.OfType<Tank>()),
            Feed = _feed.RecentLines(),
        };
    }

    // ---- Tick stages -------------------------------------------------------------------

    private void Add(Entity entity)
    {
        entity.EventBus = _eventBus;
        _entities[entity.Id] = entity;
    }

    private void DrainJoins()
    {
        while (_pendingJoins.TryDequeue(out var tank))
        {
            Add(tank);
            _connectionToTank[tank.ConnectionId] = tank.Id;
            _eventBus.Notify(new PlayerJoinedEvent(tank));
        }
    }

    private void DrainLeaves()
    {
        while (_pendingLeaves.TryDequeue(out var conn))
        {
            _inputs.TryRemove(conn, out _);
            if (_connectionToTank.Remove(conn, out var id) && _entities.Remove(id, out var e) && e is Tank tank)
                _eventBus.Notify(new PlayerLeftEvent(tank));
        }
    }

    private void ApplyInputs()
    {
        foreach (var (conn, id) in _connectionToTank)
        {
            if (!_entities.TryGetValue(id, out var e) || e is not Tank tank) continue;
            if (!_inputs.TryGetValue(conn, out var input) || input.Seq == tank.LastInputSeq) continue;

            // Command: each new input message becomes the same three commands a bot issues.
            tank.LastInputSeq = input.Seq;
            tank.InputHistory.Execute(new MoveCommand(tank, new Vector2(input.MoveX, input.MoveY)));
            tank.InputHistory.Execute(new RotateCommand(tank, input.Aim));
            tank.InputHistory.Execute(new FireCommand(tank, input.Fire));
        }
    }

    private void DrainUpgrades()
    {
        while (_pendingUpgrades.TryDequeue(out var request))
        {
            if (!_connectionToTank.TryGetValue(request.ConnectionId, out var id)) continue;
            if (!_entities.TryGetValue(id, out var e) || e is not Tank tank) continue;

            if (request.Stat is { } stat)
                tank.UpgradeHistory.Execute(new UpgradeStatCommand(tank, stat));
            else
                tank.UpgradeHistory.Undo();
        }
    }

    private void DrainConsole()
    {
        while (_pendingConsole.TryDequeue(out var request))
        {
            bool server = request.ConnectionId == ServerConsoleId;
            Tank? caller = null;
            if (!server && _connectionToTank.TryGetValue(request.ConnectionId, out var id))
                caller = _entities.GetValueOrDefault(id) as Tank;
            if (!server && caller is null) continue;   // left before the line was run

            var context = new ConsoleContext(this, caller, isAdmin: server || OpenConsole);
            string output = _console.Execute(request.Text, context);
            _consoleReplies.Enqueue(new ConsoleLine(request.ConnectionId, output));
        }
    }

    // ---- IConsoleWorld (used by console expressions, on the game-loop thread) -----------

    IReadOnlyList<Tank> IConsoleWorld.Tanks => _entities.Values.OfType<Tank>().ToList();

    bool IConsoleWorld.IsBot(Tank tank) => _bots.Any(b => b.Tank == tank);

    int IConsoleWorld.BotCount => _targetBotCount;

    void IConsoleWorld.SetBotCount(int count) => _targetBotCount = Math.Max(0, count);

    private void UpdateBots(float dt)
    {
        if (_bots.Count == 0) return;

        _tankView.Clear();
        _shapeView.Clear();
        foreach (var e in _entities.Values)
        {
            if (e is Tank t) _tankView.Add(t);
            else if (e is Shape s) _shapeView.Add(s);
        }

        foreach (var bot in _bots)
            bot.Update(dt, _tankView, _shapeView);
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
        foreach (var b in spawned) Add(b);
    }

    // Damage only; XP for kills is paid by XpAwardObserver when the victim reports its death.
    private void ResolveCollisions(float dt) => _collisions.Resolve(_entities, dt);

    // Dead tanks stay: their DeadState counts down and respawns them (State pattern).
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
            Shape shape = _shapeSpawner.Spawn(NextId(), SpawnPoints.Random(_rng), _rng);
            Add(shape);
        }
    }

    private void MaintainBots()
    {
        while (_bots.Count > _targetBotCount)
        {
            BotBrain last = _bots[^1];
            _bots.RemoveAt(_bots.Count - 1);
            _entities.Remove(last.Tank.Id);
            _eventBus.Notify(new PlayerLeftEvent(last.Tank));
        }

        while (_bots.Count < _targetBotCount)
        {
            // Prototype: copy a bot configured once by Builder + Abstract Factory.
            var archetypes = Enum.GetValues<TankArchetype>();
            TankArchetype archetype = archetypes[_rng.Next(archetypes.Length)];
            int id = NextId();
            string name = _bots.Count < BotNames.Length
                ? BotNames[_bots.Count]
                : $"{BotNames[_bots.Count % BotNames.Length]} {_bots.Count / BotNames.Length + 1}";

            Tank bot = _botPrototypes.Spawn(id, name, archetype, SpawnPoints.Random(_rng));
            Add(bot);
            // Strategy: the brain (client) picks which movement strategy the controller (context) runs.
            _bots.Add(new BotBrain(bot, _rng));
            _eventBus.Notify(new PlayerJoinedEvent(bot));
        }
    }

    /// <summary>A spend (<see cref="Stat"/> set) or an undo (<see cref="Stat"/> null) from one connection.</summary>
    private readonly record struct UpgradeRequest(string ConnectionId, StatKind? Stat);
}

/// <summary>A console line (on the way in) or its output (on the way out) for one connection.</summary>
public readonly record struct ConsoleLine(string ConnectionId, string Text);
