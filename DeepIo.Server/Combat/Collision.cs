using System.Numerics;

namespace DeepIo.Server.Combat;

/// <summary>
/// Hand-rolled collision. raylib's CheckCollision* helpers are deliberately banned
/// because collision logic is implemented here.
/// Prototype uses brute-force circle checks; a uniform grid / quadtree comes later.
/// </summary>
public static class Collision
{
    public static bool CirclesOverlap(Vector2 a, float ra, Vector2 b, float rb)
    {
        float sumR = ra + rb;
        return Vector2.DistanceSquared(a, b) <= sumR * sumR;
    }
}
