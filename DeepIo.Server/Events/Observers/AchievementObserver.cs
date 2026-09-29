using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Events.Observers;

/// <summary>
/// Tracks per-tank progress (damage dealt, shapes and tanks destroyed, level milestones) and
/// publishes <see cref="AchievementUnlockedEvent"/> the first time a tank earns each title.
/// </summary>
public sealed class AchievementObserver : IGameObserver
{
    public const string FirstBlood = "First Blood";
    public const string PentagonSlayer = "Pentagon Slayer";
    public const string ShapeHunter = "Shape Hunter";
    public const string TankBuster = "Tank Buster";
    public const string HeavyHitter = "Heavy Hitter";

    private const int ShapeHunterCount = 25;
    private const int TankBusterCount = 5;
    private const float HeavyHitterDamage = 1000f;
    private static readonly int[] LevelMilestones = [15, 30, 45];

    private sealed class Progress
    {
        public float Damage;
        public int Shapes;
        public int Kills;
        public readonly HashSet<string> Unlocked = new();
    }

    private readonly IGameSubject _events;
    private readonly Dictionary<int, Progress> _progress = new();
    private bool _firstBloodTaken;

    public AchievementObserver(IGameSubject events) => _events = events;

    public void OnNotify(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case EntityDamagedEvent { Attacker: Tank attacker } damaged:
            {
                Progress p = For(attacker);
                p.Damage += damaged.Amount;
                if (p.Damage >= HeavyHitterDamage) Unlock(attacker, p, HeavyHitter);
                break;
            }

            case EntityDestroyedEvent { Killer: Tank killer, Victim: Shape shape }:
            {
                Progress p = For(killer);
                p.Shapes++;
                if (shape.PolygonKind == ShapeKind.Pentagon) Unlock(killer, p, PentagonSlayer);
                if (p.Shapes >= ShapeHunterCount) Unlock(killer, p, ShapeHunter);
                break;
            }

            case EntityDestroyedEvent { Killer: Tank killer, Victim: Tank }:
            {
                Progress p = For(killer);
                p.Kills++;
                if (!_firstBloodTaken)
                {
                    _firstBloodTaken = true;
                    Unlock(killer, p, FirstBlood);
                }
                if (p.Kills >= TankBusterCount) Unlock(killer, p, TankBuster);
                break;
            }

            case PlayerLeveledUpEvent levelUp:
            {
                Progress p = For(levelUp.Tank);
                foreach (int milestone in LevelMilestones)
                    if (levelUp.OldLevel < milestone && levelUp.NewLevel >= milestone)
                        Unlock(levelUp.Tank, p, $"Level {milestone}");
                break;
            }

            case PlayerLeftEvent left:
                _progress.Remove(left.Tank.Id);
                break;
        }
    }

    private Progress For(Tank tank)
    {
        if (!_progress.TryGetValue(tank.Id, out Progress? p))
            _progress[tank.Id] = p = new Progress();
        return p;
    }

    private void Unlock(Tank tank, Progress progress, string title)
    {
        if (progress.Unlocked.Add(title))
            _events.Notify(new AchievementUnlockedEvent(tank, title));
    }
}
