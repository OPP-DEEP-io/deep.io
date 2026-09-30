using System.Numerics;

namespace DeepIo.Client.Rendering;

/// <summary>
/// World-to-screen transform shared by both rendering backends.
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
