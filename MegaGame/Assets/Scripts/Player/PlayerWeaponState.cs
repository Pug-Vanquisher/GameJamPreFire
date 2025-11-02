// PlayerWeaponState.cs
using UnityEngine;
using Events;

public static class PlayerWeaponState
{
    public static int MagSize { get; private set; } = 10;   // настраиваемо
    public static int InMag { get; private set; } = 10;

    public static void Configure(int magSize, int startInMag = -1)
    {
        MagSize = Mathf.Max(1, magSize);
        InMag = (startInMag < 0) ? MagSize : Mathf.Clamp(startInMag, 0, MagSize);
    }

    public static bool Reload()
    {
        int need = MagSize - InMag;
        if (need <= 0) return false;              // уже полный
        int loaded = PlayerInventory.SpendAmmo(need);
        InMag += loaded;
        return loaded > 0;
    }
}
