using System.Numerics;
using DeepIo.Server.Entities.Parts;
using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>
/// A player-controlled tank. One per connected client.
///
/// A tank does not hard-code its stats: it is composed of the three parts produced together
/// by one concrete Abstract Factory (<c>ITankPartsFactory</c>), so speed, fire rate and
/// ballistics always come from the same consistent build.
/// </summary>
public sealed class Tank : Entity
{
    public required IChassis Chassis { get; init; }
    public required IWeapon Weapon { get; init; }
    public required IProjectileSpec Round { get; init; }
    public required TankArchetype Archetype { get; init; }

    public string Name = "Player";
    public string ConnectionId = "";

    public float Aim;              // radians
    public bool Firing;
    public Vector2 MoveDir;        // normalised movement intent from last input

    public float ReloadTimer;      // counts down; can fire when <= 0
    public int Team;
    public int Score;
    public int LastInputSeq;

    public override string Kind => "tank";

    public Vector2 AimVector => new(MathF.Cos(Aim), MathF.Sin(Aim));

    public void ApplyInput(InputMessage input)
    {
        var dir = new Vector2(input.MoveX, input.MoveY);
        if (dir.LengthSquared() > 1e-4f) dir = Vector2.Normalize(dir);

        MoveDir = dir;
        Aim = input.Aim;
        Firing = input.Fire;
        LastInputSeq = input.Seq;
    }

    public override void Update(float dt)
    {
        Velocity = MoveDir * Chassis.Speed;
        base.Update(dt);
        Position = ClampToArena(Position, Radius);

        ReloadTimer -= dt;
        if (Hp < MaxHp) Heal(Chassis.RegenPerSecond * dt);
    }

    /// <summary>True when the trigger is held and the barrel has finished reloading.</summary>
    public bool CanFire => Firing && ReloadTimer <= 0f;

    /// <summary>Starts the reload for this tank's barrel. Call once per shot fired.</summary>
    public void BeginReload() => ReloadTimer = Weapon.ReloadInterval;

    public void AddScore(int amount) => Score += amount;

    /// <summary>Prototype respawn: full heal, halve score, teleport to a fresh spot.</summary>
    public void Respawn(Vector2 position)
    {
        Hp = MaxHp;
        Score = Math.Max(0, Score / 2);
        Position = position;
        Velocity = Vector2.Zero;
        MoveDir = Vector2.Zero;
        Firing = false;
        ReloadTimer = 0f;
    }

    public override EntityDto ToDto() => base.ToDto() with
    {
        Rot = Aim,
        Team = Team,
        Name = Name,
        Arch = (int)Archetype,
    };
}
