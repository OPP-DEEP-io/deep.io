using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Ai;

/// <summary>
/// Strategy client. Owns one bot's concrete <see cref="IMovementStrategy"/> objects and the rules
/// for when each one applies (HP, level gap, what is nearby). Every think step it observes the
/// arena, hands the chosen strategy to the <see cref="BotController"/> through
/// <see cref="BotController.SetStrategy"/>, and lets the controller act. Adding a strategy means
/// touching this class only; the controller never learns the concrete types.
/// </summary>
public sealed class BotBrain
{
    public const float ThinkInterval = 0.05f;   // seconds between decisions

    private const float FleeHpFraction = 0.35f;
    private const int OutLevelledBy = 6;

    private readonly BotController _controller;
    private readonly IMovementStrategy _chase;
    private readonly IMovementStrategy _flee;
    private readonly IMovementStrategy _orbit;
    private readonly IMovementStrategy _farm;
    private readonly IMovementStrategy _patrol;
    private float _thinkTimer;

    public BotBrain(Tank tank, Random rng)
    {
        // One set per bot: Orbit and Patrol keep per-bot state (turn direction, next waypoint).
        _chase = new ChaseNearestTargetStrategy();
        _flee = new FleeWhenLowHpStrategy();
        _orbit = new OrbitTargetStrategy(rng);
        _farm = new FarmShapesStrategy();
        _patrol = new PatrolWaypointsStrategy(rng);
        _controller = new BotController(tank, _patrol, rng);
    }

    public Tank Tank => _controller.Tank;

    public void Update(float dt, IReadOnlyList<Tank> tanks, IReadOnlyList<Shape> shapes)
    {
        if (Tank.Dead) return;

        _thinkTimer -= dt;
        if (_thinkTimer > 0f) return;
        _thinkTimer = ThinkInterval;

        BotContext ctx = BotContext.Observe(Tank, tanks, shapes);

        _controller.SetStrategy(SelectStrategy(ctx));
        _controller.Act(ctx);
    }

    /// <summary>The runtime swap: which strategy drives the bot is decided here and only here.</summary>
    private IMovementStrategy SelectStrategy(BotContext ctx)
    {
        if (ctx.EnemyInRange)
        {
            bool outmatched = ctx.NearestEnemy!.Level >= Tank.Level + OutLevelledBy;
            if (ctx.HpFraction < FleeHpFraction || outmatched) return _flee;
            return Tank.Archetype == TankArchetype.Sniper ? _orbit : _chase;
        }

        if (ctx.ShapeInRange) return _farm;

        return _patrol;
    }
}
