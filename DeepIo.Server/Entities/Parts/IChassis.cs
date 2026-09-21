namespace DeepIo.Server.Entities.Parts;

/// <summary>
/// Abstract Factory product #1: the hull a tank is built on. Supplies the movement and survivability half of a build.
/// </summary>
public interface IChassis
{
    string Name { get; }
    float MaxHp { get; }
    float Speed { get; }            // units / second
    float Radius { get; }           // collision + draw radius
    float RegenPerSecond { get; }
}

/// <summary>Balanced hull used by the default build.</summary>
public sealed class LightChassis : IChassis
{
    public string Name => "Light";
    public float MaxHp => 100f;
    public float Speed => 260f;
    public float Radius => 22f;
    public float RegenPerSecond => 3f;
}

/// <summary>Slow, bulky hull that lets a sniper survive being closed down.</summary>
public sealed class HeavyChassis : IChassis
{
    public string Name => "Heavy";
    public float MaxHp => 145f;
    public float Speed => 205f;
    public float Radius => 26f;
    public float RegenPerSecond => 2f;
}

/// <summary>Fragile but fast hull that suits sustained close-range fire.</summary>
public sealed class AssaultChassis : IChassis
{
    public string Name => "Assault";
    public float MaxHp => 85f;
    public float Speed => 305f;
    public float Radius => 20f;
    public float RegenPerSecond => 4f;
}
