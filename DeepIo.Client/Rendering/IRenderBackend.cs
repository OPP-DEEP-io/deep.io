using System.Numerics;

namespace DeepIo.Client.Rendering;

public readonly record struct RenderColor(byte R, byte G, byte B, byte A = 255)
{
    public static RenderColor White => new(245, 245, 245);
}

public readonly record struct RenderRect(float X, float Y, float Width, float Height)
{
    public bool Contains(Vector2 point) =>
        point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
}

public enum ClientKey
{
    W, A, S, D, Space, Left, Right, Up, Down, Home, End, Tab, Backspace, Delete, Enter,
    One, Two, Three, Four, Five, Six, Seven, Eight,   // stat upgrades
}

public enum RenderTextStyle { Ui, Title }

/// <summary>Client rendering, window, and input operations supplied by platform backend.</summary>
public interface IRenderBackend
{
    int ScreenWidth { get; }
    int ScreenHeight { get; }
    int Fps { get; }
    float FrameTime { get; }
    double Time { get; }
    Vector2 MousePosition { get; }

    void OpenWindow(int width, int height, string title, int targetFps);
    bool WindowShouldClose();
    void CloseWindow();

    bool IsKeyDown(ClientKey key);
    bool IsKeyPressed(ClientKey key);
    bool IsKeyPressedRepeat(ClientKey key);
    bool IsPrimaryMouseButtonDown();
    bool IsPrimaryMouseButtonPressed();
    int GetCharPressed();

    void BeginFrame();
    void EndFrame();
    void Clear(RenderColor color);
    void DrawLine(Vector2 start, Vector2 end, float thickness, RenderColor color);
    void DrawCircle(Vector2 center, float radius, RenderColor color);
    void DrawPolygon(Vector2 center, int sides, float radius, float rotationDegrees, RenderColor color);
    void DrawRotatedRectangle(Vector2 position, float length, float width, float rotationDegrees, RenderColor color);
    void DrawText(string text, int x, int y, int fontSize, RenderColor color, RenderTextStyle style = RenderTextStyle.Ui);
    int MeasureText(string text, int fontSize, RenderTextStyle style = RenderTextStyle.Ui);
    void FillRectangle(RenderRect bounds, RenderColor color);
    void FillRoundedRectangle(RenderRect bounds, float roundness, int segments, RenderColor color);
    void OutlineRoundedRectangle(RenderRect bounds, float roundness, int segments, float thickness, RenderColor color);
}
