namespace DeepIo.Server.Entities.Parts;

/// <summary>
/// Abstract Factory product #3: the ammunition a barrel fires. Every <see cref="Bullet"/> is
/// stamped out of one of these, so ballistics live with the build and not in GameWorld.
/// </summary>
public interface IProjectileSpec
{
    string Name { get; }
    float Speed { get; }            // units / second
    float Damage { get; }
    float Radius { get; }
    float LifeSeconds { get; }      // time before the round despawns
}

/// <summary>The default round: middling speed, middling damage.</summary>
public sealed class StandardRound : IProjectileSpec
{
    public string Name => "Standard";
    public float Speed => 520f;
    public float Damage => 12f;
    public float Radius => 7f;
    public float LifeSeconds => 1.6f;
}

/// <summary>A fast, heavy round that stays alive long enough to cross open ground.</summary>
public sealed class SniperRound : IProjectileSpec
{
    public string Name => "Sniper";
    public float Speed => 940f;
    public float Damage => 33f;
    public float Radius => 6f;
    public float LifeSeconds => 2.4f;
}

/// <summary>A small, short-lived round; damage comes from volume, not from single hits.</summary>
public sealed class RapidRound : IProjectileSpec
{
    public string Name => "Rapid";
    public float Speed => 610f;
    public float Damage => 5f;
    public float Radius => 5f;
    public float LifeSeconds => 0.9f;
}
