using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using DeepIo.Server.Builders;
using DeepIo.Server.Commands;
using DeepIo.Server.Combat;
using DeepIo.Server.Entities;
using DeepIo.Server.Entities.Parts;
using DeepIo.Server.Entities.States;
using DeepIo.Server.Events;
using DeepIo.Server.Factories;
using DeepIo.Server.Prototypes;
using DeepIo.Server.Scripting;
using DeepIo.Server.World;
using DeepIo.Shared;

namespace DeepIo.Server.Diagnostics;

/// <summary>Executable lab demonstration and regression checks, with no graphics or network needed.</summary>
public static class PatternDemo
{
    private static int _checks;

    public static void Run()
    {
        _checks = 0;
        var assembler = new TankAssembler();
        DemonstratePrototype(assembler);
        DemonstrateState(assembler);
        DemonstrateInterpreter(assembler);
        Console.WriteLine();
        Console.WriteLine($"All {_checks} checks passed.");
    }

    private static void DemonstrateState(TankAssembler assembler)
    {
        Console.WriteLine();
        Console.WriteLine("STATE: tank life cycle driven by the states themselves");
        var bus = new GameEventBus();
        var log = new StateLog();
        bus.Attach(log);

        Tank tank = assembler.Assemble(30, "state-demo", "Stateful", TankArchetype.Basic, Vector2.Zero);
        tank.EventBus = bus;
        float full = tank.MaxHp;

        Check(tank.State is SpawnProtectedState, "A new tank starts behind a spawn shield");
        Check(!tank.ApplyDamage(50f) && tank.Hp == full, "The spawn shield absorbs damage");
        Check(!tank.State.IsTargetable, "Bots ignore a shielded tank");

        Simulate(tank, SpawnProtectedState.Seconds + 0.1f);
        Check(tank.State is AliveState, "The shield expires on its own after 3 s");

        tank.ApplyDamage(full * 0.8f);
        Check(tank.Hp < full * 0.25f, "An alive tank takes damage");
        float before = tank.Hp;
        Simulate(tank, 1f);
        float aliveRegen = tank.Hp - before;

        Simulate(tank, AliveState.OutOfCombatSeconds);
        Check(tank.State is RegeneratingState, "5 s without combat switches to Regenerating");
        before = tank.Hp;
        Simulate(tank, 1f);
        float fastRegen = tank.Hp - before;
        Console.WriteLine($"  HP regained in 1 s: alive={aliveRegen:F1}, regenerating={fastRegen:F1}");
        Check(MathF.Abs(fastRegen - aliveRegen * RegeneratingState.Multiplier) < 0.5f,
            "Regenerating heals four times faster");

        tank.ApplyDamage(1f);
        Check(tank.State is AliveState, "A hit while regenerating returns the tank to Alive");

        tank.ApplyDamage(tank.Hp);
        Check(tank.State is DeadState && tank.Dead, "A killing blow switches to Dead");
        tank.MoveDir = Vector2.UnitX;
        tank.Firing = true;
        tank.ReloadTimer = 0f;
        Vector2 deathSpot = tank.Position;
        Simulate(tank, 1f);
        Check(!tank.CanFire && tank.Position == deathSpot && tank.Hp <= 0f,
            "A dead tank cannot fire, move or heal");

        Simulate(tank, DeadState.Seconds);
        Check(tank.State is SpawnProtectedState && tank.Hp == tank.MaxHp, "After 3 s the tank respawns behind a new shield");

        tank.BeginReload();
        Check(tank.State is AliveState, "Firing drops the spawn shield early");
        Check(tank.ForceKill() && tank.State is DeadState, "An admin kill works in any living state");

        Console.WriteLine("  Transitions: " + string.Join(", ", log.Lines));
        Check(log.Lines.Count == 7, "Every transition was announced on the event bus");

        Tank clone = tank.Clone(CopyDepth.Deep);
        Check(clone.State is SpawnProtectedState && !ReferenceEquals(clone.State, tank.State),
            "Prototype: a deep copy gets its own state object");
    }

    private static void DemonstrateInterpreter(TankAssembler assembler)
    {
        Console.WriteLine();
        Console.WriteLine("INTERPRETER: console lines -> expression tree -> interpreted against the world");
        var world = new FakeConsoleWorld();
        var bus = new GameEventBus();
        Tank me = world.AddPlayer(assembler.Assemble(40, "c1", "Tomas", TankArchetype.Basic, Vector2.Zero), bus);
        Tank other = world.AddPlayer(assembler.Assemble(41, "c2", "Ona", TankArchetype.Sniper, Vector2.Zero), bus);
        Tank b1 = world.AddBot(assembler.AssembleBot(42, "bot:42", "B1", TankArchetype.Basic, Vector2.Zero), bus);
        Tank b2 = world.AddBot(assembler.AssembleBot(43, "bot:43", "B2", TankArchetype.MachineGun, Vector2.Zero), bus);
        var interpreter = new ConsoleInterpreter();
        var admin = new ConsoleContext(world, me, isAdmin: true);

        const string line = "give 200 xp to bots except \"B1\"; state me and #41";
        IExpression<string> tree = ConsoleParser.Parse(line);
        Console.WriteLine($"  > {line}");
        Console.WriteLine($"  tree: {tree}");
        Check(tree.ToString() == "Seq(GiveXp(200, Except(Bots, Name(\"B1\"))), State(Union(Me, Id(41))))",
            "The parser builds the expected expression tree");

        int b1Score = b1.Score, b2Score = b2.Score;
        string output = tree.Interpret(admin);
        Console.WriteLine("  " + output.Replace("\n", "\n  "));
        Check(b2.Score == b2Score + 200 && b1.Score == b1Score, "'except' removed B1 from the bots");
        Check(output.Contains("Tomas: SpawnProtected") && output.Contains("Ona: SpawnProtected"),
            "'and' joined me with #41");

        int myScore = me.Score;
        interpreter.Execute("repeat 3 (give 100 xp to me)", admin);
        Check(me.Score == myScore + 300, "repeat interprets its body three times");

        int points = me.SkillPoints;
        string upgrade = interpreter.Execute("upgrade me reload 2", admin);
        Console.WriteLine($"  > upgrade me reload 2 -> {upgrade}");
        Check(me.SkillPoints == points - 2 && me.Stats[StatKind.Reload] == 2, "upgrade spends points through UpgradeStatCommand");
        me.UpgradeHistory.Undo();
        Check(me.Stats[StatKind.Reload] == 1, "...so it can be undone like a key press (Command)");

        interpreter.Execute("kill bots", admin);
        Check(b1.State is DeadState && b2.State is DeadState && me.State is not DeadState,
            "kill goes through the spawn shield (State)");

        var player = new ConsoleContext(world, me, isAdmin: false);
        string denied = interpreter.Execute("kill \"Ona\"", player);
        Console.WriteLine($"  > kill \"Ona\" (as a player) -> {denied}");
        Check(denied.StartsWith("error:") && other.State is not DeadState, "players may only target themselves");
        Check(interpreter.Execute("heal me", player).StartsWith("healed"), "...but 'me' is allowed");

        foreach (string bad in new[] { "give xp to me", "upgrade me relaod", "kill everyone", "repeat 3 (list" })
        {
            string error = interpreter.Execute(bad, admin);
            Console.WriteLine($"  > {bad} -> {error}");
            Check(error.StartsWith("error:"), "Syntax errors are reported, not thrown");
        }

        interpreter.Execute("bots 7", admin);
        Check(world.BotCount == 7, "bots sets the arena's bot count");
    }

    /// <summary>Just enough of a world for console expressions, without the singleton arena.</summary>
    private sealed class FakeConsoleWorld : IConsoleWorld
    {
        private readonly List<Tank> _tanks = new();
        private readonly HashSet<Tank> _bots = new();

        public IReadOnlyList<Tank> Tanks => _tanks;

        public int BotCount { get; private set; }

        public bool IsBot(Tank tank) => _bots.Contains(tank);

        public void SetBotCount(int count) => BotCount = count;

        public Tank AddPlayer(Tank tank, IGameSubject bus)
        {
            tank.EventBus = bus;
            _tanks.Add(tank);
            return tank;
        }

        public Tank AddBot(Tank tank, IGameSubject bus)
        {
            _bots.Add(AddPlayer(tank, bus));
            BotCount = _bots.Count;
            return tank;
        }
    }

    /// <summary>Advances one tank at the server tick rate, without a world.</summary>
    private static void Simulate(Tank tank, float seconds)
    {
        for (float t = 0f; t < seconds; t += GameConstants.TickDelta)
            tank.Update(GameConstants.TickDelta);
    }

    private sealed class StateLog : IGameObserver
    {
        public List<string> Lines { get; } = new();

        public void OnNotify(GameEvent gameEvent)
        {
            if (gameEvent is TankStateChangedEvent changed)
                Lines.Add($"{changed.From}->{changed.To}");
        }
    }

    private static void DemonstratePrototype(TankAssembler assembler)
    {
        Console.WriteLine("PROTOTYPE: shallow versus selective deep copy");
        Tank original = assembler.AssembleBot(10, "template", "Template", TankArchetype.Basic, Vector2.Zero);
        original.InputHistory.Execute(new MoveCommand(original, Vector2.UnitX));
        IPrototype<Tank> prototype = original;
        Tank shallow = prototype.Clone(CopyDepth.Shallow);
        Tank deep = prototype.Clone(CopyDepth.Deep);

        Check(!ReferenceEquals(original, shallow) && !ReferenceEquals(original, deep), "Copies are separate tank objects");
        Check(ReferenceEquals(original.Stats, shallow.Stats) && !ReferenceEquals(original.Stats, deep.Stats),
            "Shallow shares stats; deep owns stats");
        Check(ReferenceEquals(original.InputHistory, shallow.InputHistory) &&
            !ReferenceEquals(original.InputHistory, deep.InputHistory) && deep.InputHistory.Count == 0,
            "Deep copies use fresh command histories because commands retain their old receiver");
        Check(ReferenceEquals(original.BaseChassis, deep.BaseChassis) && ReferenceEquals(original.Weapon, deep.Weapon) &&
            ReferenceEquals(original.Round, deep.Round), "Immutable part definitions are safely shared");

        // Reflection is confined to this diagnostic: inspect the actual private mutable arrays,
        // rather than printing an address of ToArray(), which would be a new unrelated copy.
        FieldInfo levels = typeof(TankStats).GetField("_levels", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalArray = (int[])levels.GetValue(original.Stats)!;
        var shallowArray = (int[])levels.GetValue(shallow.Stats)!;
        var deepArray = (int[])levels.GetValue(deep.Stats)!;
        GCHandle originalPin = GCHandle.Alloc(originalArray, GCHandleType.Pinned);
        GCHandle shallowPin = GCHandle.Alloc(shallowArray, GCHandleType.Pinned);
        GCHandle deepPin = GCHandle.Alloc(deepArray, GCHandleType.Pinned);
        try
        {
            Console.WriteLine("  Tank identity tokens (not memory addresses): " +
                $"original={RuntimeHelpers.GetHashCode(original):X}, shallow={RuntimeHelpers.GetHashCode(shallow):X}, deep={RuntimeHelpers.GetHashCode(deep):X}");
            Console.WriteLine("  Actual pinned stat-array data addresses:");
            Console.WriteLine($"    original: 0x{originalPin.AddrOfPinnedObject().ToInt64():X}");
            Console.WriteLine($"    shallow:  0x{shallowPin.AddrOfPinnedObject().ToInt64():X}");
            Console.WriteLine($"    deep:     0x{deepPin.AddrOfPinnedObject().ToInt64():X}");
            Check(originalPin.AddrOfPinnedObject() == shallowPin.AddrOfPinnedObject() &&
                originalPin.AddrOfPinnedObject() != deepPin.AddrOfPinnedObject(), "Memory addresses confirm array sharing versus copying");
        }
        finally
        {
            deepPin.Free();
            shallowPin.Free();
            originalPin.Free();
        }

        shallow.Stats.Increase(StatKind.BulletDamage);
        Check(original.Stats[StatKind.BulletDamage] == 2 && deep.Stats[StatKind.BulletDamage] == 1,
            "Changing the shallow copy also changes the original, but not the deep copy");
        deep.Stats.Increase(StatKind.BulletSpeed);
        Check(original.Stats[StatKind.BulletSpeed] == 0, "Changing the deep copy leaves the original intact");
        Console.WriteLine("  After shallow damage upgrade: original=2, shallow=2, deep=1");

        var registry = new TankPrototypeRegistry(assembler);
        foreach (TankArchetype archetype in Enum.GetValues<TankArchetype>())
        {
            Tank a = registry.Spawn(20, "A", archetype, Vector2.Zero);
            Tank b = registry.Spawn(21, "B", archetype, Vector2.One);
            a.Stats.Increase(StatKind.BulletDamage);
            a.InputHistory.Execute(new FireCommand(a, true));
            Check(b.Stats[StatKind.BulletDamage] == 1 && b.InputHistory.Count == 0 && !b.Firing,
                "Spawned siblings have independent mutable state");
            Tank c = registry.Spawn(22, "C", archetype, Vector2.Zero);
            Check(c.Stats[StatKind.BulletDamage] == 1 && c.Archetype == archetype,
                "Spawning never modifies the stored prototype");
            Check(a.Id == 20 && b.Id == 21 && a.ConnectionId == "bot:20" && b.Position == Vector2.One,
                "Registry assigns fresh identity and position");
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
        _checks++;
    }
}
