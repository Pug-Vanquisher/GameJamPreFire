using UnityEngine;
using Events;

public static class PlayerState
{
    public static Vector2 Pos;
    public static float Speed = 400f;
}

public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 400f;

    [Tooltip("Разрешить WASD/стрелки (только для отладки). Основным вводом служит меню «Движение».")]
    [SerializeField] private bool debugWASD = true;

    [Header("Fuel")]
    [Tooltip("Литров на единицу пути (юнит карты)")]
    [SerializeField] private float fuelPerUnit = 0.0008333f;

    // режим: можно ли принимать события движения из консоли
    private bool allowConsoleMove = false;

    // направление из консоли движения
    private Vector2 consoleDir = Vector2.zero;
    private float consoleDirTs = -999f;
    private const float consoleHoldTimeout = 0.05f;

    void OnEnable()
    {
        EventBus.Subscribe<ConsoleMoveInput>(OnConsoleMove);
        EventBus.Subscribe<ConsoleMoveModeChanged>(OnConsoleMoveModeChanged);
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        EventBus.Subscribe<RunStarted>(OnRunStarted);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<ConsoleMoveInput>(OnConsoleMove);
        EventBus.Unsubscribe<ConsoleMoveModeChanged>(OnConsoleMoveModeChanged);
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<RunStarted>(OnRunStarted);
    }

    void OnConsoleMoveModeChanged(ConsoleMoveModeChanged e)
    {
        allowConsoleMove = e.Active;
        if (!allowConsoleMove) { consoleDir = Vector2.zero; consoleDirTs = -999f; }
    }

    void OnConsoleMove(ConsoleMoveInput e)
    {
        if (!allowConsoleMove) return;   // игнор, если меню движения не активно
        consoleDir = e.Dir;
        consoleDirTs = Time.time;
    }

    void Start()
    {
        PlayerState.Speed = speed;

        // страховка
        if (PlayerInventory.MaxHealth > 0 && PlayerInventory.Health == 0
            && PlayerInventory.Ammo == 0 && Mathf.Approximately(PlayerInventory.Fuel, 0f))
        {
            PlayerInventory.Init(100, 100, 100);
        }

        PlayerInventory.ConfigureFuelConsumption(fuelPerUnit);

        var ws = WorldState.Instance;
        if (ws != null && ws.PlayerBase != null)
            SnapTo(ws.PlayerSpawn);
    }

    void OnMapGenerated(MapGenerated _)
    {
        var ws = WorldState.Instance;
        if (ws != null && ws.PlayerBase != null)
        {
            PlayerInventory.ConfigureFuelConsumption(fuelPerUnit);
            SnapTo(ws.PlayerSpawn);
        }
    }

    void OnRunStarted(RunStarted _)
    {
        var ws = WorldState.Instance;
        if (ws != null && ws.PlayerBase != null)
        {
            PlayerInventory.ConfigureFuelConsumption(fuelPerUnit);
            SnapTo(ws.PlayerSpawn);
        }
    }

    void SnapTo(Vector2 p)
    {
        PlayerState.Pos = p;
        EventBus.Publish(new PlayerMoved(PlayerState.Pos));
        // transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    void Update()
    {
        var ws = WorldState.Instance;
        if (!ws) return;

        Vector2 dir = Vector2.zero;

        // 1) Основное управление — из меню «Движение»
        if (allowConsoleMove && (Time.time - consoleDirTs < consoleHoldTimeout))
            dir += consoleDir;

        // 2) Отладочный ввод
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugWASD)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            dir += new Vector2(h, v);
        }
#endif

        if (dir.sqrMagnitude < 1e-8f) return;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector2 delta = dir * (PlayerState.Speed * Time.deltaTime);

        float dist = delta.magnitude;
        PlayerInventory.ConsumeFuelByDistance(dist);

        float half = ws.MapHalfSize;
        PlayerState.Pos = new Vector2(
            Mathf.Clamp(PlayerState.Pos.x + delta.x, -half, half),
            Mathf.Clamp(PlayerState.Pos.y + delta.y, -half, half)
        );

        EventBus.Publish(new PlayerMoved(PlayerState.Pos));
        //SoundManager.Instance.PlaySound(6);
    }
}
