using System.Numerics;
using Raylib_cs;

namespace DeepIo.Client.Rendering;

/// <summary>Raylib platform backend. All calls belong on window thread.</summary>
public sealed class RaylibRenderBackend : IRenderBackend
{
    private Font _uiFont;

    public int ScreenWidth => Raylib.GetScreenWidth();
    public int ScreenHeight => Raylib.GetScreenHeight();
    public int Fps => Raylib.GetFPS();
    public float FrameTime => Raylib.GetFrameTime();
    public double Time => Raylib.GetTime();
    public Vector2 MousePosition => Raylib.GetMousePosition();

    public void OpenWindow(int width, int height, string title, int targetFps)
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(width, height, title);
        Raylib.SetTargetFPS(targetFps);

        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "OpenSans-Regular.ttf");
        if (!File.Exists(fontPath))
            throw new FileNotFoundException("UI font asset is missing.", fontPath);

        // Include Latin, Greek, and Cyrillic text used in player names as well as UI labels.
        int[] codepoints = Enumerable.Range(32, 0x0530 - 32).ToArray();
        _uiFont = Raylib.LoadFontEx(fontPath, 48, codepoints, codepoints.Length);
        if (!Raylib.IsFontValid(_uiFont))
            throw new InvalidOperationException($"Could not load UI font: {fontPath}");
    }

    public bool WindowShouldClose() => Raylib.WindowShouldClose();
    public void CloseWindow()
    {
        Raylib.UnloadFont(_uiFont);
        Raylib.CloseWindow();
    }
    public bool IsKeyDown(ClientKey key) => Raylib.IsKeyDown(ToRaylib(key));
    public bool IsKeyPressed(ClientKey key) => Raylib.IsKeyPressed(ToRaylib(key));
    public bool IsKeyPressedRepeat(ClientKey key) => Raylib.IsKeyPressedRepeat(ToRaylib(key));
    public bool IsPrimaryMouseButtonDown() => Raylib.IsMouseButtonDown(MouseButton.Left);
    public bool IsPrimaryMouseButtonPressed() => Raylib.IsMouseButtonPressed(MouseButton.Left);
    public int GetCharPressed() => Raylib.GetCharPressed();

    public void BeginFrame() => Raylib.BeginDrawing();
    public void EndFrame() => Raylib.EndDrawing();
    public void Clear(RenderColor color) => Raylib.ClearBackground(ToRaylib(color));
    public void DrawLine(Vector2 start, Vector2 end, float thickness, RenderColor color) =>
        Raylib.DrawLineEx(start, end, thickness, ToRaylib(color));
    public void DrawCircle(Vector2 center, float radius, RenderColor color) =>
        Raylib.DrawCircleV(center, radius, ToRaylib(color));
    public void DrawPolygon(Vector2 center, int sides, float radius, float rotationDegrees, RenderColor color) =>
        Raylib.DrawPoly(center, sides, radius, rotationDegrees, ToRaylib(color));
    public void DrawRotatedRectangle(Vector2 position, float length, float width, float rotationDegrees, RenderColor color) =>
        Raylib.DrawRectanglePro(new Rectangle(position.X, position.Y, length, width),
            new Vector2(0f, width * 0.5f), rotationDegrees, ToRaylib(color));
    public void DrawText(string text, int x, int y, int fontSize, RenderColor color,
        RenderTextStyle style = RenderTextStyle.Ui)
    {
        if (style == RenderTextStyle.Title)
            Raylib.DrawText(text, x, y, fontSize, ToRaylib(color));
        else
            Raylib.DrawTextEx(_uiFont, text, new Vector2(x, y), fontSize, 0, ToRaylib(color));
    }

    public int MeasureText(string text, int fontSize, RenderTextStyle style = RenderTextStyle.Ui) =>
        style == RenderTextStyle.Title
            ? Raylib.MeasureText(text, fontSize)
            : (int)MathF.Ceiling(Raylib.MeasureTextEx(_uiFont, text, fontSize, 0).X);

    public void FillRectangle(RenderRect bounds, RenderColor color) =>
        Raylib.DrawRectangleRec(ToRaylib(bounds), ToRaylib(color));
    public void FillRoundedRectangle(RenderRect bounds, float roundness, int segments, RenderColor color) =>
        Raylib.DrawRectangleRounded(ToRaylib(bounds), roundness, segments, ToRaylib(color));
    public void OutlineRoundedRectangle(RenderRect bounds, float roundness, int segments, float thickness, RenderColor color) =>
        Raylib.DrawRectangleRoundedLinesEx(ToRaylib(bounds), roundness, segments, thickness, ToRaylib(color));

    private static Color ToRaylib(RenderColor color) => new(color.R, color.G, color.B, color.A);
    private static Rectangle ToRaylib(RenderRect bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static KeyboardKey ToRaylib(ClientKey key) => key switch
    {
        ClientKey.W => KeyboardKey.W,
        ClientKey.A => KeyboardKey.A,
        ClientKey.S => KeyboardKey.S,
        ClientKey.D => KeyboardKey.D,
        ClientKey.Space => KeyboardKey.Space,
        ClientKey.Left => KeyboardKey.Left,
        ClientKey.Right => KeyboardKey.Right,
        ClientKey.Up => KeyboardKey.Up,
        ClientKey.Down => KeyboardKey.Down,
        ClientKey.Home => KeyboardKey.Home,
        ClientKey.End => KeyboardKey.End,
        ClientKey.Tab => KeyboardKey.Tab,
        ClientKey.Backspace => KeyboardKey.Backspace,
        ClientKey.Delete => KeyboardKey.Delete,
        ClientKey.Enter => KeyboardKey.Enter,
        ClientKey.One => KeyboardKey.One,
        ClientKey.Two => KeyboardKey.Two,
        ClientKey.Three => KeyboardKey.Three,
        ClientKey.Four => KeyboardKey.Four,
        ClientKey.Five => KeyboardKey.Five,
        ClientKey.Six => KeyboardKey.Six,
        ClientKey.Seven => KeyboardKey.Seven,
        ClientKey.Eight => KeyboardKey.Eight,
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };
}
