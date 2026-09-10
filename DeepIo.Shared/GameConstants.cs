namespace DeepIo.Shared;

/// <summary>
/// Tunable simulation constants shared by server (authoritative) and client (rendering).
/// Keeping them here means both ends agree on arena size, tick rate, and entity sizes.
/// </summary>
public static class GameConstants
{
    // Arena is a square centred on the origin: coordinates run from -ArenaSize/2 to +ArenaSize/2.
    public const float ArenaSize = 3000f;

    public const int TickRate = 128;               // authoritative server ticks per second
    public const float TickDelta = 1f / TickRate; // seconds per tick

    // Tank
    public const float TankRadius = 22f;
    public const float TankSpeed = 260f;          // units / second
    public const float TankMaxHp = 100f;
    public const float TankRegenPerSec = 3f;

    // Bullet
    public const float BulletRadius = 7f;
    public const float BulletSpeed = 520f;
    public const float BulletDamage = 12f;
    public const float BulletLife = 1.6f;         // seconds before despawn
    public const float ReloadInterval = 0.35f;    // seconds between shots

    // Shapes (passive XP pickups)
    public const int TargetShapeCount = 55;
}
