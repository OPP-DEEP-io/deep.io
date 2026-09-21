using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Server.Entities.Parts;
using DeepIo.Shared;

namespace DeepIo.Server.Factories;

/// <summary>
/// The Abstract Factory client. It resolves the archetype a player picked to one concrete
/// <see cref="ITankPartsFactory"/> and assembles a <see cref="Tank"/> purely from the
/// interfaces — it never mentions <c>LightChassis</c>, <c>LongBarrel</c> or any other
/// concrete part, so a new build only means a new factory in the registry below.
/// </summary>
public sealed class TankAssembler
{
    private readonly IReadOnlyDictionary<TankArchetype, ITankPartsFactory> _factories;

    public TankAssembler() : this(new ITankPartsFactory[]
    {
        new BasicTankFactory(),
        new SniperTankFactory(),
        new MachineGunTankFactory(),
    })
    {
    }

    public TankAssembler(IReadOnlyList<ITankPartsFactory> factories)
    {
        _factories = factories.ToDictionary(f => f.Archetype);
    }

    public IEnumerable<ITankPartsFactory> Factories => _factories.Values;

    /// <summary>Unknown archetypes fall back to the starter build rather than throwing.</summary>
    public ITankPartsFactory Resolve(TankArchetype archetype) =>
        _factories.TryGetValue(archetype, out ITankPartsFactory? factory)
            ? factory
            : _factories[TankArchetype.Basic];

    /// <summary>Builds a spawn-ready tank from one consistent family of parts.</summary>
    public Tank Assemble(int id, string connectionId, string name, TankArchetype archetype, Vector2 position)
    {
        ITankPartsFactory factory = Resolve(archetype);

        IChassis chassis = factory.CreateChassis();
        IWeapon weapon = factory.CreateWeapon();
        IProjectileSpec round = factory.CreateProjectile();

        return new Tank
        {
            Id = id,
            Chassis = chassis,
            Weapon = weapon,
            Round = round,
            Archetype = factory.Archetype,
            Name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim(),
            ConnectionId = connectionId,
            Position = position,
            Radius = chassis.Radius,
            Hp = chassis.MaxHp,
            MaxHp = chassis.MaxHp,
        };
    }
}
