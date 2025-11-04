using UnityEngine;
using Events;

[DefaultExecutionOrder(-1000)]
public class GameRunManager : MonoBehaviour
{
    public static GameRunManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private MapGenerator mapGen;

    [Header("Player defaults")]
    [SerializeField] private int startFuel = 100;
    [SerializeField] private int startHp = 100;
    [SerializeField] private int startAmmo = 100;
    [SerializeField] private int startMag = 10;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // рестарт на той же карте
    public void RestartSameMap()
    {
        var ws = WorldState.Instance;
        if (!ws || mapGen == null)
        {
            if (mapGen != null) mapGen.GenerateNow();
            InitPlayerAndFireRunStarted(true);
            return;
        }

        ws.ResetNodesToStart();
        mapGen.RespawnEnemiesOnly(); // переспавнить только врагов
        InitPlayerAndFireRunStarted(true);
    }

    // рестарт с новой картой
    public void RestartNewMap()
    {
        if (mapGen != null) mapGen.GenerateNow();
        InitPlayerAndFireRunStarted(false);
    }

    void InitPlayerAndFireRunStarted(bool reuseMap)
    {
        PlayerInventory.Init(startFuel, startHp, startAmmo);
        PlayerWeaponState.Configure(startMag);

        var ws = WorldState.Instance;
        if (ws != null) EventBus.Publish(new PlayerMoved(ws.PlayerSpawn));

        EventBus.Publish(new RunStarted(reuseMap));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) RestartSameMap();
        if (Input.GetKeyDown(KeyCode.F6)) RestartNewMap();
    }
}
