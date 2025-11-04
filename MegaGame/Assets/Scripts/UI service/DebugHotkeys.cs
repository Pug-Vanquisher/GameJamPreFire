using Events;
using UnityEngine;

public class DebugHotkeys : MonoBehaviour
{
    string id = "1";
    string name = "Pidor";
    int defaultNmb = 1;
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
            EventBus.Publish(new CityCaptured(id, name, defaultNmb, defaultNmb, defaultNmb));
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            EventBus.Publish(new PlayerDamaged(1, 10));
        }
    }
}
