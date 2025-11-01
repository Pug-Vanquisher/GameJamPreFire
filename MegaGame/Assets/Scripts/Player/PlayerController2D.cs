using UnityEngine;
using Events;

public static class PlayerState
{
    public static Vector2 Pos;
    public static float Speed = 400f; // ών/ρ
}

public class PlayerController2D : MonoBehaviour
{
    [SerializeField] private float speed = 400f;

    [SerializeField] private float fuelPerUnit = 0.0008333f;

    private bool initialized;

    void OnEnable() => EventBus.Subscribe<MapGenerated>(OnMapGenerated);
    void OnDisable() => EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);

    void Start()
    {
        PlayerState.Speed = speed;

        var ws = WorldState.Instance;
        if (ws != null && ws.PlayerBase != null)
        {
            SnapTo(ws.PlayerSpawn);
            TryInitResourcesOnce();
        }
    }

    void OnMapGenerated(MapGenerated _)
    {
        var ws = WorldState.Instance;
        if (ws != null && ws.PlayerBase != null)
        {
            SnapTo(ws.PlayerSpawn);
            TryInitResourcesOnce();
        }
    }

    void TryInitResourcesOnce()
    {
        if (initialized) return;
        PlayerInventory.Init(fuel: 100, health: 100, ammo: 100);
        PlayerInventory.ConfigureFuelConsumption(fuelPerUnit);
        initialized = true;
    }

    void SnapTo(Vector2 p)
    {
        PlayerState.Pos = p;
        EventBus.Publish(new PlayerMoved(PlayerState.Pos));
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 dir = new Vector2(h, v);

        if (dir.sqrMagnitude < 1e-6f) return;

        dir.Normalize();

        var ws = WorldState.Instance;
        if (!ws) return;

        float half = ws.MapHalfSize;

        float intended = PlayerState.Speed * Time.deltaTime;

        float maxByFuel = PlayerInventory.MaxReachableDistance();
        float move = Mathf.Min(intended, maxByFuel);
        if (move <= 0f) return; 

        Vector2 delta = dir * move;

        Vector2 newPos = new Vector2(
            Mathf.Clamp(PlayerState.Pos.x + delta.x, -half, half),
            Mathf.Clamp(PlayerState.Pos.y + delta.y, -half, half)
        );

        float traveled = (newPos - PlayerState.Pos).magnitude;
        if (traveled <= 0f) return;

        PlayerInventory.ConsumeFuelByDistance(traveled);
        PlayerState.Pos = newPos;
        EventBus.Publish(new PlayerMoved(PlayerState.Pos));
    }
}
