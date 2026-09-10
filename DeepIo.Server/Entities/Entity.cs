using System.Numerics;

namespace DeepIo.Server.Entities;

/// <summary>Base class for everything that lives in the arena.</summary>
public abstract class Entity
{
    public int Id { get; init; }
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Hp;
    public float MaxHp;

    public bool Dead => Hp <= 0f;
}
