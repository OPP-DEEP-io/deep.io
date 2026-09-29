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

    // Targets this round has already hit, so a penetrating round does not hit the same one
    // again on the next tick while it is still overlapping.
    private HashSet<int>? _hitIds;

    /// <summary>
    /// Builds a round for <paramref name="owner"/> from the projectile spec its factory gave
    /// it, scaled by the owner's bullet upgrades. <paramref name="angle"/> already includes
    /// the barrel's spread. Hp counts how many targets the round can still hit.
    /// </summary>
    public static Bullet FromSpec(int id, Tank owner, float angle)
    {
        IProjectileSpec spec = owner.Round;
        TankStats stats = owner.Stats;
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        float hits = 1f + stats.BulletPenetration;

        return new Bullet
        {
            Id = id,
            OwnerId = owner.Id,
            Position = owner.Position + dir * (owner.Radius + spec.Radius + owner.Weapon.MuzzleGap),
            Velocity = dir * (spec.Speed * stats.BulletSpeedMultiplier),
            Radius = spec.Radius,
            Hp = hits,
            MaxHp = hits,
            Damage = spec.Damage * stats.BulletDamageMultiplier,
            Life = spec.LifeSeconds,
        };
    }

    public bool HasHit(int targetId) => _hitIds?.Contains(targetId) == true;

    /// <summary>Uses up one hit. A round with no penetration upgrades is spent after one.</summary>
    public void RegisterHit(int targetId)
    {
        (_hitIds ??= new HashSet<int>()).Add(targetId);
        Hp -= 1f;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        Life -= dt;
    }

    public override EntityDto ToDto() => base.ToDto() with { Owner = OwnerId };
}
