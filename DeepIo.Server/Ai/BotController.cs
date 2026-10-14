using System.Numerics;
using DeepIo.Server.Commands;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Ai;

/// <summary>
/// Strategy context. Drives one bot tank through a single <see cref="IMovementStrategy"/> slot:
/// whoever owns the controller (the <see cref="BotBrain"/>) plugs a strategy in with
/// <see cref="SetStrategy"/>, and <see cref="Act"/> turns its answer into the same commands a
/// human's input becomes. The controller knows only the interface: it never creates a concrete
/// strategy and never decides which one to use.
/// </summary>
public sealed class BotController
{
    private const float AimError = 0.08f;       // radians, so bots are beatable

    private readonly Random _rng;
    private readonly StatKind[] _upgradeOrder;

    public BotController(Tank tank, IMovementStrategy initialStrategy, Random rng)
    {
        Tank = tank;
        _rng = rng;
        _upgradeOrder = UpgradeOrderFor(tank.Archetype);
        SetStrategy(initialStrategy);
    }

    public Tank Tank { get; }

    public IMovementStrategy Current { get; private set; } = null!;

    /// <summary>Swaps the movement algorithm at runtime; the next <see cref="Act"/> uses it.</summary>
    public void SetStrategy(IMovementStrategy strategy)
    {
        Current = strategy ?? throw new ArgumentNullException(nameof(strategy));
        Tank.AiLabel = strategy.Name;
    }

    /// <summary>One think step: move with the current strategy, then aim, fire and spend points.</summary>
    public void Act(BotContext ctx)
    {
        Tank.InputHistory.Execute(new MoveCommand(Tank, Current.ComputeVelocity(ctx)));
        AimAndFire(ctx);
        SpendSkillPoints();
    }

    /// <summary>Shoots at an enemy in range, otherwise at the nearest shape, with a little aim error.</summary>
    private void AimAndFire(BotContext ctx)
    {
        Entity? target =
            ctx.EnemyInRange ? ctx.NearestEnemy
            : ctx.ShapeInRange ? ctx.NearestShape
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
