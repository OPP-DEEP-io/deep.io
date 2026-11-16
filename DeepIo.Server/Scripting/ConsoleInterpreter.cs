namespace DeepIo.Server.Scripting;

/// <summary>
/// Interpreter client: parses a console line into an expression tree and interprets it. Used by
/// the in-game console (through the hub) and the server's stdin console alike. Must run on the
/// game-loop thread, because the expressions change the world directly.
/// </summary>
public sealed class ConsoleInterpreter
{
    /// <summary>Runs <paramref name="line"/> and returns what to print; errors come back as text.</summary>
    public string Execute(string line, ConsoleContext context)
    {
        try
        {
            return ConsoleParser.Parse(line).Interpret(context);
        }
        catch (ConsoleException ex)
        {
            return "error: " + ex.Message;
        }
    }
}
