namespace DeepIo.Server.Entities.Parts;

/// <summary>
/// Abstract Factory product #2: the barrel bolted onto a chassis. Owns the fire rate and
/// how accurately rounds leave the muzzle.
/// </summary>
public interface IWeapon
{
    string Name { get; }
    float ReloadInterval { get; }   // seconds between shots
    float Spread { get; }           // max aim error in radians
    float MuzzleGap { get; }        // extra spawn distance in front of the hull
    float Recoil { get; }           // impulse pushed back into the tank when firing
}

/// <summary>One shot every ~third of a second, dead on target.</summary>
public sealed class SingleBarrel : IWeapon
{
    public string Name => "Single";
    public float ReloadInterval => 0.35f;
    public float Spread => 0f;
    public float MuzzleGap => 2f;
    public float Recoil => 20f;
}

/// <summary>Slow, perfectly accurate barrel that reaches across the arena.</summary>
public sealed class LongBarrel : IWeapon
{
    public string Name => "Long";
    public float ReloadInterval => 0.95f;
    public float Spread => 0f;
    public float MuzzleGap => 16f;
    public float Recoil => 70f;
}

/// <summary>Sprays cheap rounds; accuracy is traded away for rate of fire.</summary>
public sealed class RapidBarrel : IWeapon
{
    public string Name => "Rapid";
    public float ReloadInterval => 0.11f;
    public float Spread => 0.10f;
    public float MuzzleGap => 4f;
    public float Recoil => 8f;
}
