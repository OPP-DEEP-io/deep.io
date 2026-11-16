using DeepIo.Shared;

namespace DeepIo.Server.Entities.States;

/// <summary>
/// State. One phase of a tank's life cycle. The <see cref="Tank"/> (context) delegates every
/// behaviour that depends on the phase to its current state: whether it may move or fire, how
/// much incoming damage lands, how fast it regenerates and what happens when it dies.
///
/// Unlike the bots' movement strategies, nobody outside picks the state: each concrete state
/// decides on its own when the tank moves on and calls <see cref="Tank.TransitionTo"/> with its
/// successor. States carry their own timers, so every transition creates a fresh instance and a
/// state object is never shared between tanks.
/// </summary>
public abstract class TankState
{
    /// <summary>Wire value of this state (<c>EntityDto.St</c>).</summary>
    public abstract TankLifeState Kind { get; }

    public virtual bool CanMove => true;

    public virtual bool CanFire => true;

    /// <summary>False when bots should not bother aiming at this tank.</summary>
    public virtual bool IsTargetable => true;

    /// <summary>Scales the chassis regeneration rate.</summary>
    public virtual float RegenMultiplier => 1f;

    /// <summary>Seconds left in a timed state (spawn shield, respawn countdown); 0 when untimed.</summary>
    public virtual float SecondsLeft => 0f;

    /// <summary>Runs once when the tank switches into this state.</summary>
    public virtual void Enter(Tank tank) { }

    /// <summary>Advances the state's own clock and fires timed transitions.</summary>
    public virtual void Update(Tank tank, float dt) { }

    /// <summary>How much of an incoming hit actually lands. May also trigger a transition.</summary>
    public virtual float FilterDamage(Tank tank, float amount) => amount;

    /// <summary>The tank just fired a shot.</summary>
    public virtual void OnFired(Tank tank) { }

    /// <summary>The last hit brought HP to zero.</summary>
    public virtual void OnKilled(Tank tank) => tank.TransitionTo(new DeadState());

    public override string ToString() => Kind.ToString();
}
