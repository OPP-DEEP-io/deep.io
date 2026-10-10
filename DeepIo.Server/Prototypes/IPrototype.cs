namespace DeepIo.Server.Prototypes;

public enum CopyDepth { Shallow, Deep }

/// <summary>Copies an existing configured instance without knowing its construction steps.</summary>
public interface IPrototype<out T>
{
    T Clone(CopyDepth depth = CopyDepth.Deep);
}
