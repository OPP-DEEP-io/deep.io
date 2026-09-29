using System.Numerics;
using DeepIo.Server.Commands;
using DeepIo.Server.Entities.Parts;
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
/// </summary>
public sealed class Tank : Entity
{
    private const int InputHistorySize = 64;

    public required IChassis Chassis { get; init; }
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
    public TankStats Stats { get; } = new();

    /// <summary>Recent move/aim/trigger commands (ring buffer).</summary>
    public CommandHistory InputHistory { get; } = new(InputHistorySize);

    /// <summary>Stat upgrades, newest last. <c>UpgradeHistory.Undo()</c> is a one-point respec.</summary>
    public CommandHistory UpgradeHistory { get; } = new(GameConstants.MaxLevel);

    public override string Kind => "tank";

    public Vector2 AimVector => new(MathF.Cos(Aim), MathF.Sin(Aim));

    public float MoveSpeed => Chassis.Speed * Stats.MoveSpeedMultiplier;
    public float ReloadInterval => Weapon.ReloadInterval * Stats.ReloadMultiplier;
    public float BodyDamage => Stats.BodyDamagePerSecond;

    public override void Update(float dt)
    {
        Velocity = MoveDir * MoveSpeed;
        base.Update(dt);
        Position = ClampToArena(Position, Radius);

        ReloadTimer -= dt;
        if (Hp < MaxHp) Heal(Chassis.RegenPerSecond * Stats.RegenMultiplier * dt);
    }

    /// <summary>True when the trigger is held and the barrel has finished reloading.</summary>
    public bool CanFire => Firing && ReloadTimer <= 0f;

    /// <summary>Starts the reload for this tank's barrel. Call once per shot fired.</summary>
    public void BeginReload() => ReloadTimer = ReloadInterval;

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
    };
}
