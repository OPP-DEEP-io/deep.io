namespace DeepIo.Shared;

/// <summary>The passive polygon shapes that populate the arena.</summary>
public enum ShapeKind
{
    Square = 0,
    Triangle = 1,
    Pentagon = 2,
}

/// <summary>
/// The tank builds a player can pick on the join screen. Each value selects one concrete
/// Abstract Factory on the server (chassis + weapon + projectile are assembled together so
/// a build can never be mixed, e.g. a sniper barrel on a machine-gun chassis).
/// </summary>
public enum TankArchetype
{
    Basic = 0,
    Sniper = 1,
    MachineGun = 2,
}
