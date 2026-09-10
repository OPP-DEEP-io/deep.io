using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>A passive polygon worth XP when destroyed.</summary>
public sealed class Shape : Entity
{
    public ShapeKind Kind;
    public float Rotation;    // radians, for visual spin
    public float SpinSpeed;   // radians / second
    public int XpValue;
}
