namespace DeepIo.Server.Commands;

/// <summary>
/// Invoker. Runs commands and keeps the last <see cref="Capacity"/> that actually did
/// something so they can be undone newest-first. When full, the oldest entry is dropped, so
/// it doubles as a ring buffer of recent input.
/// </summary>
public sealed class CommandHistory
{
    private readonly LinkedList<ICommand> _done = new();

    public CommandHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count => _done.Count;

    /// <summary>Executes <paramref name="command"/> and records it if it had an effect.</summary>
    public bool Execute(ICommand command)
    {
        if (!command.Execute()) return false;

        _done.AddLast(command);
        if (_done.Count > Capacity) _done.RemoveFirst();
        return true;
    }

    /// <summary>Reverts the most recent recorded command. False if there is nothing to undo.</summary>
    public bool Undo()
    {
        if (_done.Last is not { } last) return false;

        _done.RemoveLast();
        last.Value.Undo();
        return true;
    }

    public void Clear() => _done.Clear();
}
