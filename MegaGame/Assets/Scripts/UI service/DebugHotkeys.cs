using UnityEngine;

public class DebugHotkeys : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log(
                $"[DEBUG] Pos:{PlayerState.Pos}  " +
                $"Fuel:{PlayerInventory.Fuel:0.0}/{PlayerInventory.MaxFuel} (perUnit:{PlayerInventory.FuelPerUnit:0.########})  " +
                $"HP:{PlayerInventory.Health}/{PlayerInventory.MaxHealth}  " +
                $"Ammo:{PlayerInventory.Ammo}/{PlayerInventory.MaxAmmo}"
            );
        }
    }
}
