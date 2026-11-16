using System.Numerics;
using DeepIo.Server.World;
using DeepIo.Shared;

namespace DeepIo.Server.Entities.States;

/// <summary>
/// Just (re)spawned: the tank can drive and shoot but takes no damage and bots ignore it. Ends
/// after <see cref="Seconds"/> or as soon as the tank fires, so the shield cannot be used to
/// attack safely.
/// </summary>
public sealed class SpawnProtectedState : TankState
{
    public const float Seconds = 3f;

    private float _left = Seconds;

    public override TankLifeState Kind => TankLifeState.SpawnProtected;

    public override bool IsTargetable => false;

    public override float SecondsLeft => _left;

    public override void Update(Tank tank, float dt)
    {
        _left -= dt;
        if (_left <= 0f) tank.TransitionTo(new AliveState());
    }

    public override float FilterDamage(Tank tank, float amount) => 0f;

    public override void OnFired(Tank tank) => tank.TransitionTo(new AliveState());
}

/// <summary>
/// Normal play. After <see cref="OutOfCombatSeconds"/> without taking a hit or firing, a damaged
/// tank switches to <see cref="RegeneratingState"/>.
/// </summary>
public sealed class AliveState : TankState
{
    public const float OutOfCombatSeconds = 5f;

    private float _quiet;

    public override TankLifeState Kind => TankLifeState.Alive;

    public override void Update(Tank tank, float dt)
    {
        _quiet += dt;
        if (_quiet >= OutOfCombatSeconds && tank.Hp < tank.MaxHp)
            tank.TransitionTo(new RegeneratingState());
    }

    public override float FilterDamage(Tank tank, float amount)
    {
        _quiet = 0f;
        return amount;
    }

    public override void OnFired(Tank tank) => _quiet = 0f;
}

/// <summary>
/// Out of combat: regeneration runs <see cref="Multiplier"/> times faster. Getting hit, firing
/// or reaching full HP returns the tank to <see cref="AliveState"/>.
/// </summary>
public sealed class RegeneratingState : TankState
{
    public const float Multiplier = 4f;

    public override TankLifeState Kind => TankLifeState.Regenerating;

    public override float RegenMultiplier => Multiplier;

    public override void Update(Tank tank, float dt)
    {
        if (tank.Hp >= tank.MaxHp) tank.TransitionTo(new AliveState());
    }

    public override float FilterDamage(Tank tank, float amount)
    {
        // Hand the hit to Alive first, so a killing blow is reported from there.
        tank.TransitionTo(new AliveState());
        return amount;
    }

    public override void OnFired(Tank tank) => tank.TransitionTo(new AliveState());
}

/// <summary>
/// Destroyed: the tank stays in the registry but cannot move, fire, heal or be hit. After
/// <see cref="Seconds"/> it respawns at a random point and gets a fresh spawn shield.
/// </summary>
public sealed class DeadState : TankState
{
    public const float Seconds = 3f;

    private float _left = Seconds;

    public override TankLifeState Kind => TankLifeState.Dead;

    public override bool CanMove => false;

    public override bool CanFire => false;

    public override bool IsTargetable => false;

    public override float RegenMultiplier => 0f;

    public override float SecondsLeft => _left;

    public override void Enter(Tank tank)
    {
        tank.Velocity = Vector2.Zero;
        tank.MoveDir = Vector2.Zero;
        tank.Firing = false;
    }

    public override void Update(Tank tank, float dt)
    {
        _left -= dt;
        if (_left > 0f) return;

        tank.Respawn(SpawnPoints.Random(Random.Shared));
        tank.TransitionTo(new SpawnProtectedState());
    }

    public override float FilterDamage(Tank tank, float amount) => 0f;

    public override void OnKilled(Tank tank) { }
}
