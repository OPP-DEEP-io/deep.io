using System.Numerics;
using DeepIo.Server.Entities;

namespace DeepIo.Server.Commands;

/// <summary>Sets the direction a tank drives in. Remembers the previous heading for Undo.</summary>
public sealed class MoveCommand : ICommand
{
    private readonly Tank _tank;
    private readonly Vector2 _direction;
    private Vector2 _previous;

    public MoveCommand(Tank tank, Vector2 direction)
    {
        _tank = tank;
        _direction = direction.LengthSquared() > 1e-4f ? Vector2.Normalize(direction) : direction;
    }

    public Vector2 Direction => _direction;

    public bool Execute()
    {
        _previous = _tank.MoveDir;
        _tank.MoveDir = _direction;
        return true;
    }

    public void Undo() => _tank.MoveDir = _previous;
}

/// <summary>Turns a tank's barrel to an aim angle (radians). Remembers the previous angle for Undo.</summary>
public sealed class RotateCommand : ICommand
{
    private readonly Tank _tank;
    private readonly float _aim;
    private float _previous;

    public RotateCommand(Tank tank, float aim)
    {
        _tank = tank;
        _aim = aim;
    }

    public float Aim => _aim;

    public bool Execute()
    {
        _previous = _tank.Aim;
        _tank.Aim = _aim;
        return true;
    }

    public void Undo() => _tank.Aim = _previous;
}

/// <summary>Holds or releases a tank's trigger. Remembers the previous trigger state for Undo.</summary>
public sealed class FireCommand : ICommand
{
    private readonly Tank _tank;
    private readonly bool _firing;
    private bool _previous;

    public FireCommand(Tank tank, bool firing)
    {
        _tank = tank;
        _firing = firing;
    }

    public bool Firing => _firing;

    public bool Execute()
    {
        _previous = _tank.Firing;
        _tank.Firing = _firing;
        return true;
    }

    public void Undo() => _tank.Firing = _previous;
}
