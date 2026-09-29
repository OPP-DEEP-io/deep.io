namespace DeepIo.Server.Commands;

/// <summary>
/// Command. One request to a tank, packaged as an object so it can be queued, recorded in a
/// <see cref="CommandHistory"/> and reverted. Human input and bot AI produce the same
/// commands, so a tank cannot tell who is driving it.
/// </summary>
public interface ICommand
{
    /// <summary>Carries out the request.</summary>
    /// <returns>
    /// False when there was nothing to do (e.g. no skill point to spend). The invoker does not
    /// record such a command, so Undo never reverts something that never happened.
    /// </returns>
    bool Execute();

    /// <summary>Reverts exactly what <see cref="Execute"/> changed.</summary>
    void Undo();
}
