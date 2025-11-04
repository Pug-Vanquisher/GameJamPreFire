using UnityEngine;
using Events;

public static class PlayerWeaponState
{
    public static int MagSize { get; private set; } = 10;
    public static int InMag { get; private set; } = 10;

    public static void Configure(int magSize, int startInMag = -1)
    {
        MagSize = Mathf.Max(1, magSize);
        InMag = (startInMag < 0) ? MagSize : Mathf.Clamp(startInMag, 0, MagSize);
    }

    public static bool Reload()
    {
        int need = MagSize - InMag;
        if (need <= 0) return false;
        int loaded = PlayerInventory.SpendAmmo(need);
        InMag += loaded;
        return loaded > 0;
    }

    public static bool CanSpend(int count) => count > 0 && InMag >= count;

    public static bool TrySpend(int count)
    {
        if (!CanSpend(count)) return false;
        InMag -= count;
        return true;
    }

    public static int SpendUpTo(int count)
    {
        if (count <= 0) return 0;
        int take = Mathf.Min(InMag, count);
        InMag -= take;
        return take;
    }

    public static (int inMag, int magSize, int reserve) GetAmmoStatus()
        => (InMag, MagSize, PlayerInventory.Ammo);
}
