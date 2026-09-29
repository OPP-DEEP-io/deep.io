using System.Numerics;
using DeepIo.Server.Commands;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Ai;

/// <summary>
/// Strategy context. Drives one bot tank: every think step it looks at the arena, picks the
/// <see cref="IMovementStrategy"/> that fits (HP, level gap, what is nearby), and turns the
/// result into the same commands a human's input becomes. Swapping strategies is one
/// assignment to <see cref="Current"/>; no movement logic lives here.
/// </summary>
public sealed class BotController
{
    public const float ThinkInterval = 0.05f;   // seconds between decisions

    private const float EngageRange = 650f;
    private const float FarmRange = 900f;
    private const float FleeHpFraction = 0.35f;
    private const int OutLevelledBy = 6;
    private const float AimError = 0.08f;       // radians, so bots are beatable

    private readonly Random _rng;
    private readonly IMovementStrategy _chase;
    private readonly IMovementStrategy _flee;
    private readonly IMovementStrategy _orbit;
    private readonly IMovementStrategy _farm;
    private readonly IMovementStrategy _patrol;
    private readonly StatKind[] _upgradeOrder;
    private float _thinkTimer;

    public BotController(Tank tank, Random rng)
    {
        Tank = tank;
        _rng = rng;
        _chase = new ChaseNearestTargetStrategy();
        _flee = new FleeWhenLowHpStrategy();
        _orbit = new OrbitTargetStrategy(rng);
        _farm = new FarmShapesStrategy();
        _patrol = new PatrolWaypointsStrategy(rng);
        _upgradeOrder = UpgradeOrderFor(tank.Archetype);
        Current = _patrol;
    }

    public Tank Tank { get; }

    public IMovementStrategy Current { get; private set; }

    public void Update(float dt, IReadOnlyList<Tank> tanks, IReadOnlyList<Shape> shapes)
    {
        if (Tank.Dead) return;

        _thinkTimer -= dt;
        if (_thinkTimer > 0f) return;
        _thinkTimer = ThinkInterval;

        BotContext ctx = Observe(tanks, shapes);

        Current = SelectStrategy(ctx);
        Tank.AiLabel = Current.Name;

        Tank.InputHistory.Execute(new MoveCommand(Tank, Current.ComputeVelocity(ctx)));
        AimAndFire(ctx);
        SpendSkillPoints();
    }

    /// <summary>The runtime swap: which strategy drives the bot is decided here and only here.</summary>
    private IMovementStrategy SelectStrategy(BotContext ctx)
    {
        if (ctx.NearestEnemy is { } enemy && ctx.EnemyDistance < EngageRange)
        {
            bool outmatched = enemy.Level >= Tank.Level + OutLevelledBy;
            if (ctx.HpFraction < FleeHpFraction || outmatched) return _flee;
            return Tank.Archetype == TankArchetype.Sniper ? _orbit : _chase;
        }

        if (ctx.NearestShape is not null && ctx.ShapeDistance < FarmRange) return _farm;

        return _patrol;
    }

    private BotContext Observe(IReadOnlyList<Tank> tanks, IReadOnlyList<Shape> shapes)
    {
        Tank? enemy = null;
        float enemyDist = float.PositiveInfinity;
        foreach (Tank t in tanks)
        {
            if (t == Tank || t.Dead) continue;
            float d = Vector2.Distance(Tank.Position, t.Position);
            if (d < enemyDist) (enemy, enemyDist) = (t, d);
        }

        Shape? shape = null;
        float shapeDist = float.PositiveInfinity;
        foreach (Shape s in shapes)
        {
            if (s.Dead) continue;
            float d = Vector2.Distance(Tank.Position, s.Position);
            if (d < shapeDist) (shape, shapeDist) = (s, d);
        }

        return new BotContext
        {
            Self = Tank,
            NearestEnemy = enemy,
            EnemyDistance = enemyDist,
            NearestShape = shape,
            ShapeDistance = shapeDist,
        };
    }

    /// <summary>Shoots at an enemy in range, otherwise at the nearest shape, with a little aim error.</summary>
    private void AimAndFire(BotContext ctx)
    {
        Entity? target =
            ctx.NearestEnemy is not null && ctx.EnemyDistance < EngageRange ? ctx.NearestEnemy
            : ctx.NearestShape is not null && ctx.ShapeDistance < FarmRange ? ctx.NearestShape
            : null;

        if (target is null)
        {
            Tank.InputHistory.Execute(new FireCommand(Tank, false));
            return;
        }

        Vector2 to = target.Position - Tank.Position;
        float aim = MathF.Atan2(to.Y, to.X) + (float)((_rng.NextDouble() * 2 - 1) * AimError);
        float range = Tank.Round.Speed * Tank.Stats.BulletSpeedMultiplier * Tank.Round.LifeSeconds;

        Tank.InputHistory.Execute(new RotateCommand(Tank, aim));
        Tank.InputHistory.Execute(new FireCommand(Tank, to.Length() <= range));
    }

    /// <summary>Bots level up like players and spend points through the same undoable command.</summary>
    private void SpendSkillPoints()
    {
        while (Tank.SkillPoints > 0)
        {
            int next = Array.FindIndex(_upgradeOrder, Tank.Stats.CanIncrease);
            if (next < 0) return;
            Tank.UpgradeHistory.Execute(new UpgradeStatCommand(Tank, _upgradeOrder[next]));
        }
    }

    private static StatKind[] UpgradeOrderFor(TankArchetype archetype) => archetype switch
    {
        TankArchetype.Sniper =>
        [
            StatKind.BulletDamage, StatKind.BulletSpeed, StatKind.Reload, StatKind.BulletPenetration,
            StatKind.MaxHealth, StatKind.HealthRegen, StatKind.MovementSpeed, StatKind.BodyDamage,
        ],
        TankArchetype.MachineGun =>
        [
            StatKind.Reload, StatKind.BulletDamage, StatKind.MovementSpeed, StatKind.BulletPenetration,
            StatKind.MaxHealth, StatKind.HealthRegen, StatKind.BulletSpeed, StatKind.BodyDamage,
        ],
        _ =>
        [
            StatKind.Reload, StatKind.BulletDamage, StatKind.BulletSpeed, StatKind.MaxHealth,
            StatKind.MovementSpeed, StatKind.HealthRegen, StatKind.BulletPenetration, StatKind.BodyDamage,
        ],
    };
}
