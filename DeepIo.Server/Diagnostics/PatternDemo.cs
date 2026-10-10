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
using DeepIo.Server.Factories;
using DeepIo.Server.Prototypes;
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
