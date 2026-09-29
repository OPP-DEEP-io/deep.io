using DeepIo.Shared;

namespace DeepIo.Server.Entities;

/// <summary>
/// How many skill points a tank has put into each <see cref="StatKind"/>, and the modifiers
/// those levels translate into. All levels at 0 means the tank behaves exactly like the bare
/// parts its Abstract Factory produced.
/// </summary>
public sealed class TankStats
{
    private static readonly int StatCount = Enum.GetValues<StatKind>().Length;

    private readonly int[] _levels = new int[StatCount];

    public int this[StatKind stat] => _levels[(int)stat];

    public bool CanIncrease(StatKind stat) => _levels[(int)stat] < GameConstants.MaxStatLevel;

    public void Increase(StatKind stat)
    {
        if (!CanIncrease(stat))
            throw new InvalidOperationException($"{stat} is already at level {GameConstants.MaxStatLevel}.");
        _levels[(int)stat]++;
    }

    public void Decrease(StatKind stat)
    {
        if (_levels[(int)stat] == 0)
            throw new InvalidOperationException($"{stat} has no points to take back.");
        _levels[(int)stat]--;
    }

    public void Reset() => Array.Clear(_levels);

    /// <summary>Copy for the wire (<see cref="EntityDto.Up"/>).</summary>
    public int[] ToArray() => (int[])_levels.Clone();

    public float RegenMultiplier => 1f + 0.5f * this[StatKind.HealthRegen];
    public float MaxHealthMultiplier => 1f + 0.10f * this[StatKind.MaxHealth];

    /// <summary>Contact damage per second dealt while touching a shape or enemy tank. 0 until upgraded.</summary>
    public float BodyDamagePerSecond => 18f * this[StatKind.BodyDamage];

    public float BulletSpeedMultiplier => 1f + 0.08f * this[StatKind.BulletSpeed];

    /// <summary>Extra targets a round can pass through before it is spent.</summary>
    public int BulletPenetration => this[StatKind.BulletPenetration];

    public float BulletDamageMultiplier => 1f + 0.12f * this[StatKind.BulletDamage];
    public float ReloadMultiplier => 1f - 0.07f * this[StatKind.Reload];
    public float MoveSpeedMultiplier => 1f + 0.05f * this[StatKind.MovementSpeed];
}
