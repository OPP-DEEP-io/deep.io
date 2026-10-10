using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Server.Factories;
using DeepIo.Shared;

namespace DeepIo.Server.Prototypes;

public sealed class TankPrototypeRegistry
{
    private readonly Dictionary<TankArchetype, IPrototype<Tank>> _prototypes;

    public TankPrototypeRegistry(TankAssembler assembler)
    {
        _prototypes = assembler.Factories.ToDictionary(
            factory => factory.Archetype,
            factory => (IPrototype<Tank>)assembler.AssembleBot(
                0, "", "Bot template", factory.Archetype, Vector2.Zero));
    }

    public Tank Spawn(int id, string name, TankArchetype archetype, Vector2 position)
    {
        IPrototype<Tank> prototype = _prototypes.GetValueOrDefault(archetype)
            ?? _prototypes[TankArchetype.Basic];
        Tank copy = prototype.Clone();
        copy.AssignSpawnId(id);
        copy.Name = name;
        copy.ConnectionId = $"bot:{id}";
        copy.Position = position;
        return copy;
    }
}
