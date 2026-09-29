using DeepIo.Server.Entities;

namespace DeepIo.Server.Events;

/// <summary>
/// Base of everything published on the <see cref="GameEventBus"/>. Observers receive this type
/// and pattern-match on the concrete events they care about.
/// </summary>
public abstract record GameEvent;

/// <summary>Raised by <see cref="Entity.ApplyDamage"/> for every hit that lands.</summary>
public sealed record EntityDamagedEvent(Entity Target, Entity? Attacker, float Amount) : GameEvent;

/// <summary>Raised by <see cref="Entity.ApplyDamage"/> when the hit was the killing blow.</summary>
public sealed record EntityDestroyedEvent(Entity Victim, Entity? Killer) : GameEvent;

/// <summary>Raised when awarded XP pushes a tank past one or more level thresholds.</summary>
public sealed record PlayerLeveledUpEvent(Tank Tank, int OldLevel, int NewLevel) : GameEvent;

/// <summary>Raised when a tank (human or bot) enters the arena.</summary>
public sealed record PlayerJoinedEvent(Tank Tank) : GameEvent;

/// <summary>Raised when a human's connection drops and their tank is removed.</summary>
public sealed record PlayerLeftEvent(Tank Tank) : GameEvent;

/// <summary>Raised by <c>AchievementObserver</c> the first time a tank earns an achievement.</summary>
public sealed record AchievementUnlockedEvent(Tank Tank, string Title) : GameEvent;
