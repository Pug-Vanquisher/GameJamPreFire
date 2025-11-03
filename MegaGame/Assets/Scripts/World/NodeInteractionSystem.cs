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

    void OnEnable()
    {
        EventBus.Subscribe<GarrisonCountChanged>(OnGarrisonChanged);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<GarrisonCountChanged>(OnGarrisonChanged);
    }

    void OnGarrisonChanged(GarrisonCountChanged e)
    {
        var ws = WorldState.Instance; if (!ws) return;

        var city = ws.FindCityById(e.NodeId);
        if (city != null)
        {
            Debug.Log($"[Garrison] {city.Name}: осталось защитников {e.Remaining}.");
            if (e.Remaining <= 0) Debug.Log($"[Garrison] {city.Name}: гарнизон уничтожен — город можно захватить.");
            return;
        }

        var camp = ws.FindCampById(e.NodeId);
        if (camp != null)
        {
            Debug.Log($"[Garrison] {camp.Name}: осталось защитников {e.Remaining}.");
            if (e.Remaining <= 0) Debug.Log($"[Garrison] {camp.Name}: гарнизон уничтожен — лагерь можно уничтожить.");
        }
    }

    void CaptureCity(WorldState ws, NodeData city)
    {
        if (city.IsCaptured) return;

        // СЧИТАЕМ ЖИВЫЕ ГАРНИЗОННЫЕ ОТРЯДЫ У ЭТОГО ГОРОДА
        int alive = ws.EnemySquads.FindAll(s => s.IsGarrison && s.AnchorNodeId == city.Id).Count;
        if (alive > 0)
        {
            Debug.Log($"[City] {city.Name}: гарнизон ещё жив ({alive}). Захват невозможен.");
            return;
        }

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

        // Считаем ЖИВЫЕ гарнизонные отряды, привязанные к этому лагерю
        int alive = ws.EnemySquads.FindAll(s => s.IsGarrison && s.AnchorNodeId == camp.Id).Count;
        if (alive > 0)
        {
            Debug.Log($"[Camp] {camp.Name}: гарнизон ещё жив ({alive}). Уничтожение невозможно.");
            return;
        }

        // Забираем ресурсы игроку
        int f = camp.Fuel, m = camp.Meds, a = camp.Ammo;
        PlayerInventory.Add(f, m, a);

        // Помечаем лагерь уничтоженным и убираем из мира
        camp.IsDestroyed = true;
        ws.Camps.RemoveAll(n => n.Id == camp.Id);

        Debug.Log($"[Camp] Уничтожен: {camp.Name}. Получено: топливо {f}, мед {m}, патроны {a}");

        // Отправляем событие для композера + сигнал нод-системе (радар/карта и т.п.)
        EventBus.Publish(new CampDestroyed(camp.Id, camp.Name, f, m, a));
        EventBus.Publish(new NodeRemoved(camp.Id));
    }
}
