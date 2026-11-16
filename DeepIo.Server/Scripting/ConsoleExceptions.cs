namespace DeepIo.Server.Scripting;

/// <summary>A console line that could not be run; the message is shown to whoever typed it.</summary>
public abstract class ConsoleException(string message) : Exception(message);

/// <summary>The line does not match the grammar. <see cref="Position"/> is the 1-based column.</summary>
public sealed class ConsoleSyntaxException(string message, int position)
    : ConsoleException($"{message} (at column {position})")
{
    public int Position { get; } = position;
}

/// <summary>The line parsed, but interpreting it failed (bad value, not allowed, nobody to target).</summary>
public sealed class ConsoleRuntimeException(string message) : ConsoleException(message);
