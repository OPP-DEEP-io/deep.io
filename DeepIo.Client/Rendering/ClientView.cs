namespace DeepIo.Client.Rendering;

/// <summary>Screen abstraction; screens decide what to draw while backend decides how.</summary>
public abstract class ClientView<TState>(IRenderBackend backend)
{
    protected IRenderBackend Backend { get; } = backend;

    public abstract void Draw(TState state);
}
