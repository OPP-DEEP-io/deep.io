using DeepIo.Server.Entities;

namespace DeepIo.Server.Events.Observers;

/// <summary>
/// Pays out XP for kills. Collision code only deals damage; whoever lands the killing blow
/// gets the victim's bounty here. When the payout crosses a level threshold the tank
/// publishes <see cref="PlayerLeveledUpEvent"/> back onto the bus for the others.
/// </summary>
public sealed class XpAwardObserver : IGameObserver
{
    public const int TankKillXp = 100;

    public void OnNotify(GameEvent gameEvent)
    {
        if (gameEvent is not EntityDestroyedEvent { Killer: Tank killer } destroyed) return;

        int xp = destroyed.Victim switch
        {
            Shape shape => shape.XpValue,
            Tank => TankKillXp,
            _ => 0,
        };
        if (xp == 0) return;

        killer.AwardXp(xp);
    }
}
