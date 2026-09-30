using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using SDL3;

namespace DeepIo.Client.Rendering;

/// <summary>SDL3 implementation of the same window, input, and drawing contract as raylib.</summary>
public sealed class Sdl3RenderBackend : IRenderBackend
{
    private readonly Dictionary<int, IntPtr> _fonts = new();
    private readonly Dictionary<(string Text, int Size, RenderColor Color), CachedText> _textCache = new();
    private readonly HashSet<SDL.Scancode> _pressed = new();
    private readonly HashSet<SDL.Scancode> _repeated = new();
    private readonly Queue<int> _characters = new();
    private IntPtr _window;
    private IntPtr _renderer;
    private bool _initialized;
    private bool _ttfInitialized;
    private bool _closeRequested;
    private bool _mousePressed;
    private int _targetFps;
    private int _frameNumber;
    private ulong _lastFrameTicks;
    private float _frameTime = 1f / 60f;
    private string _fontPath = "";

    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }
    public int Fps => _frameTime > 0 ? (int)MathF.Round(1f / _frameTime) : 0;
    public float FrameTime => _frameTime;
    public double Time => SDL.GetTicks() / 1000.0;
    public Vector2 MousePosition
    {
        get
        {
            SDL.GetMouseState(out float x, out float y);
            return new Vector2(x, y);
        }
    }

    public void OpenWindow(int width, int height, string title, int targetFps)
    {
        _fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "OpenSans-Regular.ttf");
        if (!File.Exists(_fontPath))
            throw new FileNotFoundException("UI font asset is missing.", _fontPath);

        _targetFps = targetFps;
        ScreenWidth = width;
        ScreenHeight = height;
        try
        {
            if (!SDL.Init(SDL.InitFlags.Video))
                throw new InvalidOperationException($"SDL3 initialization failed: {SDL.GetError()}");
            _initialized = true;
            _window = SDL.CreateWindow(title, width, height, 0);
            if (_window == IntPtr.Zero)
                throw new InvalidOperationException($"SDL3 window creation failed: {SDL.GetError()}");
            string driver = Environment.GetEnvironmentVariable("SDL_RENDER_DRIVER") ?? "software";
            _renderer = SDL.CreateRenderer(_window, driver);
            if (_renderer == IntPtr.Zero)
                throw new InvalidOperationException($"SDL3 renderer creation failed: {SDL.GetError()}");
            if (!TTF.Init())
                throw new InvalidOperationException($"SDL3_ttf initialization failed: {SDL.GetError()}");
            _ttfInitialized = true;
            if (!SDL.StartTextInput(_window))
                throw new InvalidOperationException($"SDL3 text input failed: {SDL.GetError()}");
            SDL.SetRenderDrawBlendMode(_renderer, SDL.BlendMode.Blend);
            _lastFrameTicks = SDL.GetTicks();
        }
        catch
        {
            CloseWindow();
            throw;
        }
    }

    public bool WindowShouldClose()
    {
        _pressed.Clear();
        _repeated.Clear();
        _characters.Clear();
        _mousePressed = false;
        while (SDL.PollEvent(out SDL.Event e))
        {
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.Quit:
                case SDL.EventType.WindowCloseRequested:
                    _closeRequested = true;
                    break;
                case SDL.EventType.KeyDown:
                    if (e.Key.Repeat) _repeated.Add(e.Key.Scancode);
                    else _pressed.Add(e.Key.Scancode);
                    break;
                case SDL.EventType.MouseButtonDown:
                    if (e.Button.Button == 1) _mousePressed = true;
                    break;
                case SDL.EventType.TextInput:
                    string? text = Marshal.PtrToStringUTF8(e.Text.Text);
                    if (text is not null)
                        foreach (Rune rune in text.EnumerateRunes())
                            _characters.Enqueue(rune.Value);
                    break;
            }
        }

        ulong ticks = SDL.GetTicks();
        _frameTime = Math.Max((ticks - _lastFrameTicks) / 1000f, 0.001f);
        _lastFrameTicks = ticks;
        return _closeRequested;
    }

    public void CloseWindow()
    {
        foreach (CachedText cached in _textCache.Values)
            SDL.DestroyTexture(cached.Texture);
        _textCache.Clear();
        foreach (IntPtr font in _fonts.Values)
            TTF.CloseFont(font);
        _fonts.Clear();
        if (_ttfInitialized) TTF.Quit();
        if (_renderer != IntPtr.Zero) SDL.DestroyRenderer(_renderer);
        if (_window != IntPtr.Zero) SDL.DestroyWindow(_window);
        if (_initialized) SDL.Quit();
        _ttfInitialized = false;
        _initialized = false;
        _renderer = IntPtr.Zero;
        _window = IntPtr.Zero;
    }

    public bool IsKeyDown(ClientKey key)
    {
        ReadOnlySpan<bool> keys = SDL.GetKeyboardState(out int count);
        int index = (int)ToSdl(key);
        return index < count && keys[index];
    }

    public bool IsKeyPressed(ClientKey key) => _pressed.Contains(ToSdl(key));
    public bool IsKeyPressedRepeat(ClientKey key) => _repeated.Contains(ToSdl(key));
    public bool IsPrimaryMouseButtonDown() => (SDL.GetMouseState(out _, out _) & SDL.MouseButtonFlags.Left) != 0;
    public bool IsPrimaryMouseButtonPressed() => _mousePressed;
    public int GetCharPressed() => _characters.TryDequeue(out int value) ? value : 0;

    public void BeginFrame() { }

    public void EndFrame()
    {
        SDL.RenderPresent(_renderer);
        _frameNumber++;
        if (_textCache.Count > 256)
        {
            foreach (var key in _textCache.Where(entry => entry.Value.LastUsedFrame < _frameNumber - 30)
                         .Select(entry => entry.Key).ToArray())
            {
                SDL.DestroyTexture(_textCache[key].Texture);
                _textCache.Remove(key);
            }
        }

        if (_targetFps > 0)
        {
            ulong elapsed = SDL.GetTicks() - _lastFrameTicks;
            uint frameMs = (uint)(1000 / _targetFps);
            if (elapsed < frameMs) SDL.Delay(frameMs - (uint)elapsed);
        }
    }

    public void Clear(RenderColor color)
    {
        SetColor(color);
        SDL.RenderClear(_renderer);
    }

    public void DrawLine(Vector2 start, Vector2 end, float thickness, RenderColor color)
    {
        if (thickness <= 1f)
        {
            SetColor(color);
            SDL.RenderLine(_renderer, start.X, start.Y, end.X, end.Y);
            return;
        }

        Vector2 direction = end - start;
        if (direction.LengthSquared() < 0.001f) return;
        Vector2 offset = Vector2.Normalize(new Vector2(-direction.Y, direction.X)) * (thickness * 0.5f);
        FillPolygon([start + offset, end + offset, end - offset, start - offset], color);
    }

    public void DrawCircle(Vector2 center, float radius, RenderColor color)
    {
        if (radius <= 0) return;
        const int segments = 40;
        var points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * MathF.Tau / segments;
            points[i] = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        FillPolygon(points, color);
    }

    public void DrawPolygon(Vector2 center, int sides, float radius, float rotationDegrees, RenderColor color)
    {
        if (sides < 3 || radius <= 0) return;
        var points = new Vector2[sides];
        float rotation = rotationDegrees * MathF.PI / 180f;
        for (int i = 0; i < sides; i++)
        {
            float angle = rotation + i * MathF.Tau / sides;
            points[i] = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        FillPolygon(points, color);
    }

    public void DrawRotatedRectangle(Vector2 position, float length, float width, float rotationDegrees, RenderColor color)
    {
        float angle = rotationDegrees * MathF.PI / 180f;
        Vector2 forward = new(MathF.Cos(angle), MathF.Sin(angle));
        Vector2 side = new(-forward.Y, forward.X);
        Vector2 halfSide = side * (width * 0.5f);
        Vector2 tip = position + forward * length;
        FillPolygon([position - halfSide, tip - halfSide, tip + halfSide, position + halfSide], color);
    }

    public void DrawText(string text, int x, int y, int fontSize, RenderColor color,
        RenderTextStyle style = RenderTextStyle.Ui)
    {
        if (text.Length == 0) return;
        if (style == RenderTextStyle.Title)
        {
            float scaleX = fontSize / 16f;
            float scaleY = fontSize / 8f;
            SetColor(color);
            SDL.SetRenderScale(_renderer, scaleX, scaleY);
            SDL.RenderDebugText(_renderer, x / scaleX, y / scaleY, text);
            SDL.SetRenderScale(_renderer, 1, 1);
            return;
        }

        var key = (text, fontSize, color);
        if (!_textCache.TryGetValue(key, out CachedText? cached))
        {
            IntPtr surface = TTF.RenderTextBlended(GetFont(fontSize), text,
                (nuint)Encoding.UTF8.GetByteCount(text), ToSdl(color));
            if (surface == IntPtr.Zero)
                throw new InvalidOperationException($"SDL3 text rendering failed: {SDL.GetError()}");
            IntPtr texture = SDL.CreateTextureFromSurface(_renderer, surface);
            SDL.DestroySurface(surface);
            if (texture == IntPtr.Zero)
                throw new InvalidOperationException($"SDL3 text texture failed: {SDL.GetError()}");
            SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
            SDL.GetTextureSize(texture, out float width, out float height);
            cached = new CachedText(texture, width, height);
            _textCache.Add(key, cached);
        }
        cached.LastUsedFrame = _frameNumber;
        var destination = new SDL.FRect { X = x, Y = y, W = cached.Width, H = cached.Height };
        SDL.RenderTexture(_renderer, cached.Texture, IntPtr.Zero, in destination);
    }

    public int MeasureText(string text, int fontSize, RenderTextStyle style = RenderTextStyle.Ui)
    {
        if (style == RenderTextStyle.Title) return (int)MathF.Ceiling(text.Length * fontSize * 0.5f);
        if (text.Length == 0) return 0;
        if (!TTF.GetStringSize(GetFont(fontSize), text, (nuint)Encoding.UTF8.GetByteCount(text),
                out int width, out _))
            throw new InvalidOperationException($"SDL3 text measurement failed: {SDL.GetError()}");
        return width;
    }

    public void FillRectangle(RenderRect bounds, RenderColor color)
    {
        SetColor(color);
        var rect = new SDL.FRect { X = bounds.X, Y = bounds.Y, W = bounds.Width, H = bounds.Height };
        SDL.RenderFillRect(_renderer, in rect);
    }

    public void FillRoundedRectangle(RenderRect bounds, float roundness, int segments, RenderColor color) =>
        FillPolygon(RoundedPoints(bounds, roundness, segments), color);

    public void OutlineRoundedRectangle(RenderRect bounds, float roundness, int segments,
        float thickness, RenderColor color)
    {
        Vector2[] points = RoundedPoints(bounds, roundness, segments);
        for (int i = 0; i < points.Length; i++)
            DrawLine(points[i], points[(i + 1) % points.Length], thickness, color);
    }

    private static Vector2[] RoundedPoints(RenderRect bounds, float roundness, int segments)
    {
        float radius = MathF.Min(bounds.Width, bounds.Height) * Math.Clamp(roundness, 0f, 1f) * 0.5f;
        int steps = Math.Max(2, segments);
        var points = new Vector2[4 * (steps + 1)];
        Vector2[] corners =
        [
            new(bounds.X + bounds.Width - radius, bounds.Y + radius),
            new(bounds.X + bounds.Width - radius, bounds.Y + bounds.Height - radius),
            new(bounds.X + radius, bounds.Y + bounds.Height - radius),
            new(bounds.X + radius, bounds.Y + radius),
        ];
        for (int corner = 0; corner < 4; corner++)
            for (int step = 0; step <= steps; step++)
            {
                float angle = (-MathF.PI / 2 + corner * MathF.PI / 2) + step * MathF.PI / (2 * steps);
                points[corner * (steps + 1) + step] = corners[corner] +
                    radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        return points;
    }

    private void FillPolygon(ReadOnlySpan<Vector2> points, RenderColor color)
    {
        if (points.Length < 3) return;
        SDL.FColor sdlColor = new()
        {
            R = color.R / 255f, G = color.G / 255f,
            B = color.B / 255f, A = color.A / 255f,
        };
        var vertices = new SDL.Vertex[points.Length];
        for (int i = 0; i < points.Length; i++)
            vertices[i] = new SDL.Vertex
            {
                Position = new SDL.FPoint { X = points[i].X, Y = points[i].Y },
                Color = sdlColor,
            };
        var indices = new int[(points.Length - 2) * 3];
        for (int i = 0; i < points.Length - 2; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = i + 2;
        }
        SDL.RenderGeometry(_renderer, IntPtr.Zero, vertices, vertices.Length, indices, indices.Length);
    }

    private IntPtr GetFont(int size)
    {
        if (_fonts.TryGetValue(size, out IntPtr font)) return font;
        font = TTF.OpenFont(_fontPath, size);
        if (font == IntPtr.Zero)
            throw new InvalidOperationException($"Could not load SDL3 UI font: {SDL.GetError()}");
        _fonts.Add(size, font);
        return font;
    }

    private void SetColor(RenderColor color) =>
        SDL.SetRenderDrawColor(_renderer, color.R, color.G, color.B, color.A);

    private static SDL.Color ToSdl(RenderColor color) =>
        new() { R = color.R, G = color.G, B = color.B, A = color.A };

    private static SDL.Scancode ToSdl(ClientKey key) => key switch
    {
        ClientKey.W => SDL.Scancode.W, ClientKey.A => SDL.Scancode.A,
        ClientKey.S => SDL.Scancode.S, ClientKey.D => SDL.Scancode.D,
        ClientKey.Space => SDL.Scancode.Space,
        ClientKey.Left => SDL.Scancode.Left, ClientKey.Right => SDL.Scancode.Right,
        ClientKey.Up => SDL.Scancode.Up, ClientKey.Down => SDL.Scancode.Down,
        ClientKey.Home => SDL.Scancode.Home, ClientKey.End => SDL.Scancode.End,
        ClientKey.Tab => SDL.Scancode.Tab, ClientKey.Backspace => SDL.Scancode.Backspace,
        ClientKey.Delete => SDL.Scancode.Delete, ClientKey.Enter => SDL.Scancode.Return,
        ClientKey.One => SDL.Scancode.Alpha1, ClientKey.Two => SDL.Scancode.Alpha2,
        ClientKey.Three => SDL.Scancode.Alpha3, ClientKey.Four => SDL.Scancode.Alpha4,
        ClientKey.Five => SDL.Scancode.Alpha5, ClientKey.Six => SDL.Scancode.Alpha6,
        ClientKey.Seven => SDL.Scancode.Alpha7, ClientKey.Eight => SDL.Scancode.Alpha8,
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };

    private sealed class CachedText(IntPtr texture, float width, float height)
    {
        public IntPtr Texture { get; } = texture;
        public float Width { get; } = width;
        public float Height { get; } = height;
        public int LastUsedFrame { get; set; }
    }
}
