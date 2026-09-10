namespace DeepIo.Server.Entities;

/// <summary>A projectile fired by a tank.</summary>
public sealed class Bullet : Entity
{
    public int OwnerId;
    public float Damage;
    public float Life;   // seconds until it despawns
}
