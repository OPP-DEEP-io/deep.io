using System.Numerics;
using DeepIo.Shared;

namespace DeepIo.Server.World;

/// <summary>Random spawn positions inside the arena, kept clear of the walls.</summary>
public static class SpawnPoints
{
    private const float WallMargin = 40f;

    public static Vector2 Random(Random rng)
    {
        float half = GameConstants.ArenaSize * 0.5f - WallMargin;
        return new Vector2(
            (float)(rng.NextDouble() * 2 - 1) * half,
            (float)(rng.NextDouble() * 2 - 1) * half);
    }
}
