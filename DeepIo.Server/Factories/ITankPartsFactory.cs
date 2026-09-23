using DeepIo.Server.Entities.Parts;
using DeepIo.Shared;

namespace DeepIo.Server.Factories;

/// <summary>
/// Abstract Factory. One call site asks for a chassis, a weapon and a projectile spec, and
/// whichever concrete factory answers guarantees the three parts belong to the same build.
/// Nothing outside these factories may pair, say, a <see cref="HeavyChassis"/> with a
/// <see cref="RapidBarrel"/>.
/// </summary>
public interface ITankPartsFactory
{
    TankArchetype Archetype { get; }

    /// <summary>Human-readable build name shown on the join screen.</summary>
    string DisplayName { get; }

    IChassis CreateChassis();
    IWeapon CreateWeapon();
    IProjectileSpec CreateProjectile();
}

/// <summary>Concrete factory #1: the all-round starter build.</summary>
public sealed class BasicTankFactory : ITankPartsFactory
{
    public TankArchetype Archetype => TankArchetype.Basic;
    public string DisplayName => "Basic";

    public IChassis CreateChassis() => new LightChassis();
    public IWeapon CreateWeapon() => new SingleBarrel();
    public IProjectileSpec CreateProjectile() => new StandardRound();
}

/// <summary>Concrete factory #2: slow and durable, one heavy round at a time.</summary>
public sealed class SniperTankFactory : ITankPartsFactory
{
    public TankArchetype Archetype => TankArchetype.Sniper;
    public string DisplayName => "Sniper";

    public IChassis CreateChassis() => new HeavyChassis();
    public IWeapon CreateWeapon() => new LongBarrel();
    public IProjectileSpec CreateProjectile() => new SniperRound();
}

/// <summary>Concrete factory #3: fast and fragile, wins by volume of fire.</summary>
public sealed class MachineGunTankFactory : ITankPartsFactory
{
    public TankArchetype Archetype => TankArchetype.MachineGun;
    public string DisplayName => "Machine gun";

    public IChassis CreateChassis() => new AssaultChassis();
    public IWeapon CreateWeapon() => new RapidBarrel();
    public IProjectileSpec CreateProjectile() => new RapidRound();
}
