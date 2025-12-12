using System.Collections;
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

    [Header("Victory")]
    [Tooltip("Звук победы (индекс в SoundManager)")]
    [SerializeField] private int winSoundIndex = 8;

    [Tooltip("Через сколько секунд после победы включаем черный экран и открываем Win меню")]
    [SerializeField] private float winBlackoutDelay = 20f;

    // текущая цель забега
    private readonly List<string> objectiveCityNames = new();
    private readonly HashSet<string> remainingCityNames = new();

    private bool initialObjectivesAssigned = false;

    // фикс для новой карты: ждём MapGenerated
    private bool pendingNewMapInit = false;

    // победный стейт, чтобы не стартовать повторно
    private bool victoryTriggered = false;
    private Coroutine winRoutine;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        EventBus.Subscribe<CityCaptured>(OnCityCaptured);
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<CityCaptured>(OnCityCaptured);
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) RestartSameMap();
        if (Input.GetKeyDown(KeyCode.F6)) RestartNewMap();
    }

    // ЕДИНАЯ точка входа после фактической генерации карты
    void OnMapGenerated(MapGenerated _)
    {
        // Первый запуск приложения/сцены
        if (!initialObjectivesAssigned)
        {
            initialObjectivesAssigned = true;
            SetupObjectives();
            AnnounceObjectives();
            InitPlayer(reuseMap: true);
            return;
        }

        // Рестарт с новой картой: задачи/инициализация делаются ТОЛЬКО после MapGenerated
        if (pendingNewMapInit)
        {
            pendingNewMapInit = false;
            SetupObjectives();
            AnnounceObjectives();
            InitPlayer(reuseMap: false);
        }
    }

    // рестарт на той же карте
    public void RestartSameMap()
    {
        StopWinIfRunning();
        EventBus.Publish(new VictoryBlackoutChanged(false)); // на всякий случай (если рестарт с win-экрана)

        victoryTriggered = false;

        var ws = WorldState.Instance;
        if (!ws || mapGen == null)
        {
            if (mapGen != null) mapGen.GenerateNow();
            SetupObjectives();
            AnnounceObjectives();
            InitPlayer(reuseMap: true);
            return;
        }

        ws.ResetNodesToStart();
        mapGen.RespawnEnemiesOnly();
        SetupObjectives();
        AnnounceObjectives();
        InitPlayer(reuseMap: true);
    }

    // рестарт с новой картой
    public void RestartNewMap()
    {
        StopWinIfRunning();
        EventBus.Publish(new VictoryBlackoutChanged(false));

        victoryTriggered = false;

        if (mapGen != null)
        {
            pendingNewMapInit = true;

            // ВАЖНО: НЕ вызываем SetupObjectives здесь — карта ещё не готова
            mapGen.GenerateNow();
        }
        else
        {
            // fallback
            pendingNewMapInit = false;
            SetupObjectives();
            AnnounceObjectives();
            InitPlayer(reuseMap: false);
        }
    }

    void InitPlayer(bool reuseMap)
    {
        PlayerInventory.Init(startFuel, startHp, startAmmo);
        PlayerWeaponState.Configure(startMag);

        var ws = WorldState.Instance;
        if (ws != null) EventBus.Publish(new PlayerMoved(ws.PlayerSpawn));

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
        if (victoryTriggered) return;

        // интересуют именно «цели»
        if (!remainingCityNames.Contains(e.Name)) return;

        remainingCityNames.Remove(e.Name);
        int left = remainingCityNames.Count;
        int total = objectiveCityNames.Count;

        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
            $"Город {e.Name} захвачен. Осталось целей: {left} из {total}."));

        if (left <= 0)
        {
            victoryTriggered = true;

            // Можно оставить одно сообщение о победе, без автоперезапуска
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
                "Все цели захвачены. Победа подтверждена."));

            winRoutine = StartCoroutine(WinSequence());
        }
    }

    IEnumerator WinSequence()
    {
        // 1) звук победы сразу
        if (winSoundIndex >= 0)
            SoundManager.Instance.PlaySound(winSoundIndex);

        // 2) ждём 20 сек (реального времени, не зависит от Time.timeScale)
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, winBlackoutDelay));

        // 3) включаем чёрный экран и открываем Win меню
        EventBus.Publish(new VictoryBlackoutChanged(true));
        EventBus.Publish(new PlayerWon());
    }

    void StopWinIfRunning()
    {
        if (winRoutine != null)
        {
            StopCoroutine(winRoutine);
            winRoutine = null;
        }
        victoryTriggered = false;
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

namespace Events
{
    /// <summary>Победа (для MenuScript.Win())</summary>
    public struct PlayerWon { }

    /// <summary>Включить/выключить чёрный экран победы</summary>
    public readonly struct VictoryBlackoutChanged
    {
        public readonly bool Active;
        public VictoryBlackoutChanged(bool active) { Active = active; }
    }
}
