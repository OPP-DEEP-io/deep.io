using System.Numerics;
using DeepIo.Shared;
using Raylib_cs;

namespace DeepIo.Client.Rendering;

/// <summary>
/// Draws a snapshot using only raylib's primitive-drawing API (the allowed subset in §3.1:
/// DrawCircleV, DrawPoly, DrawLineEx, DrawRectanglePro, DrawText, MeasureText). No textures,
/// no Camera2D, no collision helpers. In Part 1 this moves behind the Bridge implementor
/// (IRenderApi -> RaylibRenderApi) so raylib lives in exactly one file.
/// </summary>
public sealed class Renderer
{
    private const float Rad2Deg = 57.29578f;

    private static readonly Color Background = new(30, 30, 38, 255);
    private static readonly Color Grid = new(45, 45, 56, 255);
    private static readonly Color Border = new(70, 70, 88, 255);
    private static readonly Color BulletColor = new(232, 172, 92, 255);
    private static readonly Color BarrelColor = new(140, 140, 150, 255);
    private static readonly Color SelfColor = new(90, 170, 240, 255);
    private static readonly Color EnemyColor = new(230, 110, 110, 255);
    private static readonly Color HpBack = new(60, 60, 60, 255);
    private static readonly Color HpFront = new(90, 220, 120, 255);
    private static readonly Color Faint = new(150, 150, 160, 255);
    private static readonly Color Dim = new(110, 110, 122, 255);

    public void Draw(Snapshot? snap, Camera cam, int myId)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Background);

        DrawGrid(cam);
        DrawBorder(cam);

        if (snap is not null)
        {
            // Draw order: shapes, then bullets, then tanks on top.
            foreach (var e in snap.Entities) if (e.Kind == "shape") DrawShape(e, cam);
            foreach (var e in snap.Entities) if (e.Kind == "bullet") DrawBullet(e, cam);
            foreach (var e in snap.Entities) if (e.Kind == "tank") DrawTank(e, cam, myId);

            DrawHud(snap);
        }
        else
        {
            Raylib.DrawText("Connecting to server...", 20, 60, 22, Color.RayWhite);
        }

        Raylib.EndDrawing();
    }

    private static void DrawGrid(Camera cam)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        const float step = 100f;

        for (float x = -half; x <= half + 0.5f; x += step)
        {
            var a = cam.WorldToScreen(new Vector2(x, -half));
            var b = cam.WorldToScreen(new Vector2(x, half));
            Raylib.DrawLineEx(a, b, 1f, Grid);
        }
        for (float y = -half; y <= half + 0.5f; y += step)
        {
            var a = cam.WorldToScreen(new Vector2(-half, y));
            var b = cam.WorldToScreen(new Vector2(half, y));
            Raylib.DrawLineEx(a, b, 1f, Grid);
        }
    }

    private static void DrawBorder(Camera cam)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        var tl = cam.WorldToScreen(new Vector2(-half, -half));
        var tr = cam.WorldToScreen(new Vector2(half, -half));
        var br = cam.WorldToScreen(new Vector2(half, half));
        var bl = cam.WorldToScreen(new Vector2(-half, half));
        Raylib.DrawLineEx(tl, tr, 3f, Border);
        Raylib.DrawLineEx(tr, br, 3f, Border);
        Raylib.DrawLineEx(br, bl, 3f, Border);
        Raylib.DrawLineEx(bl, tl, 3f, Border);
    }

    private static void DrawShape(EntityDto e, Camera cam)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        (int sides, float radius, Color color) = (ShapeKind)e.Shape switch
        {
            ShapeKind.Square => (4, 18f, new Color(240, 210, 90, 255)),
            ShapeKind.Triangle => (3, 24f, new Color(230, 120, 120, 255)),
            _ => (5, 34f, new Color(120, 130, 230, 255)),
        };
        Raylib.DrawPoly(pos, sides, radius * cam.Zoom, e.Rot * Rad2Deg, color);
        DrawHpBar(pos, radius * cam.Zoom, e.Hp, e.MaxHp);
    }

    private static void DrawBullet(EntityDto e, Camera cam)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        Raylib.DrawCircleV(pos, GameConstants.BulletRadius * cam.Zoom, BulletColor);
    }

    private static void DrawTank(EntityDto e, Camera cam, int myId)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        float r = GameConstants.TankRadius * cam.Zoom;

        // Barrel: a rectangle rooted at the tank centre, rotated to the aim angle.
        float len = r * 1.7f;
        float wid = r * 0.7f;
        var rect = new Rectangle(pos.X, pos.Y, len, wid);
        var origin = new Vector2(0f, wid * 0.5f);
        Raylib.DrawRectanglePro(rect, origin, e.Rot * Rad2Deg, BarrelColor);

        // Body.
        Raylib.DrawCircleV(pos, r, e.Id == myId ? SelfColor : EnemyColor);

        // Name above.
        if (!string.IsNullOrEmpty(e.Name))
        {
            int w = Raylib.MeasureText(e.Name, 14);
            Raylib.DrawText(e.Name, (int)(pos.X - w * 0.5f), (int)(pos.Y - r - 26f), 14, Color.RayWhite);
        }

        DrawHpBar(pos, r, e.Hp, e.MaxHp);
    }

    private static void DrawHpBar(Vector2 pos, float radius, float hp, float maxHp)
    {
        if (maxHp <= 0f || hp >= maxHp) return;
        float frac = Math.Clamp(hp / maxHp, 0f, 1f);
        float y = pos.Y + radius + 8f;
        var left = new Vector2(pos.X - radius, y);
        var right = new Vector2(pos.X + radius, y);
        Raylib.DrawLineEx(left, right, 4f, HpBack);
        Raylib.DrawLineEx(left, new Vector2(left.X + (radius * 2f) * frac, y), 4f, HpFront);
    }

    private static void DrawHud(Snapshot snap)
    {
        Raylib.DrawText("WASD move   |   mouse aim   |   left click / space to fire", 20, 20, 16, Faint);
        Raylib.DrawText($"tick {snap.Tick}", 20, 42, 14, Dim);

        int x = 20, y = 72;
        Raylib.DrawText("Leaderboard", x, y, 18, Color.RayWhite);
        y += 26;
        foreach (var entry in snap.Leaderboard)
        {
            Raylib.DrawText($"{entry.Name}:  {entry.Score}", x, y, 16, Color.RayWhite);
            y += 20;
        }
    }
}
