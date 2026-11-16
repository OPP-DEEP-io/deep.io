using System.Numerics;
using DeepIo.Server.Commands;
using DeepIo.Server.Entities.Parts;
using DeepIo.Server.Entities.States;
using DeepIo.Server.Events;
using DeepIo.Server.Prototypes;
using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>
/// A tank: one per connected client, plus the server's bots.
///
/// A tank does not hard-code its stats: it is composed of the three parts produced together
/// by one concrete Abstract Factory (<c>ITankPartsFactory</c>), so speed, fire rate and
/// ballistics always come from the same consistent build. Skill points spent on
/// <see cref="Stats"/> scale those base values.
///
/// Player input and bot AI both reach a tank as commands (<see cref="MoveCommand"/>,
/// <see cref="RotateCommand"/>, <see cref="FireCommand"/>, <see cref="UpgradeStatCommand"/>)
/// run through its two <see cref="CommandHistory"/> invokers; the tank is their receiver.
///
/// The tank is also the context of the State pattern: whether it can move, fire, take damage
/// or regenerate depends on its current <see cref="TankState"/> (spawn shield, alive,
/// regenerating, dead), and the states themselves decide when to move on.
/// </summary>
public sealed class Tank : Entity, IPrototype<Tank>
{
    private const int InputHistorySize = 64;

    public required IChassis BaseChassis { get; init; }
    private IChassis? _chassis;
    public IChassis Chassis => _chassis ?? BaseChassis;
    public required IWeapon Weapon { get; init; }
    public required IProjectileSpec Round { get; init; }
    public required TankArchetype Archetype { get; init; }

    public string Name = "Player";
    public string ConnectionId = "";

    public float Aim;              // radians
    public bool Firing;
    public Vector2 MoveDir;        // normalised movement intent from the last MoveCommand

    public float ReloadTimer;      // counts down; can fire when <= 0
    public int Team;
    public int Score;
    public int LastInputSeq;

    /// <summary>Label of the movement strategy driving this tank if it is a bot; null for humans.</summary>
    public string? AiLabel;

    public int Level { get; private set; } = 1;
    public int SkillPoints { get; private set; }
    public TankStats Stats { get; private set; } = new();

    /// <summary>Recent move/aim/trigger commands (ring buffer).</summary>
    public CommandHistory InputHistory { get; private set; } = new(InputHistorySize);

    /// <summary>Stat upgrades, newest last. <c>UpgradeHistory.Undo()</c> is a one-point respec.</summary>
    public CommandHistory UpgradeHistory { get; private set; } = new(GameConstants.MaxLevel);

    /// <summary>Current life-cycle phase. Every tank starts behind a spawn shield.</summary>
    public TankState State { get; private set; } = new SpawnProtectedState();

    /// <summary>
    /// Switches to <paramref name="next"/> and announces it on the event bus. Called by the
    /// states themselves; nothing outside the tank chooses which state comes next.
    /// </summary>
    internal void TransitionTo(TankState next)
    {
        TankLifeState from = State.Kind;
        State = next;
        next.Enter(this);
        EventBus?.Notify(new TankStateChangedEvent(this, from, next.Kind));
    }

    /// <summary>
    /// Admin kill (console <c>kill</c>): destroys the tank even through its spawn shield.
    /// Reported like any other death, without a killer. False if it is already dead.
    /// </summary>
    public bool ForceKill()
    {
        if (State is DeadState) return false;

        Hp = 0f;
        EventBus?.Notify(new EntityDestroyedEvent(this, null));
        TransitionTo(new DeadState());
        return true;
    }

    protected override float FilterIncomingDamage(float amount) => State.FilterDamage(this, amount);

    protected override void OnKilled() => State.OnKilled(this);

    /// <summary>
    /// Shallow copies share mutable stats and histories (demonstration only). Deep copies
    /// own their stat array and fresh histories: commands hold references to their receiver
    /// and must not be copied to a new tank. They also start in a fresh spawn shield, because a
    /// state object carries its own timer. Immutable parts and the event bus are shared.
    /// Identity is preserved here; the spawn registry assigns a new world id.
    /// </summary>
    public Tank Clone(CopyDepth depth = CopyDepth.Deep)
    {
        if (depth is not (CopyDepth.Shallow or CopyDepth.Deep))
            throw new ArgumentOutOfRangeException(nameof(depth));
        var copy = (Tank)MemberwiseClone();
        if (depth == CopyDepth.Deep)
        {
            copy.Stats = Stats.DeepCopy();
            copy.InputHistory = new CommandHistory(InputHistory.Capacity);
            copy.UpgradeHistory = new CommandHistory(UpgradeHistory.Capacity);
            copy.State = new SpawnProtectedState();
        }
        return copy;
    }

    public override string Kind => "tank";

    public Vector2 AimVector => new(MathF.Cos(Aim), MathF.Sin(Aim));

    public float MoveSpeed => Chassis.Speed * Stats.MoveSpeedMultiplier;
    public float ReloadInterval => Weapon.ReloadInterval * Stats.ReloadMultiplier;
    public float BodyDamage => Stats.BodyDamagePerSecond;

    public override void Update(float dt)
    {
        Velocity = State.CanMove ? MoveDir * MoveSpeed : Vector2.Zero;
        base.Update(dt);
        Position = ClampToArena(Position, Radius);

        ReloadTimer -= dt;
        float regen = Chassis.RegenPerSecond * Stats.RegenMultiplier * State.RegenMultiplier;
        if (Hp < MaxHp && regen > 0f) Heal(regen * dt);

        State.Update(this, dt);
    }

    /// <summary>True when the state allows shooting, the trigger is held and the barrel has reloaded.</summary>
    public bool CanFire => State.CanFire && Firing && ReloadTimer <= 0f;

    /// <summary>Starts the reload for this tank's barrel. Call once per shot fired.</summary>
    public void BeginReload()
    {
        ReloadTimer = ReloadInterval;
        State.OnFired(this);
    }

    /// <summary>Level reached at <paramref name="score"/>: a square-root curve capped at MaxLevel.</summary>
    public static int LevelForScore(int score) =>
        Math.Min(GameConstants.MaxLevel, 1 + (int)MathF.Sqrt(Math.Max(0, score) / 15f));

    /// <summary>Adds XP; every level crossed grants one skill point.</summary>
    public void AddScore(int amount)
    {
        Score += amount;

        int reached = LevelForScore(Score);
        if (reached <= Level) return;

        SkillPoints += reached - Level;
        Level = reached;
        _chassis = ChassisUpgrades.ForLevel(BaseChassis, Level);
        ApplyStats();
    }

    /// <summary>
    /// Adds XP and, if that crossed a level threshold, publishes <see cref="PlayerLeveledUpEvent"/>
    /// so the feed and achievements hear about it. Used for kill bounties and console grants.
    /// </summary>
    public void AwardXp(int amount)
    {
        int before = Level;
        AddScore(amount);

        if (Level > before)
            EventBus?.Notify(new PlayerLeveledUpEvent(this, before, Level));
    }

    /// <summary>Receiver side of <see cref="UpgradeStatCommand.Execute"/>.</summary>
    /// <returns>False if there is no point to spend or the stat is maxed.</returns>
    public bool TrySpendPoint(StatKind stat)
    {
        if (SkillPoints <= 0 || !Stats.CanIncrease(stat)) return false;

        SkillPoints--;
        Stats.Increase(stat);
        ApplyStats();
        return true;
    }

    /// <summary>Receiver side of <see cref="UpgradeStatCommand.Undo"/>: takes the point back and refunds it.</summary>
    public void RefundPoint(StatKind stat)
    {
        Stats.Decrease(stat);
        SkillPoints++;
        ApplyStats();
    }

    /// <summary>
    /// Respawn: halve the score, drop to the level that score is worth, and hand every point
    /// back unspent (the build resets, as in diep.io).
    /// </summary>
    public void Respawn(Vector2 position)
    {
        Score = Math.Max(0, Score / 2);
        Level = LevelForScore(Score);
        SkillPoints = Level - 1;
        Stats.Reset();
        UpgradeHistory.Clear();
        InputHistory.Clear();

        _chassis = ChassisUpgrades.ForLevel(BaseChassis, Level);
        MaxHp = Chassis.MaxHp;
        Hp = MaxHp;
        Position = position;
        Velocity = Vector2.Zero;
        MoveDir = Vector2.Zero;
        Firing = false;
        ReloadTimer = 0f;
    }

    /// <summary>Re-derives stat-driven values, keeping the current HP fraction when max HP changes.</summary>
    private void ApplyStats()
    {
        float fraction = MaxHp > 0f ? Hp / MaxHp : 1f;
        MaxHp = Chassis.MaxHp * Stats.MaxHealthMultiplier;
        Hp = fraction * MaxHp;
    }

    public override EntityDto ToDto() => base.ToDto() with
    {
        Rot = Aim,
        Team = Team,
        Name = Name,
        Arch = (int)Archetype,
        Lvl = Level,
        Pts = SkillPoints,
        Up = Stats.ToArray(),
        Ai = AiLabel,
        Mods = Chassis is ChassisDecorator ? Chassis.Name : null,
        St = (int)State.Kind,
        StT = State.SecondsLeft,
    };
}
