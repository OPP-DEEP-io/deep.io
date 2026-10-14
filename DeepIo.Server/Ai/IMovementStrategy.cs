using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Ai;

/// <summary>
/// Strategy. One interchangeable way for a bot to decide where to drive. The
/// <see cref="BotController"/> (context) holds one slot and runs whatever fills it; the
/// <see cref="BotBrain"/> (client) decides which strategy goes into that slot at runtime.
/// The strategies know nothing about each other or about when they are chosen.
/// </summary>
public interface IMovementStrategy
{
    /// <summary>Short label shown under the bot on the client ("Chase", "Flee", ...).</summary>
    string Name { get; }

    /// <summary>Direction the bot wants to drive this think step; zero means hold position.</summary>
    Vector2 ComputeVelocity(BotContext ctx);
}

/// <summary>What a bot can see when it decides. Built fresh each think step by <see cref="BotBrain"/>.</summary>
public sealed class BotContext
{
    public const float EngageRange = 650f;
    public const float FarmRange = 900f;

    public required Tank Self { get; init; }
    public Tank? NearestEnemy { get; init; }
    public float EnemyDistance { get; init; } = float.PositiveInfinity;
    public Shape? NearestShape { get; init; }
    public float ShapeDistance { get; init; } = float.PositiveInfinity;

    public float HpFraction => Self.MaxHp > 0f ? Self.Hp / Self.MaxHp : 0f;

    public bool EnemyInRange => NearestEnemy is not null && EnemyDistance < EngageRange;

    public bool ShapeInRange => NearestShape is not null && ShapeDistance < FarmRange;

    /// <summary>Finds the nearest live enemy tank and the nearest live shape around <paramref name="self"/>.</summary>
    public static BotContext Observe(Tank self, IReadOnlyList<Tank> tanks, IReadOnlyList<Shape> shapes)
    {
        Tank? enemy = null;
        float enemyDist = float.PositiveInfinity;
        foreach (Tank t in tanks)
        {
            if (t == self || t.Dead) continue;
            float d = Vector2.Distance(self.Position, t.Position);
            if (d < enemyDist) (enemy, enemyDist) = (t, d);
        }

        Shape? shape = null;
        float shapeDist = float.PositiveInfinity;
        foreach (Shape s in shapes)
        {
            if (s.Dead) continue;
            float d = Vector2.Distance(self.Position, s.Position);
            if (d < shapeDist) (shape, shapeDist) = (s, d);
        }

        return new BotContext
        {
            Self = self,
            NearestEnemy = enemy,
            EnemyDistance = enemyDist,
            NearestShape = shape,
            ShapeDistance = shapeDist,
        };
    }
}

/// <summary>Vector helpers shared by the movement strategies.</summary>
internal static class Steering
{
    public static Vector2 Normalized(Vector2 v) =>
        v.LengthSquared() > 1e-4f ? Vector2.Normalize(v) : Vector2.Zero;

    public static Vector2 Toward(Vector2 from, Vector2 to) => Normalized(to - from);

    /// <summary>Inward push that grows from 0 at <paramref name="margin"/> away from a wall to 1 at the wall.</summary>
    public static Vector2 WallRepulsion(Vector2 p, float margin)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        Vector2 push = Vector2.Zero;

        if (p.X < -half + margin) push.X += 1f - (p.X + half) / margin;
        if (p.X > half - margin) push.X -= 1f - (half - p.X) / margin;
        if (p.Y < -half + margin) push.Y += 1f - (p.Y + half) / margin;
        if (p.Y > half - margin) push.Y -= 1f - (half - p.Y) / margin;

        return push;
    }
}
