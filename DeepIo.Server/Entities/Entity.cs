using System.Numerics;
using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>
/// Base class for everything that lives in the arena.
///
/// Subclasses own their per-tick behaviour (<see cref="Update"/>) and their wire format
/// (<see cref="ToDto"/>), so <c>GameWorld</c> never has to switch on the concrete type.
/// </summary>
public abstract class Entity
{
    public int Id { get; init; }
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Hp;
    public float MaxHp;

    public bool Dead => Hp <= 0f;

    /// <summary>Wire discriminator: "tank" | "shape" | "bullet".</summary>
    public abstract string Kind { get; }

    /// <summary>Advances this entity by <paramref name="dt"/> seconds. Default: straight-line motion.</summary>
    public virtual void Update(float dt) => Position += Velocity * dt;

    /// <summary>Applies damage and reports whether this hit was the killing blow.</summary>
    public bool ApplyDamage(float amount)
    {
        if (Dead) return false;
        Hp -= amount;
        return Dead;
    }

    public void Heal(float amount) => Hp = MathF.Min(MaxHp, Hp + amount);

    public void Kill() => Hp = 0f;

    /// <summary>Projects this entity into the flat snapshot DTO sent to clients.</summary>
    public virtual EntityDto ToDto() => new()
    {
        Id = Id,
        Kind = Kind,
        X = Position.X,
        Y = Position.Y,
        R = Radius,
        Hp = Hp,
        MaxHp = MaxHp,
    };

    protected static Vector2 ClampToArena(Vector2 p, float radius)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        p.X = Math.Clamp(p.X, -half + radius, half - radius);
        p.Y = Math.Clamp(p.Y, -half + radius, half - radius);
        return p;
    }
}
