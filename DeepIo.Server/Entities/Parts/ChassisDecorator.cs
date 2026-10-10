namespace DeepIo.Server.Entities.Parts;

public abstract class ChassisDecorator(IChassis inner) : IChassis
{
    public IChassis Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));
    public virtual string Name => Inner.Name;
    public virtual float MaxHp => Inner.MaxHp;
    public virtual float Speed => Inner.Speed;
    public virtual float Radius => Inner.Radius;
    public virtual float RegenPerSecond => Inner.RegenPerSecond;
}

public sealed class ArmoredChassis(IChassis inner) : ChassisDecorator(inner)
{
    public override string Name => $"{Inner.Name} + Armor";
    public override float MaxHp => Inner.MaxHp * 1.2f;
}

public sealed class RegeneratingChassis(IChassis inner) : ChassisDecorator(inner)
{
    public override string Name => $"{Inner.Name} + Regen";
    public override float RegenPerSecond => Inner.RegenPerSecond + 2f;
}

public sealed class TurboChassis(IChassis inner) : ChassisDecorator(inner)
{
    public override string Name => $"{Inner.Name} + Turbo";
    public override float Speed => Inner.Speed * 1.1f;
}

public static class ChassisUpgrades
{
    public static IChassis ForLevel(IChassis chassis, int level)
    {
        IChassis result = chassis;
        if (level >= 5) result = new ArmoredChassis(result);
        if (level >= 10) result = new RegeneratingChassis(result);
        if (level >= 15) result = new TurboChassis(result);
        return result;
    }
}
