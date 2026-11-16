using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Scripting.Expressions;

// Interpreter: TerminalExpressions. Leaves of the syntax tree; each one is a single token of
// the grammar and interprets to a value without evaluating any child expression.

/// <summary><c>&lt;number&gt;</c></summary>
public sealed class NumberLiteral(int value) : IExpression<int>
{
    public int Value { get; } = value;

    public int Interpret(ConsoleContext context) => Value;

    public override string ToString() => Value.ToString();
}

/// <summary><c>&lt;stat&gt;</c>: one of the eight upgradable stats, by its console name.</summary>
public sealed class StatLiteral(StatKind stat) : IExpression<StatKind>
{
    public static readonly IReadOnlyDictionary<string, StatKind> Names = new Dictionary<string, StatKind>
    {
        ["regen"] = StatKind.HealthRegen,
        ["health"] = StatKind.MaxHealth,
        ["body"] = StatKind.BodyDamage,
        ["bulletspeed"] = StatKind.BulletSpeed,
        ["penetration"] = StatKind.BulletPenetration,
        ["damage"] = StatKind.BulletDamage,
        ["reload"] = StatKind.Reload,
        ["speed"] = StatKind.MovementSpeed,
    };

    public StatKind Stat { get; } = stat;

    public StatKind Interpret(ConsoleContext context) => Stat;

    public override string ToString() => Stat.ToString();
}

/// <summary><c>"me"</c>: the tank of whoever typed the line.</summary>
public sealed class MeSelector : IExpression<IReadOnlyList<Tank>>
{
    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        context.Caller is { } me
            ? [me]
            : throw new ConsoleRuntimeException("'me' only works in the in-game console");

    public override string ToString() => "Me";
}

/// <summary><c>"all"</c>: every tank in the arena.</summary>
public sealed class AllSelector : IExpression<IReadOnlyList<Tank>>
{
    public IReadOnlyList<Tank> Interpret(ConsoleContext context) => context.World.Tanks;

    public override string ToString() => "All";
}

/// <summary><c>"bots"</c>: the server-controlled tanks.</summary>
public sealed class BotsSelector : IExpression<IReadOnlyList<Tank>>
{
    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        context.World.Tanks.Where(context.World.IsBot).ToList();

    public override string ToString() => "Bots";
}

/// <summary><c>"players"</c>: the human-controlled tanks.</summary>
public sealed class PlayersSelector : IExpression<IReadOnlyList<Tank>>
{
    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        context.World.Tanks.Where(t => !context.World.IsBot(t)).ToList();

    public override string ToString() => "Players";
}

/// <summary><c>&lt;string&gt;</c>: tanks with this display name (case-insensitive).</summary>
public sealed class NameSelector(string name) : IExpression<IReadOnlyList<Tank>>
{
    public string Name { get; } = name;

    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        context.World.Tanks.Where(t => string.Equals(t.Name, Name, StringComparison.OrdinalIgnoreCase)).ToList();

    public override string ToString() => $"Name(\"{Name}\")";
}

/// <summary><c>"#" &lt;number&gt;</c>: the tank with this entity id.</summary>
public sealed class IdSelector(int id) : IExpression<IReadOnlyList<Tank>>
{
    public int Id { get; } = id;

    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        context.World.Tanks.Where(t => t.Id == Id).ToList();

    public override string ToString() => $"Id({Id})";
}
