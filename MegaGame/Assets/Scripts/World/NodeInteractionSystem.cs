using UnityEngine;
using Events;

public class NodeInteractionSystem : MonoBehaviour
{
    [SerializeField] private float enterRadius = 120f;   
    [SerializeField] private bool autoCaptureDebug = true; 

    void Update()
    {
        var ws = WorldState.Instance; if (!ws) return;

        bool isCamp;
        var node = ws.FindNearestNode(PlayerState.Pos, enterRadius, out isCamp);
        if (node == null) return;

        if (!autoCaptureDebug && !Input.GetKeyDown(KeyCode.E)) return;

        if (isCamp)
        {
            CaptureCamp(ws, node);
        }
        else if (node.Type == NodeType.City)
        {
            CaptureCity(ws, node);
        }
    }

    void CaptureCity(WorldState ws, NodeData city)
    {
        if (city.IsCaptured) return;

        int garrison = 0; // city.Garrison;
        if (garrison > 0) { Debug.Log($"[City] {city.Name}: гарнизон ещё жив ({city.Garrison})."); return; }

        city.IsCaptured = true;
        city.Faction = Faction.Player;

        int f = city.Fuel, m = city.Meds, a = city.Ammo;
        PlayerInventory.Add(f, m, a);
        city.Fuel = city.Meds = city.Ammo = 0;

        Debug.Log($"[City] Захвачено: {city.Name}. Ресурсы получены: топливо {f}, мед {m}, патроны {a}");
        EventBus.Publish(new CityCaptured(city.Id, city.Name, f, m, a));
    }

    void CaptureCamp(WorldState ws, NodeData camp)
    {
        if (camp.IsDestroyed) return;

        int garrison = 0; // camp.Garrison;
        if (garrison > 0) { Debug.Log($"[Camp] {camp.Name}: гарнизон ещё жив ({camp.Garrison})."); return; }

        int f = camp.Fuel, m = camp.Meds, a = camp.Ammo;
        PlayerInventory.Add(f, m, a);

        camp.IsDestroyed = true;

        ws.Camps.RemoveAll(n => n.Id == camp.Id);
        Debug.Log($"[Camp] Уничтожен: {camp.Name}. Получено: топливо {f}, мед {m}, патроны {a}");
        EventBus.Publish(new CampDestroyed(camp.Id, camp.Name, f, m, a));
        EventBus.Publish(new NodeRemoved(camp.Id));
    }
}
