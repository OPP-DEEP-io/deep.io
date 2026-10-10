using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Server.Entities.Parts;
using DeepIo.Server.Factories;
using DeepIo.Shared;

namespace DeepIo.Server.Builders;

public abstract class TankBuilder(ITankPartsFactory parts) : ITankBuilder
{
    private int _id;
    private string _connectionId = "";
    private string _name = "";
    private Vector2 _position;
    private IChassis? _chassis;
    private IWeapon? _weapon;
    private IProjectileSpec? _round;
    private bool _started;

    protected abstract string DefaultName { get; }
    protected abstract void ConfigureTank(Tank tank);

    public ITankBuilder Start(int id, string connectionId, string name, Vector2 position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 0);
        ArgumentNullException.ThrowIfNull(connectionId);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new ArgumentException("Spawn position must be finite.", nameof(position));

        _id = id;
        _connectionId = connectionId;
        _name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
        _position = position;
        _chassis = null;
        _weapon = null;
        _round = null;
        _started = true;
        return this;
    }

    public ITankBuilder AssembleBody()
    {
        RequireStarted();
        _chassis = parts.CreateChassis();
        return this;
    }

    public ITankBuilder AddWeapon()
    {
        RequireStarted();
        _weapon = parts.CreateWeapon();
        return this;
    }

    public ITankBuilder LoadProjectile()
    {
        RequireStarted();
        _round = parts.CreateProjectile();
        return this;
    }

    public Tank Build()
    {
        RequireStarted();
        if (_chassis is null || _weapon is null || _round is null)
            throw new InvalidOperationException("A tank requires a body, weapon, and projectile before Build.");

        var tank = new Tank
        {
            Id = _id,
            BaseChassis = _chassis,
            Weapon = _weapon,
            Round = _round,
            Archetype = parts.Archetype,
            Name = _name,
            ConnectionId = _connectionId,
            Position = _position,
            Radius = _chassis.Radius,
            Hp = _chassis.MaxHp,
            MaxHp = _chassis.MaxHp,
        };
        ConfigureTank(tank);
        _started = false;
        return tank;
    }

    private void RequireStarted()
    {
        if (!_started) throw new InvalidOperationException("Call Start before constructing another tank.");
    }
}

public sealed class PlayerTankBuilder(ITankPartsFactory parts) : TankBuilder(parts)
{
    protected override string DefaultName => "Player";
    protected override void ConfigureTank(Tank tank) { }
}

public sealed class BotTankBuilder(ITankPartsFactory parts) : TankBuilder(parts)
{
    protected override string DefaultName => "Bot";

    protected override void ConfigureTank(Tank tank)
    {
        tank.AddScore(60);
        tank.TrySpendPoint(StatKind.BulletDamage);
        tank.TrySpendPoint(StatKind.Reload);
    }
}
