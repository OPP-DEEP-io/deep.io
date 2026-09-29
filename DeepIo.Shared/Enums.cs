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

/// <summary>
/// The eight upgradable tank stats. A tank earns one skill point per level and spends it on
/// one of these (keys 1-8 on the client); each spend is an undoable UpgradeStatCommand on the
/// server. The numeric values double as the index into <c>EntityDto.Up</c>.
/// </summary>
public enum StatKind
{
    HealthRegen = 0,
    MaxHealth = 1,
    BodyDamage = 2,
    BulletSpeed = 3,
    BulletPenetration = 4,
    BulletDamage = 5,
    Reload = 6,
    MovementSpeed = 7,
}
