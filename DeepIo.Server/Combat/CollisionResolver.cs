using DeepIo.Server.Entities;

namespace DeepIo.Server.Combat;

/// <summary>
/// The per-tick damage pass: bullets against shapes and tanks, then body contact (ramming).
/// It only applies damage. Entities report hits and kills on their event bus, and what a kill
/// is worth (XP, leaderboard, achievements, kill feed) is decided by the attached observers.
/// </summary>
public sealed class CollisionResolver
{
    // Reused every tick to avoid re-allocating the typed views (brute force, fine for prototype counts).
    private readonly List<Bullet> _bullets = new();
    private readonly List<Shape> _shapes = new();
    private readonly List<Tank> _tanks = new();

    public void Resolve(IReadOnlyDictionary<int, Entity> entities, float dt)
    {
        Partition(entities.Values);

        foreach (Bullet bullet in _bullets)
            ResolveBullet(bullet, entities.GetValueOrDefault(bullet.OwnerId));

        ResolveRamming(dt);
    }

    private void Partition(IEnumerable<Entity> entities)
    {
        _bullets.Clear();
        _shapes.Clear();
        _tanks.Clear();

        foreach (Entity e in entities)
        {
            if (e is Bullet b) _bullets.Add(b);
            else if (e is Shape s) _shapes.Add(s);
            else if (e is Tank t) _tanks.Add(t);
        }
    }

    /// <summary>At most one hit per bullet per tick; a penetrating round carries on next tick.</summary>
    private void ResolveBullet(Bullet bullet, Entity? owner)
    {
        if (bullet.Dead) return;

        foreach (Shape shape in _shapes)
        {
            if (shape.Dead || bullet.HasHit(shape.Id)) continue;
            if (!Collision.CirclesOverlap(bullet.Position, bullet.Radius, shape.Position, shape.Radius))
                continue;

            shape.ApplyDamage(bullet.Damage, owner);
            bullet.RegisterHit(shape.Id);
            return;
        }

        foreach (Tank tank in _tanks)
        {
            if (tank.Id == bullet.OwnerId || tank.Dead || bullet.HasHit(tank.Id)) continue;
            if (!Collision.CirclesOverlap(bullet.Position, bullet.Radius, tank.Position, tank.Radius))
                continue;

            tank.ApplyDamage(bullet.Damage, owner);
            bullet.RegisterHit(tank.Id);
            return;
        }
    }

    /// <summary>Tanks with Body Damage upgrades hurt whatever they are touching.</summary>
    private void ResolveRamming(float dt)
    {
        foreach (Tank tank in _tanks)
        {
            float damage = tank.BodyDamage * dt;
            if (tank.Dead || damage <= 0f) continue;

            foreach (Shape shape in _shapes)
                if (!shape.Dead && Collision.CirclesOverlap(tank.Position, tank.Radius, shape.Position, shape.Radius))
                    shape.ApplyDamage(damage, tank);

            foreach (Tank other in _tanks)
                if (other != tank && !other.Dead && Collision.CirclesOverlap(tank.Position, tank.Radius, other.Position, other.Radius))
                    other.ApplyDamage(damage, tank);
        }
    }
}
