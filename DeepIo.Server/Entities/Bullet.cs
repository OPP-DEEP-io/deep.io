using System.Numerics;
using DeepIo.Server.Entities.Parts;
using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>A projectile fired by a tank, stamped out of the owner's <see cref="IProjectileSpec"/>.</summary>
public sealed class Bullet : Entity
{
    public int OwnerId;
    public float Damage;
    public float Life;   // seconds until it despawns

    public override string Kind => "bullet";

    public bool Expired => Life <= 0f;

    /// <summary>
    /// Builds a round for <paramref name="owner"/> from the projectile spec its factory gave
    /// it. <paramref name="angle"/> already includes the barrel's spread.
    /// </summary>
    public static Bullet FromSpec(int id, Tank owner, float angle)
    {
        IProjectileSpec spec = owner.Round;
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

        return new Bullet
        {
            Id = id,
            OwnerId = owner.Id,
            Position = owner.Position + dir * (owner.Radius + spec.Radius + owner.Weapon.MuzzleGap),
            Velocity = dir * spec.Speed,
            Radius = spec.Radius,
            Hp = 1f,
            MaxHp = 1f,
            Damage = spec.Damage,
            Life = spec.LifeSeconds,
        };
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        Life -= dt;
    }

    public override EntityDto ToDto() => base.ToDto() with { Owner = OwnerId };
}
