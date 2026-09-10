using System.Numerics;

namespace DiepClone.Server.Entities;

/// <summary>A player-controlled tank. One per connected client.</summary>
public sealed class Tank : Entity
{
    public string Name = "Player";
    public string ConnectionId = "";

    public float Aim;              // radians
    public bool Firing;
    public Vector2 MoveDir;        // normalised movement intent from last input

    public float ReloadTimer;      // counts down; can fire when <= 0
    public int Team;
    public int Score;
    public int LastInputSeq;
}
