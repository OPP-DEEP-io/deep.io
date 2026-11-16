using System.Text;
using DeepIo.Server.Commands;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Scripting.Expressions;

// Interpreter: NonterminalExpressions for <command>. Each one interprets its child expressions
// first (numbers, stats, targets) and then acts on the world. The result is the text sent back.

/// <summary><c>&lt;command&gt; { ";" &lt;command&gt; }</c>: runs the commands in order.</summary>
public sealed class SequenceCommand(IReadOnlyList<IExpression<string>> commands) : IExpression<string>
{
    public IReadOnlyList<IExpression<string>> Commands { get; } = commands;

    public string Interpret(ConsoleContext context) =>
        string.Join('\n', Commands.Select(c => c.Interpret(context)));

    public override string ToString() => $"Seq({string.Join(", ", Commands)})";
}

/// <summary><c>"repeat" &lt;number&gt; "(" &lt;script&gt; ")"</c>: interprets the body several times.</summary>
public sealed class RepeatCommand(IExpression<int> count, IExpression<string> body) : IExpression<string>
{
    public const int MaxCount = 20;

    public string Interpret(ConsoleContext context)
    {
        int n = count.Interpret(context);
        if (n is < 1 or > MaxCount) throw new ConsoleRuntimeException($"repeat count must be 1..{MaxCount}");

        var output = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) output.Append('\n');
            output.Append(body.Interpret(context));
        }
        return output.ToString();
    }

    public override string ToString() => $"Repeat({count}, {body})";
}

/// <summary><c>"give" &lt;number&gt; "xp" "to" &lt;target&gt;</c>: awards XP like a kill would, levelling up.</summary>
public sealed class GiveXpCommand(IExpression<int> amount, IExpression<IReadOnlyList<Tank>> target) : IExpression<string>
{
    public const int MaxAmount = 100_000;

    public string Interpret(ConsoleContext context)
    {
        int xp = amount.Interpret(context);
        if (xp is < 1 or > MaxAmount) throw new ConsoleRuntimeException($"xp must be 1..{MaxAmount}");

        IReadOnlyList<Tank> tanks = target.Interpret(context);
        context.RequireAllowed(tanks, "give");
        foreach (Tank tank in tanks) tank.AwardXp(xp);

        return Describe(tanks, $"+{xp} xp");
    }

    internal static string Describe(IReadOnlyList<Tank> tanks, string what) =>
        tanks.Count == 0
            ? "no tanks matched"
            : $"{what}: {string.Join(", ", tanks.Select(t => $"{t.Name} (Lv {t.Level})"))}";

    public override string ToString() => $"GiveXp({amount}, {target})";
}

/// <summary>
/// <c>"upgrade" &lt;target&gt; &lt;stat&gt; [ &lt;number&gt; ]</c>: spends skill points. Goes through the
/// same undoable <see cref="UpgradeStatCommand"/> as keys 1-8, so Backspace reverts it.
/// </summary>
public sealed class UpgradeCommand(IExpression<IReadOnlyList<Tank>> target, IExpression<StatKind> stat, IExpression<int> times)
    : IExpression<string>
{
    public string Interpret(ConsoleContext context)
    {
        StatKind kind = stat.Interpret(context);
        int n = times.Interpret(context);
        if (n is < 1 or > GameConstants.MaxStatLevel)
            throw new ConsoleRuntimeException($"upgrade count must be 1..{GameConstants.MaxStatLevel}");

        IReadOnlyList<Tank> tanks = target.Interpret(context);
        context.RequireAllowed(tanks, "upgrade");
        if (tanks.Count == 0) return "no tanks matched";

        var lines = new List<string>();
        foreach (Tank tank in tanks)
        {
            int done = 0;
            while (done < n && tank.UpgradeHistory.Execute(new UpgradeStatCommand(tank, kind))) done++;
            string note = done < n ? " (no points left or stat maxed)" : "";
            lines.Add($"{tank.Name}: {kind} +{done}{note}");
        }
        return string.Join('\n', lines);
    }

    public override string ToString() => $"Upgrade({target}, {stat}, {times})";
}

/// <summary><c>"heal" &lt;target&gt;</c>: restores living tanks to full HP.</summary>
public sealed class HealCommand(IExpression<IReadOnlyList<Tank>> target) : IExpression<string>
{
    public string Interpret(ConsoleContext context)
    {
        IReadOnlyList<Tank> tanks = target.Interpret(context);
        context.RequireAllowed(tanks, "heal");

        List<Tank> living = tanks.Where(t => !t.Dead).ToList();
        foreach (Tank tank in living) tank.Heal(tank.MaxHp);
        return GiveXpCommand.Describe(living, "healed");
    }

    public override string ToString() => $"Heal({target})";
}

/// <summary><c>"kill" &lt;target&gt;</c>: destroys tanks, even through a spawn shield (State pattern).</summary>
public sealed class KillCommand(IExpression<IReadOnlyList<Tank>> target) : IExpression<string>
{
    public string Interpret(ConsoleContext context)
    {
        IReadOnlyList<Tank> tanks = target.Interpret(context);
        context.RequireAllowed(tanks, "kill");

        List<Tank> killed = tanks.Where(t => t.ForceKill()).ToList();
        return GiveXpCommand.Describe(killed, "destroyed");
    }

    public override string ToString() => $"Kill({target})";
}

/// <summary><c>"state" &lt;target&gt;</c>: prints each tank's life-cycle state.</summary>
public sealed class ShowStateCommand(IExpression<IReadOnlyList<Tank>> target) : IExpression<string>
{
    public string Interpret(ConsoleContext context)
    {
        IReadOnlyList<Tank> tanks = target.Interpret(context);
        if (tanks.Count == 0) return "no tanks matched";

        return string.Join('\n', tanks.Select(t =>
            t.State.SecondsLeft > 0f
                ? $"{t.Name}: {t.State.Kind} ({t.State.SecondsLeft:F1} s left)"
                : $"{t.Name}: {t.State.Kind}"));
    }

    public override string ToString() => $"State({target})";
}

/// <summary><c>"list"</c>: one line per tank with id, level, state and, for bots, the movement strategy.</summary>
public sealed class ListCommand : IExpression<string>
{
    public string Interpret(ConsoleContext context)
    {
        IReadOnlyList<Tank> tanks = context.World.Tanks;
        if (tanks.Count == 0) return "the arena is empty";

        return string.Join('\n', tanks.OrderBy(t => t.Id).Select(t =>
        {
            string who = context.World.IsBot(t) ? $"bot [{t.AiLabel}]" : "player";
            return $"#{t.Id} {t.Name}  Lv {t.Level}  {t.State.Kind}  {who}";
        }));
    }

    public override string ToString() => "List";
}

/// <summary><c>"bots" &lt;number&gt;</c>: how many bots the arena keeps (admin only).</summary>
public sealed class SetBotCountCommand(IExpression<int> count) : IExpression<string>
{
    public const int MaxBots = 20;

    public string Interpret(ConsoleContext context)
    {
        context.RequireAdmin("bots");
        int n = count.Interpret(context);
        if (n is < 0 or > MaxBots) throw new ConsoleRuntimeException($"bot count must be 0..{MaxBots}");

        int before = context.World.BotCount;
        context.World.SetBotCount(n);
        return $"bots: {before} -> {n}";
    }

    public override string ToString() => $"SetBotCount({count})";
}

/// <summary><c>"help"</c>: the grammar in short.</summary>
public sealed class HelpCommand : IExpression<string>
{
    public const string Text =
        """
        commands (join with ';'):
          help | list | state <target>
          give <n> xp to <target>
          upgrade <target> <stat> [n]
          heal <target> | kill <target>
          bots <n>
          repeat <n> ( <commands> )
        target: me | all | bots | players | "name" | #id, joined with 'and' / 'except'
        stat: regen health body bulletspeed penetration damage reload speed
        """;

    public string Interpret(ConsoleContext context) => Text;

    public override string ToString() => "Help";
}
