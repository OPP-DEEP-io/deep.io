using System.Numerics;
using DeepIo.Server.Builders;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Factories;

/// <summary>
/// Builder director and Abstract Factory client. It resolves the archetype to one
/// <see cref="ITankPartsFactory"/> and runs the same construction steps with either a
/// player or bot builder. Builders request parts through interfaces, so this director
/// never names a concrete chassis, weapon, or projectile.
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

    /// <summary>Director: executes the same steps with the player builder.</summary>
    public Tank Assemble(int id, string connectionId, string name, TankArchetype archetype, Vector2 position)
    {
        ITankPartsFactory factory = Resolve(archetype);
        return Construct(new PlayerTankBuilder(factory), id, connectionId, name, position);
    }

    /// <summary>Director: executes the same steps with the trained bot builder.</summary>
    public Tank AssembleBot(int id, string connectionId, string name, TankArchetype archetype, Vector2 position)
    {
        ITankPartsFactory factory = Resolve(archetype);
        return Construct(new BotTankBuilder(factory), id, connectionId, name, position);
    }

    private static Tank Construct(ITankBuilder builder, int id, string connectionId, string name, Vector2 position) =>
        builder.Start(id, connectionId, name, position)
            .AssembleBody()
            .AddWeapon()
            .LoadProjectile()
            .Build();
}
