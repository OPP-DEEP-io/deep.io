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

    // Per-tank and per-bullet stats are NOT global any more: they belong to the chassis,
    // weapon and projectile parts produced by the tank Abstract Factories (IChassis,
    // IWeapon, IProjectileSpec), and shape stats belong to the shape factories. The client
    // reads each entity's radius off the snapshot (EntityDto.R) instead of assuming one.

    // Shapes (passive XP pickups)
    public const int TargetShapeCount = 55;
}
