using System.Numerics;
using DeepIo.Server.Entities;

namespace DeepIo.Server.Builders;

/// <summary>Construction steps shared by the player and bot builders. Only Build returns a tank.</summary>
public interface ITankBuilder
{
    ITankBuilder Start(int id, string connectionId, string name, Vector2 position);
    ITankBuilder AssembleBody();
    ITankBuilder AddWeapon();
    ITankBuilder LoadProjectile();
    Tank Build();
}
