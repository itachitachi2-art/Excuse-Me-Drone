using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Controlled doubles, never a replacement for a 7DTD/Unity runtime test.
public class EntityAlive
{
    public EntityAlive Revenge;
    public int Timer;
    public int Calls;
    public int Health = 87;
    public int DamageEvents = 12;
    public EntityAlive Attack;
    public bool Destroyed;

    // Model Unity's destroyed-object null comparison: the production guard must
    // distinguish this from a real null argument, without consulting Unity.
    public static bool operator ==(EntityAlive a, EntityAlive b)
    {
        bool an = object.ReferenceEquals(a, null) || a.Destroyed;
        bool bn = object.ReferenceEquals(b, null) || b.Destroyed;
        return (an && bn) || object.ReferenceEquals(a, b);
    }
    public static bool operator !=(EntityAlive a, EntityAlive b) { return !(a == b); }
    public override bool Equals(object o) { return object.ReferenceEquals(this, o); }
    public override int GetHashCode() { return base.GetHashCode(); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetRevengeTarget(EntityAlive _other)
    {
        Calls++;
        Revenge = _other;
        Timer = _other == null ? 0 : 500;
    }
}
public class EntityDrone : EntityAlive { }
public class CustomDrone : EntityDrone { }
public class EntityZombie : EntityAlive { }
public class EntityPlayer : EntityAlive { }

public static class RevengeTests
{
    private static int passed;
    private static readonly MethodInfo Prefix = typeof(Itachi.ExcuseMeDrone.EntityDroneRevengePatch)
        .GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        passed++;
        Console.WriteLine("PASS " + message);
    }
    private static bool Allow(EntityAlive recipient, EntityAlive other)
    {
        return (bool)Prefix.Invoke(null, new object[] { recipient, other });
    }
    private static void ApplyControlled(EntityAlive recipient, EntityAlive other)
    {
        if (Allow(recipient, other)) recipient.SetRevengeTarget(other);
    }
    public static void Main(string[] args)
    {
        var drone = new EntityDrone();
        var player = new EntityPlayer();
        var zombie = new EntityZombie();
        var old = new EntityAlive();
        Assert(!Allow(drone, player), "drone rejects player registration");
        Assert(!Allow(drone, zombie), "drone rejects zombie registration");
        Assert(!Allow(drone, new EntityDrone()), "drone rejects drone attacker");
        Assert(!Allow(drone, new EntityAlive()), "drone rejects arbitrary script-supplied entity");
        Assert(!Allow(drone, drone), "drone rejects self registration");
        Assert(!Allow(new CustomDrone(), player), "derived drones also guarded");
        Assert(Allow(drone, null), "drone native null clear allowed");
        Assert(Allow(new CustomDrone(), null), "derived drone null clear allowed");
        Assert(Allow(player, drone), "non-drone attacked by drone unchanged");
        Assert(Allow(zombie, player), "zombie registration unchanged");
        Assert(Allow(new EntityAlive(), zombie), "base entity registration unchanged");
        Assert(Allow(zombie, null), "non-drone null clear allowed");
        Assert(Allow(null, player), "defensive null recipient adds no filtering");
        Assert(!Allow(drone, new EntityAlive { Destroyed = true }), "destroyed non-null wrapper is not a native clear");
        Assert(Allow(zombie, new EntityAlive { Destroyed = true }), "non-drone fake-null behavior stays native");
        drone.Revenge = old; drone.Timer = 237; drone.Attack = zombie;
        ApplyControlled(drone, player);
        Assert(object.ReferenceEquals(drone.Revenge, old) && drone.Timer == 237 && drone.Calls == 0,
            "blocked setter preserves old revenge and timer until native clear");
        Assert(drone.Health == 87 && drone.DamageEvents == 12 && object.ReferenceEquals(drone.Attack, zombie),
            "guard has no HP, damage-event or attack-target side effects");
        ApplyControlled(drone, null);
        Assert(object.ReferenceEquals(drone.Revenge, null) && drone.Timer == 0 && drone.Calls == 1,
            "native clear runs and resets revenge plus timer");
        ApplyControlled(zombie, player);
        Assert(object.ReferenceEquals(zombie.Revenge, player) && zombie.Timer == 500 && zombie.Calls == 1,
            "non-drone original setter runs");
        ApplyControlled(zombie, null);
        Assert(object.ReferenceEquals(zombie.Revenge, null) && zombie.Timer == 0 && zombie.Calls == 2,
            "non-drone clear unchanged");
        for (int i = 0; i < 50; i++) ApplyControlled(drone, player);
        Assert(drone.Calls == 1 && drone.Timer == 0 && object.ReferenceEquals(drone.Revenge, null),
            "repeated registration does not create or refresh revenge");
        Console.WriteLine(passed + " direct production-prefix controlled cases PASS");

        if (args.Length == 0 || args[0] != "--harmony") return;
        // Applies the exact production attribute and prefix with the real shipped
        // Harmony, but to controlled entity doubles rather than game assemblies.
        var harmony = new Harmony("itachi.excusemedrone.tests.revenge");
        harmony.CreateClassProcessor(typeof(Itachi.ExcuseMeDrone.EntityDroneRevengePatch)).Patch();
        var patched = new EntityDrone { Revenge = old, Timer = 123, Attack = zombie };
        patched.SetRevengeTarget(player);
        Assert(patched.Calls == 0 && object.ReferenceEquals(patched.Revenge, old) && patched.Timer == 123,
            "real Harmony binding skips original drone setter");
        patched.SetRevengeTarget(null);
        Assert(patched.Calls == 1 && object.ReferenceEquals(patched.Revenge, null) && patched.Timer == 0,
            "real Harmony binding allows native clear");
        var other = new EntityZombie(); other.SetRevengeTarget(player);
        Assert(other.Calls == 1 && object.ReferenceEquals(other.Revenge, player),
            "real Harmony binding leaves non-drone setter active");
        Assert(patched.Health == 87 && patched.DamageEvents == 12 && object.ReferenceEquals(patched.Attack, zombie),
            "real Harmony skip leaves unrelated state intact");
        harmony.UnpatchSelf();
        patched.SetRevengeTarget(player);
        Assert(patched.Calls == 2 && object.ReferenceEquals(patched.Revenge, player) && patched.Timer == 500,
            "unpatch restores baseline registration");
        Console.WriteLine("5 real-Harmony controlled entity cases PASS (no game execution)");
    }
}
