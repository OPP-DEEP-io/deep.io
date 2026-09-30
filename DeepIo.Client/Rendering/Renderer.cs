using System.Numerics;
using DeepIo.Shared;

namespace DeepIo.Client.Rendering;

/// <summary>
/// Gameplay rendering abstraction in the Bridge. Draws snapshots through an independent
/// primitive backend; camera transforms and draw order remain here.
/// </summary>
public readonly record struct GameplayFrame(Snapshot? Snapshot, Camera Camera, int PlayerId);

public sealed class Renderer(IRenderBackend backend) : ClientView<GameplayFrame>(backend)
{
    private const float Rad2Deg = 57.29578f;

    private static readonly RenderColor Background = new(30, 30, 38, 255);
    private static readonly RenderColor Grid = new(45, 45, 56, 255);
    private static readonly RenderColor Border = new(70, 70, 88, 255);
    private static readonly RenderColor BulletColor = new(232, 172, 92, 255);
    private static readonly RenderColor BarrelColor = new(140, 140, 150, 255);
    private static readonly RenderColor SelfColor = new(90, 170, 240, 255);
    private static readonly RenderColor EnemyColor = new(230, 110, 110, 255);
    private static readonly RenderColor HpBack = new(60, 60, 60, 255);
    private static readonly RenderColor HpFront = new(90, 220, 120, 255);
    private static readonly RenderColor Faint = new(150, 150, 160, 255);
    private static readonly RenderColor Dim = new(110, 110, 122, 255);
    private static readonly RenderColor PipEmpty = new(60, 62, 74, 255);
    private static readonly RenderColor PipFull = new(120, 190, 250, 255);

    // Indexed by (int)StatKind; the number in front is the key that upgrades it.
    private static readonly string[] StatLabels =
    [
        "Health Regen", "Max Health", "Body Damage", "Bullet Speed",
        "Bullet Penetration", "Bullet Damage", "Reload", "Movement Speed",
    ];

    public override void Draw(GameplayFrame frame)
    {
        Snapshot? snap = frame.Snapshot;
        Camera cam = frame.Camera;
        int myId = frame.PlayerId;
        Backend.BeginFrame();
        Backend.Clear(Background);

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
            Backend.DrawText("Connecting to server...", 20, 60, 26, RenderColor.White);
        }

        DrawFps();
        Backend.EndFrame();
    }

    private void DrawGrid(Camera cam)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        const float step = 100f;

        for (float x = -half; x <= half + 0.5f; x += step)
        {
            var a = cam.WorldToScreen(new Vector2(x, -half));
            var b = cam.WorldToScreen(new Vector2(x, half));
            Backend.DrawLine(a, b, 1f, Grid);
        }
        for (float y = -half; y <= half + 0.5f; y += step)
        {
            var a = cam.WorldToScreen(new Vector2(-half, y));
            var b = cam.WorldToScreen(new Vector2(half, y));
            Backend.DrawLine(a, b, 1f, Grid);
        }
    }

    private void DrawBorder(Camera cam)
    {
        float half = GameConstants.ArenaSize * 0.5f;
        var tl = cam.WorldToScreen(new Vector2(-half, -half));
        var tr = cam.WorldToScreen(new Vector2(half, -half));
        var br = cam.WorldToScreen(new Vector2(half, half));
        var bl = cam.WorldToScreen(new Vector2(-half, half));
        Backend.DrawLine(tl, tr, 3f, Border);
        Backend.DrawLine(tr, br, 3f, Border);
        Backend.DrawLine(br, bl, 3f, Border);
        Backend.DrawLine(bl, tl, 3f, Border);
    }

    private void DrawShape(EntityDto e, Camera cam)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        (int sides, RenderColor color) = (ShapeKind)e.Shape switch
        {
            ShapeKind.Square => (4, new RenderColor(240, 210, 90, 255)),
            ShapeKind.Triangle => (3, new RenderColor(230, 120, 120, 255)),
            _ => (5, new RenderColor(120, 130, 230, 255)),
        };
        float radius = e.R * cam.Zoom;
        Backend.DrawPolygon(pos, sides, radius, e.Rot * Rad2Deg, color);
        DrawHpBar(pos, radius, e.Hp, e.MaxHp);
    }

    private void DrawBullet(EntityDto e, Camera cam)
    {
        var pos = cam.WorldToScreen(new Vector2(e.X, e.Y));
        Backend.DrawCircle(pos, e.R * cam.Zoom, BulletColor);
    }

    private void DrawTank(EntityDto e, Camera cam, int myId)
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
        Backend.DrawRotatedRectangle(pos, len, wid, e.Rot * Rad2Deg, BarrelColor);

        // Body.
        Backend.DrawCircle(pos, r, e.Id == myId ? SelfColor : EnemyColor);

        // Name and level above.
        if (!string.IsNullOrEmpty(e.Name))
        {
            string label = $"{e.Name}  Lv {e.Lvl}";
            int w = Backend.MeasureText(label, 18);
            Backend.DrawText(label, (int)(pos.X - w * 0.5f), (int)(pos.Y - r - 30f), 18, RenderColor.White);
        }

        // Bots: which movement strategy is driving them right now.
        if (!string.IsNullOrEmpty(e.Ai))
        {
            string ai = $"[{e.Ai}]";
            int w = Backend.MeasureText(ai, 15);
            Backend.DrawText(ai, (int)(pos.X - w * 0.5f), (int)(pos.Y + r + 16f), 15, Faint);
        }

        DrawHpBar(pos, r, e.Hp, e.MaxHp);
    }

    private void DrawHpBar(Vector2 pos, float radius, float hp, float maxHp)
    {
        if (maxHp <= 0f || hp >= maxHp) return;
        float frac = Math.Clamp(hp / maxHp, 0f, 1f);
        float y = pos.Y + radius + 8f;
        var left = new Vector2(pos.X - radius, y);
        var right = new Vector2(pos.X + radius, y);
        Backend.DrawLine(left, right, 4f, HpBack);
        Backend.DrawLine(left, new Vector2(left.X + (radius * 2f) * frac, y), 4f, HpFront);
    }

    private void DrawHud(Snapshot snap)
    {
        Backend.DrawText("WASD move   |   mouse aim   |   left click / space to fire   |   1-8 upgrade   |   Backspace undo upgrade", 20, 20, 19, Faint);
        Backend.DrawText($"tick {snap.Tick}", 20, 46, 17, Dim);

        int x = 20, y = 78;
        Backend.DrawText("Leaderboard", x, y, 22, RenderColor.White);
        y += 30;
        foreach (var entry in snap.Leaderboard)
        {
            Backend.DrawText($"{entry.Name}:  {entry.Score}", x, y, 19, RenderColor.White);
            y += 24;
        }
    }

    /// <summary>Kill feed (kills, level milestones, achievements), right-aligned under the FPS counter.</summary>
    private void DrawFeed(Snapshot snap)
    {
        const int fontSize = 19;
        int y = 52;
        foreach (string line in snap.Feed)
        {
            int x = Backend.ScreenWidth - Backend.MeasureText(line, fontSize) - 20;
            Backend.DrawText(line, x, y, fontSize, RenderColor.White);
            y += 26;
        }
    }

    /// <summary>Own level, unspent points and the eight stat bars, bottom-left.</summary>
    private void DrawUpgradePanel(Snapshot snap, int myId)
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

        const int rowH = 26;
        const float pipW = 16f, pipGap = 3f;
        int x = 20;
        int y = Backend.ScreenHeight - 20 - rowH * (StatLabels.Length + 1);

        string header = me.Pts > 0 ? $"Level {me.Lvl}   -   {me.Pts} point(s) to spend" : $"Level {me.Lvl}";
        Backend.DrawText(header, x, y, 22, me.Pts > 0 ? PipFull : RenderColor.White);
        y += rowH + 6;

        for (int i = 0; i < StatLabels.Length; i++)
        {
            int level = i < me.Up.Length ? me.Up[i] : 0;
            Backend.DrawText($"[{i + 1}] {StatLabels[i]}", x, y, 17, Faint);

            float px = x + 230f;
            float py = y + 9f;
            for (int p = 0; p < GameConstants.MaxStatLevel; p++)
            {
                var a = new Vector2(px + p * (pipW + pipGap), py);
                Backend.DrawLine(a, a with { X = a.X + pipW }, 9f, p < level ? PipFull : PipEmpty);
            }
            y += rowH;
        }
    }

    private void DrawFps()
    {
        const int fontSize = 19;
        string text = $"FPS: {Backend.Fps}";
        int x = Backend.ScreenWidth - Backend.MeasureText(text, fontSize) - 20;
        Backend.DrawText(text, x, 20, fontSize, Faint);
    }
}
