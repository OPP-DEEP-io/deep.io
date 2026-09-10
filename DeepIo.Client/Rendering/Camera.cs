using System.Numerics;

namespace DeepIo.Client.Rendering;

/// <summary>
/// Hand-rolled world->screen transform. raylib's Camera2D / BeginMode2D / GetScreenToWorld2D
/// are deliberately not used — camera is implemented in this project.
/// assignment. ~15 lines of maths.
/// </summary>
public sealed class Camera
{
    public Vector2 Target;    // world point drawn at the centre of the screen
    public float Zoom = 1f;
    public int ScreenWidth;
    public int ScreenHeight;

    private Vector2 Center => new(ScreenWidth * 0.5f, ScreenHeight * 0.5f);

    public Vector2 WorldToScreen(Vector2 world) => (world - Target) * Zoom + Center;

    public Vector2 ScreenToWorld(Vector2 screen) => (screen - Center) / Zoom + Target;
}
