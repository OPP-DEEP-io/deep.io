using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Factories;

/// <summary>
/// Factory Method creator. <see cref="Create"/> is the template every polygon goes through
/// (id, position, hitpoints, random spin); the single varying step — deciding *which*
/// <see cref="Shape"/> subclass to allocate — is deferred to <see cref="NewShape"/>, which
/// each concrete creator overrides.
///
/// Product family: <see cref="SquareShape"/>, <see cref="TriangleShape"/>,
/// <see cref="PentagonShape"/>.
/// </summary>
public abstract class ShapeFactory
{
    /// <summary>Which polygon this creator produces, used for logging and diagnostics.</summary>
    public abstract ShapeKind Kind { get; }

    /// <summary>Relative chance this creator is picked when the arena restocks.</summary>
    public abstract int SpawnWeight { get; }

    protected abstract float Radius { get; }
    protected abstract float Hitpoints { get; }
    protected abstract float MaxSpin { get; }

    /// <summary>The factory method: allocates the concrete product, nothing else.</summary>
    protected abstract Shape NewShape(int id);

    /// <summary>Produces a fully initialised polygon ready to be inserted into the world.</summary>
    public Shape Create(int id, Vector2 position, Random rng)
    {
        Shape shape = NewShape(id);

        shape.Position = position;
        shape.Radius = Radius;
        shape.MaxHp = Hitpoints;
        shape.Hp = Hitpoints;
        shape.Rotation = (float)(rng.NextDouble() * MathF.Tau);
        shape.SpinSpeed = (float)((rng.NextDouble() * 2 - 1) * MaxSpin);

        return shape;
    }
}

/// <summary>Concrete creator for <see cref="SquareShape"/>.</summary>
public sealed class SquareFactory : ShapeFactory
{
    public override ShapeKind Kind => ShapeKind.Square;
    public override int SpawnWeight => 60;
    protected override float Radius => 18f;
    protected override float Hitpoints => 30f;
    protected override float MaxSpin => 0.3f;

    protected override Shape NewShape(int id) => new SquareShape { Id = id };
}

/// <summary>Concrete creator for <see cref="TriangleShape"/>.</summary>
public sealed class TriangleFactory : ShapeFactory
{
    public override ShapeKind Kind => ShapeKind.Triangle;
    public override int SpawnWeight => 30;
    protected override float Radius => 24f;
    protected override float Hitpoints => 45f;
    protected override float MaxSpin => 0.5f;

    protected override Shape NewShape(int id) => new TriangleShape { Id = id };
}

/// <summary>Concrete creator for <see cref="PentagonShape"/>.</summary>
public sealed class PentagonFactory : ShapeFactory
{
    public override ShapeKind Kind => ShapeKind.Pentagon;
    public override int SpawnWeight => 10;
    protected override float Radius => 34f;
    protected override float Hitpoints => 100f;
    protected override float MaxSpin => 0.8f;

    protected override Shape NewShape(int id) => new PentagonShape { Id = id };
}
