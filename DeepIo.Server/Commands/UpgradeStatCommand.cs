using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Commands;

/// <summary>
/// Spends one skill point on a stat. Undo is a respec: the point comes back out of the stat
/// and is refunded, so a player who misclicks (or a bot changing plans) is not stuck with it.
/// </summary>
public sealed class UpgradeStatCommand : ICommand
{
    private readonly Tank _tank;
    private readonly StatKind _stat;
    private Tank.IUpgradeMemento? _before;

    public UpgradeStatCommand(Tank tank, StatKind stat)
    {
        _tank = tank;
        _stat = stat;
    }

    public StatKind Stat => _stat;

    /// <returns>False if the tank has no points left or the stat is already maxed.</returns>
    public bool Execute()
    {
        if (_before is not null) return false;

        _before = _tank.UpgradeWithMemento(_stat);
        return _before is not null;
    }

    public void Undo()
    {
        if (_before is null) return;

        _tank.RestoreUpgrade(_before);
        _before = null;
    }
}
