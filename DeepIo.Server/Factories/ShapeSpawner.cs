using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Factories;

/// <summary>
/// Holds the registered <see cref="ShapeFactory"/> creators and picks one per spawn using
/// their spawn weights. GameWorld asks this for polygons and never names a concrete
/// <see cref="Shape"/> subclass, so adding a new polygon is one extra creator here.
/// </summary>
public sealed class ShapeSpawner
{
    private readonly IReadOnlyList<ShapeFactory> _factories;
    private readonly int _totalWeight;

    public ShapeSpawner() : this(new ShapeFactory[]
    {
        new SquareFactory(),
        new TriangleFactory(),
        new PentagonFactory(),
    })
    {
    }

    public ShapeSpawner(IReadOnlyList<ShapeFactory> factories)
    {
        if (factories.Count == 0)
            throw new ArgumentException("At least one shape factory is required.", nameof(factories));

        _factories = factories;
        _totalWeight = factories.Sum(f => f.SpawnWeight);
    }

    public IReadOnlyList<ShapeFactory> Factories => _factories;

    /// <summary>Weighted pick over the registered creators.</summary>
    public ShapeFactory Pick(Random rng)
    {
        int roll = rng.Next(_totalWeight);
        foreach (ShapeFactory factory in _factories)
        {
            roll -= factory.SpawnWeight;
            if (roll < 0) return factory;
        }
        return _factories[^1];
    }

    /// <summary>Picks a creator and asks it for one finished polygon.</summary>
    public Shape Spawn(int id, Vector2 position, Random rng) => Pick(rng).Create(id, position, rng);

    /// <summary>Spawns a specific polygon, bypassing the weighted roll (used by tests/tools).</summary>
    public Shape Spawn(ShapeKind kind, int id, Vector2 position, Random rng)
    {
        foreach (ShapeFactory factory in _factories)
            if (factory.Kind == kind)
                return factory.Create(id, position, rng);

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "No factory registered for this shape.");
    }
}
