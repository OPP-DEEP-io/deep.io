using System.Numerics;
using DeepIo.Shared;

namespace DeepIo.Server.Ai;

/// <summary>Drive straight at the nearest enemy tank, stopping at gun range instead of ramming.</summary>
public sealed class ChaseNearestTargetStrategy : IMovementStrategy
{
    private const float StopDistance = 220f;

    public string Name => "Chase";

    public Vector2 ComputeVelocity(BotContext ctx)
    {
        if (ctx.NearestEnemy is null || ctx.EnemyDistance <= StopDistance) return Vector2.Zero;
        return Steering.Toward(ctx.Self.Position, ctx.NearestEnemy.Position);
    }
}

/// <summary>Run directly away from the nearest enemy, bending off the walls so the bot is not pinned in a corner.</summary>
public sealed class FleeWhenLowHpStrategy : IMovementStrategy
{
    private const float WallMargin = 350f;
    private const float WallWeight = 1.5f;

    public string Name => "Flee";

    public Vector2 ComputeVelocity(BotContext ctx)
    {
        Vector2 away = ctx.NearestEnemy is null
            ? Vector2.Zero
            : Steering.Toward(ctx.NearestEnemy.Position, ctx.Self.Position);

        return Steering.Normalized(away + Steering.WallRepulsion(ctx.Self.Position, WallMargin) * WallWeight);
    }
}

/// <summary>Circle the nearest enemy at a fixed range: the sniper's way of fighting.</summary>
public sealed class OrbitTargetStrategy : IMovementStrategy
{
    private const float PreferredRange = 420f;
    private const float WallMargin = 250f;

    private readonly float _turn;   // +1 counter-clockwise, -1 clockwise

    public OrbitTargetStrategy(Random rng) => _turn = rng.Next(2) == 0 ? 1f : -1f;

    public string Name => "Orbit";

    public Vector2 ComputeVelocity(BotContext ctx)
    {
        if (ctx.NearestEnemy is null) return Vector2.Zero;

        Vector2 radial = Steering.Toward(ctx.Self.Position, ctx.NearestEnemy.Position);
        Vector2 tangent = new Vector2(-radial.Y, radial.X) * _turn;
        float correction = Math.Clamp((ctx.EnemyDistance - PreferredRange) / PreferredRange, -1f, 1f);

        return Steering.Normalized(tangent + radial * (correction * 2f)
                                   + Steering.WallRepulsion(ctx.Self.Position, WallMargin));
    }
}

/// <summary>Drive up to the nearest polygon and hold at firing distance to farm XP.</summary>
public sealed class FarmShapesStrategy : IMovementStrategy
{
    private const float FiringDistance = 260f;

    public string Name => "Farm";

    public Vector2 ComputeVelocity(BotContext ctx)
    {
        if (ctx.NearestShape is null) return Vector2.Zero;
        if (ctx.ShapeDistance <= FiringDistance + ctx.NearestShape.Radius) return Vector2.Zero;
        return Steering.Toward(ctx.Self.Position, ctx.NearestShape.Position);
    }
}

/// <summary>Loop through fixed waypoints around the arena when there is nothing to do.</summary>
public sealed class PatrolWaypointsStrategy : IMovementStrategy
{
    private const float ArrivalRadius = 120f;

    private readonly Vector2[] _waypoints;
    private int _next;

    public PatrolWaypointsStrategy(Random rng)
    {
        float d = GameConstants.ArenaSize * 0.3f;
        _waypoints = [new(-d, -d), new(d, -d), new(d, d), new(-d, d)];
        _next = rng.Next(_waypoints.Length);
    }

    public string Name => "Patrol";

    public IReadOnlyList<Vector2> Waypoints => _waypoints;

    public Vector2 ComputeVelocity(BotContext ctx)
    {
        if (Vector2.Distance(ctx.Self.Position, _waypoints[_next]) < ArrivalRadius)
            _next = (_next + 1) % _waypoints.Length;

        return Steering.Toward(ctx.Self.Position, _waypoints[_next]);
    }
}
