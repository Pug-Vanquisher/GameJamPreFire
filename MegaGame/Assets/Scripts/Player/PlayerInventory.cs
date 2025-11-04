using UnityEngine;
using Events;

public static class PlayerInventory
{
    public const int MaxHealth = 100;
    public const int MaxFuel = 100;
    public const int MaxAmmo = 200;

    public static int Health { get; private set; }
    public static float Fuel { get; private set; }
    public static int Ammo { get; private set; }

    public static float FuelPerUnit { get; private set; } = 0.0008333f;

    public static void Init(int fuel = 100, int health = 100, int ammo = 100)
    {
        Fuel = Mathf.Clamp(fuel, 0, MaxFuel);
        Health = Mathf.Clamp(health, 0, MaxHealth);
        Ammo = Mathf.Clamp(ammo, 0, MaxAmmo);
        Debug.Log($"[PlayerInventory.Init] Fuel:{Fuel}/{MaxFuel} HP:{Health}/{MaxHealth} Ammo:{Ammo}/{MaxAmmo}");
    }

    public static void ConfigureFuelConsumption(float fuelPerUnit)
    {
        FuelPerUnit = Mathf.Max(0f, fuelPerUnit);
        Debug.Log($"[PlayerInventory] FuelPerUnit set to {FuelPerUnit}");
    }

    public static void Add(int fuel, int meds, int ammo)
    {
        Fuel = Mathf.Clamp(Fuel + Mathf.Max(0, fuel), 0, MaxFuel);
        Health = Mathf.Clamp(Health + Mathf.Max(0, meds), 0, MaxHealth);
        Ammo = Mathf.Clamp(Ammo + Mathf.Max(0, ammo), 0, MaxAmmo);
        Debug.Log($"[PlayerInventory] +Fuel:{fuel} +HP:{meds} +Ammo:{ammo} => Fuel:{Fuel} HP:{Health} Ammo:{Ammo}");
    }

    public static int SpendAmmo(int count)
    {
        if (count <= 0) return 0;
        int taken = Mathf.Min(Ammo, count);
        Ammo -= taken;
        return taken;
    }

    public static void Damage(int dmg)
    {
        int old = Health;
        Health = Mathf.Max(0, Health - Mathf.Abs(dmg));

        EventBus.Publish(new PlayerDamaged(Mathf.Abs(dmg), Health));
        if (old > 0 && Health == 0)
            EventBus.Publish(new PlayerDied());
    }

    public static float ConsumeFuelByDistance(float distance)
    {
        if (distance <= 0f || FuelPerUnit <= 0f || Fuel <= 0f) return 0f;
        float need = distance * FuelPerUnit;
        float spent = Mathf.Min(Fuel, need);
        Fuel -= spent;
        return spent;
    }

    public static float MaxReachableDistance() =>
        (FuelPerUnit <= 0f) ? float.PositiveInfinity : (Fuel / FuelPerUnit);

    public static void Heal(int hp) => Health = Mathf.Clamp(Health + Mathf.Max(0, hp), 0, MaxHealth);
    public static float TryConsumeFuel(float amount)
    {
        if (amount <= 0f || Fuel <= 0f) return 0f;
        float spent = Mathf.Min(Fuel, amount);
        Fuel -= spent;
        return spent;
    }

    public static bool TrySpendHealth(int amount)
    {
        if (amount <= 0) return false;
        if (Health < amount) { Health = 0; return false; }
        Health -= amount;
        return true;
    }
}
