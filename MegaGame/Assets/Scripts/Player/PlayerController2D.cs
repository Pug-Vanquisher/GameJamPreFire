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

    [Header("Fuel")]
    [Tooltip("Литров на единицу пути (юнит карты)")]
    [SerializeField] private float fuelPerUnit = 0.0008333f;

    [Header("Footsteps")]
    [Tooltip("Индексы звуков шагов в SoundManager")]
    [SerializeField] private int[] stepSoundIndices = new int[] { 6 };
    [Tooltip("Интервал между шагами, пока мы двигаемся")]
    [SerializeField] private float stepInterval = 0.45f;
    [Tooltip("Порог движения (нормализованное |dir|), чтобы считать, что идём")]
    [SerializeField] private float moveThreshold = 0.1f;

    [Header("Self-Destruct Outside Playable Zone")]
    [Tooltip("Задержка до самоуничтожения, когда игрок вне игровой зоны")]
    [SerializeField] private float selfDestructDelay = 10f;
    [Tooltip("Звук запуска самоуничтожения (индекс в SoundManager)")]
    [SerializeField] private int selfDestructStartSound = 7;
    [Tooltip("Заголовок строки прогресса самоуничтожения")]
    [SerializeField] private string selfDestructTitle = "Вы покидаете боевую зону — немедленно возвращайтесь.";

    private bool wasMoving;
    private Coroutine stepLoopCo;
    private bool outOfFuelRaised;

    // состояние самоуничтожения
    private Coroutine selfDestructCo;
    private bool selfDestructActive;

    void OnEnable()
    {
        EventBus.Subscribe<MapGenerated>(OnMapGenerated);
        EventBus.Subscribe<RunStarted>(OnRunStarted);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<MapGenerated>(OnMapGenerated);
        EventBus.Unsubscribe<RunStarted>(OnRunStarted);
    }

    void Start()
    {
        PlayerState.Speed = speed;

        // страховка инвентаря для пустых значений
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

        // 1) Тихо гасим возможное самоуничтожение с прошлого рана (без логов).
        CancelSelfDestruct(silent: true);

        // 2) Обновляем параметры расхода топлива.
        PlayerInventory.ConfigureFuelConsumption(fuelPerUnit);

        // 3) Возвращаем игрока на точку спавна текущего забега.
        if (ws != null && ws.PlayerBase != null)
            SnapTo(ws.PlayerSpawn);

        // 4) После снапа гарантированно снимаем блокировку консоли (без доп. сообщений).
        if (ws != null && ws.IsInsidePlayable(PlayerState.Pos))
            EventBus.Publish(new SelfDestructLockChanged(false));
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

        // ===== ограничение карты + самоуничтожение =====
        bool inside = ws.IsInsidePlayable(PlayerState.Pos);
        if (!inside && selfDestructCo == null)
        {
            selfDestructCo = StartCoroutine(SelfDestructRoutine());
        }
        else if (inside && selfDestructCo != null)
        {
            // вернулись внутрь — отменяем с сообщением
            CancelSelfDestruct(silent: false);
        }

        // ===== если топлива нет — не двигаемся, один раз шлём событие =====
        if (PlayerInventory.Fuel <= 0f)
        {
            StopStepLoop();
            if (!outOfFuelRaised)
            {
                outOfFuelRaised = true;
                EventBus.Publish(new PlayerOutOfFuel());
                SoundManager.Instance.PlaySound(2); // короткий «тык»
            }
            return;
        }
        else
        {
            outOfFuelRaised = false;
        }

        // ===== управление WASD/стрелки =====
        float h = Input.GetAxisRaw("Horizontal");   // A/D, Left/Right
        float v = Input.GetAxisRaw("Vertical");     // W/S, Up/Down
        Vector2 dir = new Vector2(h, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        bool moving = dir.sqrMagnitude > (moveThreshold * moveThreshold);

        if (!moving)
        {
            StopStepLoop();
            wasMoving = false;
            return;
        }

        if (!wasMoving) StartStepLoop();
        wasMoving = true;

        Vector2 delta = dir * (PlayerState.Speed * Time.deltaTime);
        float dist = delta.magnitude;

        // расход топлива пропорционален пройденной дистанции
        PlayerInventory.ConsumeFuelByDistance(dist);

        float half = ws.MapHalfSize;
        PlayerState.Pos = new Vector2(
            Mathf.Clamp(PlayerState.Pos.x + delta.x, -half, half),
            Mathf.Clamp(PlayerState.Pos.y + delta.y, -half, half)
        );

        EventBus.Publish(new PlayerMoved(PlayerState.Pos));
    }

    // === корутина самоуничтожения ===
    private System.Collections.IEnumerator SelfDestructRoutine()
    {
        selfDestructActive = true;

        // 1) Блокируем консоль (консоль сама выведет, что она заблокирована).
        EventBus.Publish(new SelfDestructLockChanged(true));

        // 2) Особый звук старта
        if (selfDestructStartSound >= 0) SoundManager.Instance.PlaySound(selfDestructStartSound);

        // 3) Одна «команда с прогрессом» (10 сек по умолчанию) —
        // текст строго по требованию: «Вы покидаете боевую зону — немедленно возвращайтесь.»
        float delay = Mathf.Max(0.01f, selfDestructDelay);
        EventBus.Publish(new CommandExecutionStarted(selfDestructTitle, delay));

        float end = Time.time + delay;
        while (Time.time < end)
        {
            // Если вернулись, нас отменят извне через CancelSelfDestruct()
            yield return null;
        }

        // Если корутина не была отменена — смерть
        if (selfDestructActive)
        {
            selfDestructActive = false;
            selfDestructCo = null;

            EventBus.Publish(new CommandExecutionFinished(selfDestructTitle, selfDestructTitle));
            EventBus.Publish(new SelfDestructLockChanged(false)); // снять блок консоли
            EventBus.Publish(new PlayerDied());
        }
    }

    private void CancelSelfDestruct(bool silent)
    {
        if (selfDestructCo != null)
        {
            StopCoroutine(selfDestructCo);
            selfDestructCo = null;
        }
        if (selfDestructActive)
        {
            selfDestructActive = false;

            // Закрываем прогресс-бар и разблокируем консоль.
            EventBus.Publish(new CommandExecutionFinished(selfDestructTitle, selfDestructTitle));
            EventBus.Publish(new SelfDestructLockChanged(false));

            // При обычной отмене пишем понятный лог; при «тихой» (рестарт) — ничего не пишем.
            if (!silent)
            {
                EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, "Самоуничтожение отменено. Вы вернулись в боевую зону."));
            }
        }
    }

    // === шаги ===
    void StartStepLoop()
    {
        if (stepLoopCo != null) return;
        stepLoopCo = StartCoroutine(StepLoop());
    }
    void StopStepLoop()
    {
        if (stepLoopCo != null) { StopCoroutine(stepLoopCo); stepLoopCo = null; }
    }
    System.Collections.IEnumerator StepLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, stepInterval));
        while (true)
        {
            // safety: не играть, если топлива нет
            if (PlayerInventory.Fuel <= 0f) yield break;

            if (stepSoundIndices != null && stepSoundIndices.Length > 0)
            {
                int idx = stepSoundIndices[Random.Range(0, stepSoundIndices.Length)];
                SoundManager.Instance.PlaySound(idx);
            }
            yield return wait;
        }
    }
}

namespace Events
{
    /// <summary>Топливо закончилось — движение запрещено.</summary>
    public struct PlayerOutOfFuel { }

    /// <summary>Включить/выключить блокировку команд консоли из-за самоуничтожения.</summary>
    public readonly struct SelfDestructLockChanged
    {
        public readonly bool Active;
        public SelfDestructLockChanged(bool active) { Active = active; }
    }
}
