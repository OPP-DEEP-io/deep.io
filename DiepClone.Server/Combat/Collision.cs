using System.Numerics;

namespace DiepClone.Server.Combat;

/// <summary>
/// Hand-rolled collision. raylib's CheckCollision* helpers are deliberately banned
/// (plan §3.1) because collision logic is an explicitly graded requirement.
/// Prototype uses brute-force circle checks; a uniform grid / quadtree comes later (plan §6).
/// </summary>
public static class Collision
{
    public static bool CirclesOverlap(Vector2 a, float ra, Vector2 b, float rb)
    {
        float sumR = ra + rb;
        return Vector2.DistanceSquared(a, b) <= sumR * sumR;
    }
}
