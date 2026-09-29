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
    private static readonly Color PipEmpty = new(60, 62, 74, 255);
    private static readonly Color PipFull = new(120, 190, 250, 255);

    // Indexed by (int)StatKind; the number in front is the key that upgrades it.
    private static readonly string[] StatLabels =
    [
        "Health Regen", "Max Health", "Body Damage", "Bullet Speed",
        "Bullet Penetration", "Bullet Damage", "Reload", "Movement Speed",
    ];

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
            DrawFeed(snap);
            DrawUpgradePanel(snap, myId);
        }
        else
        {
            Raylib.DrawText("Connecting to server...", 20, 60, 22, Color.RayWhite);
        }

        DrawFps();
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
        (int sides, Color color) = (ShapeKind)e.Shape switch
        {
            ShapeKind.Square => (4, new Color(240, 210, 90, 255)),
            ShapeKind.Triangle => (3, new Color(230, 120, 120, 255)),
            _ => (5, new Color(120, 130, 230, 255)),
        };
        float radius = e.R * cam.Zoom;
        Raylib.DrawPoly(pos, sides, radius, e.Rot * Rad2Deg, color);
        DrawHpBar(pos, radius, e.Hp, e.MaxHp);
    }

    private static void DrawBullet(EntityDto e, Camera cam)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        Raylib.DrawCircleV(pos, e.R * cam.Zoom, BulletColor);
    }

    private static void DrawTank(EntityDto e, Camera cam, int myId)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        float r = e.R * cam.Zoom;

        // Barrel: a rectangle rooted at the tank centre, rotated to the aim angle. Its shape
        // reflects the build the player picked, so enemies are readable at a glance.
        (float lenScale, float widScale) = (TankArchetype)e.Arch switch
        {
            TankArchetype.Sniper => (2.5f, 0.55f),
            TankArchetype.MachineGun => (1.4f, 0.95f),
            _ => (1.7f, 0.7f),
        };
        float len = r * lenScale;
        float wid = r * widScale;
        var rect = new Rectangle(pos.X, pos.Y, len, wid);
        var origin = new Vector2(0f, wid * 0.5f);
        Raylib.DrawRectanglePro(rect, origin, e.Rot * Rad2Deg, BarrelColor);

        // Body.
        Raylib.DrawCircleV(pos, r, e.Id == myId ? SelfColor : EnemyColor);

        // Name and level above.
        if (!string.IsNullOrEmpty(e.Name))
        {
            string label = $"{e.Name}  Lv {e.Lvl}";
            int w = Raylib.MeasureText(label, 14);
            Raylib.DrawText(label, (int)(pos.X - w * 0.5f), (int)(pos.Y - r - 26f), 14, Color.RayWhite);
        }

        // Bots: which movement strategy is driving them right now.
        if (!string.IsNullOrEmpty(e.Ai))
        {
            string ai = $"[{e.Ai}]";
            int w = Raylib.MeasureText(ai, 12);
            Raylib.DrawText(ai, (int)(pos.X - w * 0.5f), (int)(pos.Y + r + 16f), 12, Faint);
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
        Raylib.DrawText("WASD move   |   mouse aim   |   left click / space to fire   |   1-8 upgrade   |   Backspace undo upgrade", 20, 20, 16, Faint);
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

    /// <summary>Kill feed (kills, level milestones, achievements), right-aligned under the FPS counter.</summary>
    private static void DrawFeed(Snapshot snap)
    {
        const int fontSize = 16;
        int y = 48;
        foreach (string line in snap.Feed)
        {
            int x = Raylib.GetScreenWidth() - Raylib.MeasureText(line, fontSize) - 20;
            Raylib.DrawText(line, x, y, fontSize, Color.RayWhite);
            y += 22;
        }
    }

    /// <summary>Own level, unspent points and the eight stat bars, bottom-left.</summary>
    private static void DrawUpgradePanel(Snapshot snap, int myId)
    {
        EntityDto? me = null;
        foreach (var e in snap.Entities)
        {
            if (e.Id == myId && e.Kind == "tank")
            {
                me = e;
                break;
            }
        }
        if (me?.Up is null) return;

        const int rowH = 22;
        const float pipW = 14f, pipGap = 3f;
        int x = 20;
        int y = Raylib.GetScreenHeight() - 20 - rowH * (StatLabels.Length + 1);

        string header = me.Pts > 0 ? $"Level {me.Lvl}   -   {me.Pts} point(s) to spend" : $"Level {me.Lvl}";
        Raylib.DrawText(header, x, y, 18, me.Pts > 0 ? PipFull : Color.RayWhite);
        y += rowH + 4;

        for (int i = 0; i < StatLabels.Length; i++)
        {
            int level = i < me.Up.Length ? me.Up[i] : 0;
            Raylib.DrawText($"[{i + 1}] {StatLabels[i]}", x, y, 14, Faint);

            float px = x + 190f;
            float py = y + 7f;
            for (int p = 0; p < GameConstants.MaxStatLevel; p++)
            {
                var a = new Vector2(px + p * (pipW + pipGap), py);
                Raylib.DrawLineEx(a, a with { X = a.X + pipW }, 8f, p < level ? PipFull : PipEmpty);
            }
            y += rowH;
        }
    }

    private static void DrawFps()
    {
        const int fontSize = 16;
        string text = $"FPS: {Raylib.GetFPS()}";
        int x = Raylib.GetScreenWidth() - Raylib.MeasureText(text, fontSize) - 20;
        Raylib.DrawText(text, x, 20, fontSize, Faint);
    }
}
