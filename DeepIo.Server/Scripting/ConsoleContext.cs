using DeepIo.Server.Entities;

namespace DeepIo.Server.Scripting;

/// <summary>
/// The world as console commands see it. <c>GameWorld</c> implements it; the pattern demo uses
/// a small fake, so expressions can be interpreted without the singleton arena.
/// </summary>
public interface IConsoleWorld
{
    /// <summary>Every tank in the arena, humans and bots, dead or alive.</summary>
    IReadOnlyList<Tank> Tanks { get; }

    bool IsBot(Tank tank);

    int BotCount { get; }

    /// <summary>Sets how many bots the arena keeps; extra bots leave, missing ones join next tick.</summary>
    void SetBotCount(int count);
}

/// <summary>
/// Interpreter: Context. Everything an expression needs while it is interpreted: the world,
/// who typed the line (for <c>me</c>) and whether they may touch other tanks.
/// </summary>
public sealed class ConsoleContext
{
    public ConsoleContext(IConsoleWorld world, Tank? caller, bool isAdmin)
    {
        World = world;
        Caller = caller;
        IsAdmin = isAdmin;
    }

    public IConsoleWorld World { get; }

    /// <summary>The player's own tank; null on the server console, where <c>me</c> has no meaning.</summary>
    public Tank? Caller { get; }

    /// <summary>The server console is always admin; players are admin only with --open-console.</summary>
    public bool IsAdmin { get; }

    /// <summary>Players may change only their own tank.</summary>
    public void RequireAllowed(IReadOnlyList<Tank> targets, string command)
    {
        if (IsAdmin) return;
        foreach (Tank tank in targets)
            if (tank != Caller)
                throw new ConsoleRuntimeException($"'{command}' on other tanks needs an admin console");
    }

    public void RequireAdmin(string command)
    {
        if (!IsAdmin) throw new ConsoleRuntimeException($"'{command}' needs an admin console");
    }
}
