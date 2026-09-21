using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>
/// A passive polygon worth XP when destroyed — the Factory Method product base.
///
/// The concrete polygons (<see cref="SquareShape"/>, <see cref="TriangleShape"/>,
/// <see cref="PentagonShape"/>) are never constructed by the simulation directly: each one is
/// produced by its own <c>ShapeFactory</c> subclass.
/// </summary>
public abstract class Shape : Entity
{
    public float Rotation;    // radians, for visual spin
    public float SpinSpeed;   // radians / second

    public override string Kind => "shape";

    /// <summary>Which polygon this is, for the client's renderer.</summary>
    public abstract ShapeKind PolygonKind { get; }

    /// <summary>Score awarded to whoever lands the killing blow.</summary>
    public abstract int XpValue { get; }

    public override void Update(float dt) => Rotation += SpinSpeed * dt;

    public override EntityDto ToDto() => base.ToDto() with
    {
        Rot = Rotation,
        Shape = (int)PolygonKind,
    };
}

/// <summary>The common filler polygon: cheap, plentiful, low reward.</summary>
public sealed class SquareShape : Shape
{
    public override ShapeKind PolygonKind => ShapeKind.Square;
    public override int XpValue => 10;
}

/// <summary>Middle tier: tougher than a square and worth more than twice as much.</summary>
public sealed class TriangleShape : Shape
{
    public override ShapeKind PolygonKind => ShapeKind.Triangle;
    public override int XpValue => 25;
}

/// <summary>Rare, heavy polygon. Spins slowly and pays out a large bounty.</summary>
public sealed class PentagonShape : Shape
{
    public override ShapeKind PolygonKind => ShapeKind.Pentagon;
    public override int XpValue => 130;

    /// <summary>Pentagons are massive, so they drift at a fraction of the spin they are given.</summary>
    public override void Update(float dt) => Rotation += SpinSpeed * 0.5f * dt;
}
