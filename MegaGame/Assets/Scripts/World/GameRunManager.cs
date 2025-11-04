using System.Collections.Generic;
using System.Linq;
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

    [Header("Mission")]
    [Tooltip("Сколько городов нужно захватить (столица не учитывается)")]
    [SerializeField] private int objectiveCitiesCount = 4;

    // текущая цель забега
    private readonly List<string> objectiveCityNames = new();
    private readonly HashSet<string> remainingCityNames = new();

    // чтобы назначить задачи один раз при самом первом запуске сцены
    private bool initialObjectivesAssigned = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        EventBus.Subscribe<CityCaptured>(OnCityCaptured);
        EventBus.Subscribe<MapGenerated>(OnMapGenerated_FirstBoot);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<CityCaptured>(OnCityCaptured);
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated_FirstBoot);
    }

    void Update()
    {
        // горячие клавиши на ресеты (по желанию)
        if (Input.GetKeyDown(KeyCode.F5)) RestartSameMap();
        if (Input.GetKeyDown(KeyCode.F6)) RestartNewMap();
    }

    // --- первичный игровой цикл: первый MapGenerated этой сессии ---
    void OnMapGenerated_FirstBoot(MapGenerated _)
    {
        if (initialObjectivesAssigned) return;
        initialObjectivesAssigned = true;

        // карта готова → назначаем задачи и объявляем их
        SetupObjectives();
        AnnounceObjectives();

        // инициализация состояния игрока (топливо/хп/патроны/позиция)
        InitPlayer(reuseMap: true);
    }

    // рестарт на той же карте
    public void RestartSameMap()
    {
        var ws = WorldState.Instance;
        if (!ws || mapGen == null)
        {
            if (mapGen != null) mapGen.GenerateNow();
            SetupObjectives();
            AnnounceObjectives();
            InitPlayer(reuseMap: true);
            return;
        }

        ws.ResetNodesToStart();          // сброс ресурсов узлов и статусов
        mapGen.RespawnEnemiesOnly();     // переспавнить врагов
        SetupObjectives();               // новые цели на ту же карту
        AnnounceObjectives();            // и объявить их
        InitPlayer(reuseMap: true);
    }

    // рестарт с новой картой
    public void RestartNewMap()
    {
        if (mapGen != null) mapGen.GenerateNow(); // сгенерит карту и вызовет MapGenerated (но мы уже initialObjectivesAssigned=true)
        SetupObjectives();
        AnnounceObjectives();
        InitPlayer(reuseMap: false);
    }

    void InitPlayer(bool reuseMap)
    {
        PlayerInventory.Init(startFuel, startHp, startAmmo);
        PlayerWeaponState.Configure(startMag);

        var ws = WorldState.Instance;
        if (ws != null) EventBus.Publish(new PlayerMoved(ws.PlayerSpawn));

        // Событие оставляем для внутренних подписчиков (обновить UI, меню и т.п.),
        // но НИЧЕГО не логируем «Новый забег начался».
        EventBus.Publish(new RunStarted(reuseMap));
    }

    // ---------------- Миссия/цели ----------------

    void SetupObjectives()
    {
        objectiveCityNames.Clear();
        remainingCityNames.Clear();

        var ws = WorldState.Instance;
        if (!ws) { Debug.LogWarning("[GameRun] WorldState not ready, mission skipped."); return; }

        // пул городов без столицы
        var pool = ws.Cities.Where(c => ws.Capital == null || c.Id != ws.Capital.Id).ToList();
        if (pool.Count == 0) { Debug.LogWarning("[GameRun] No cities to make objectives."); return; }

        // перетасовать и взять N
        int need = Mathf.Clamp(objectiveCitiesCount, 1, pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < need; i++)
        {
            string name = pool[i].Name;
            objectiveCityNames.Add(name);
            remainingCityNames.Add(name);
        }
    }

    void OnCityCaptured(CityCaptured e)
    {
        // интересуют именно «цели»
        if (!remainingCityNames.Contains(e.Name)) return;

        remainingCityNames.Remove(e.Name);
        int left = remainingCityNames.Count;
        int total = objectiveCityNames.Count;

        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
            $"Город {e.Name} захвачен. Осталось целей: {left} из {total}."));

        if (left <= 0)
        {
            EventBus.Publish(new WinCondition());
        }
    }

    System.Collections.IEnumerator RestartSoon()
    {
        yield return new WaitForSeconds(2.0f);
        RestartSameMap();
    }

    // Текст задач
    public string BuildObjectivesLine()
    {
        if (objectiveCityNames.Count == 0) return "Боевых задач нет.";
        var list = string.Join(", ", objectiveCityNames);
        return $"Ваши боевые задачи: захватить города — {list}.";
    }

    // Сказать задачи в консоль
    public void AnnounceObjectives()
    {
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, BuildObjectivesLine()));

        if (remainingCityNames.Count > 0 && remainingCityNames.Count < objectiveCityNames.Count)
        {
            int left = remainingCityNames.Count;
            int total = objectiveCityNames.Count;
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
                $"Прогресс: осталось {left} из {total}."));
        }
    }
}
