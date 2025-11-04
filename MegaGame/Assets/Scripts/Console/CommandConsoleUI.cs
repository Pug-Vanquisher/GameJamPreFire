
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using Events;

public class CommandConsoleUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text screen;
    [SerializeField] private bool autoFocus = true;

    [Header("Scan settings")]
    [SerializeField] private float scanRange = 1500f;
    [SerializeField] private int maxReportPerType = 3;

    [Header("Combat balance")]
    [SerializeField, Range(0f, 1f)] private float hitChance = 0.55f;
    [SerializeField, Range(0f, 1f)] private float killChance = 0.40f;
    [SerializeField] private Vector2Int ammoSpentRange = new Vector2Int(1, 3);

    [Header("Delays (sec)")]
    [SerializeField] private float delayScan = 0.8f;
    [SerializeField] private float delayReload = 0.9f;
    [SerializeField] private float delayDiagnostics = 0.6f;
    [SerializeField] private float delayAttack = 0.7f;

    [Header("Targeting")]
    [SerializeField] private bool useRadarRangeForTargets = true;

    private enum Menu { Root, Combat, Diagnostics, AttackList, Move }
    private readonly Stack<Menu> stack = new();

    private struct TargetInfo { public string id; public string label; public Vector2 pos; }
    private readonly List<TargetInfo> attackTargets = new();

    private bool busy;
    private string busyTitle;

    // локальный флаг, чтобы не слать лишние ивенты
    private bool moveModeActive = false;
    private void SetMoveMode(bool active)
    {
        if (moveModeActive == active) return;
        moveModeActive = active;
        EventBus.Publish(new ConsoleMoveModeChanged(active));
    }

    void OnEnable()
    {
        EventBus.Subscribe<VisibleTargetsChanged>(OnVisibleTargetsChanged);
        EventBus.Subscribe<RunStarted>(OnRunStarted);
        if (screen) RenderMenu(Menu.Root);
        SetMoveMode(false);
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<VisibleTargetsChanged>(OnVisibleTargetsChanged);
        EventBus.Unsubscribe<RunStarted>(OnRunStarted);
        SetMoveMode(false);
    }

    void OnRunStarted(RunStarted _)
    {
        stack.Clear();
        RenderMenu(Menu.Root);
        SetMoveMode(false);
    }

    void Update()
    {
        if (!autoFocus) return;

        for (int d = 0; d <= 9; d++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d))
                PressDigit(d);

        if (CurrentMenu == Menu.Move)
        {
            Vector2 dir = Vector2.zero;
            if (Input.GetKey(KeyCode.Keypad8)) dir += Vector2.up;     // Север
            if (Input.GetKey(KeyCode.Keypad2)) dir += Vector2.down;   // Юг
            if (Input.GetKey(KeyCode.Keypad6)) dir += Vector2.right;  // Восток
            if (Input.GetKey(KeyCode.Keypad4)) dir += Vector2.left;   // Запад

            if (Input.GetKey(KeyCode.UpArrow)) dir += Vector2.up;
            if (Input.GetKey(KeyCode.DownArrow)) dir += Vector2.down;
            if (Input.GetKey(KeyCode.RightArrow)) dir += Vector2.right;
            if (Input.GetKey(KeyCode.LeftArrow)) dir += Vector2.left;

            if (dir.sqrMagnitude > 1f) dir.Normalize();
            EventBus.Publish(new ConsoleMoveInput(dir));
        }
    }

    void OnVisibleTargetsChanged(VisibleTargetsChanged _)
    {
        if (CurrentMenu == Menu.AttackList || CurrentMenu == Menu.Combat)
        {
            BuildAttackTargetsFromRegistry();
            RenderMenu(CurrentMenu);
        }
    }

    public void PressDigit(int d)
    {
        var m = CurrentMenu;
        if (d == 0) { if (m != Menu.Root) { stack.Pop(); RenderMenu(CurrentMenu); SetMoveMode(CurrentMenu == Menu.Move); } return; }

        switch (m)
        {
            case Menu.Root: HandleRoot(d); break;
            case Menu.Combat: HandleCombat(d); break;
            case Menu.Diagnostics: HandleDiagnostics(d); break;
            case Menu.AttackList: HandleAttackList(d); break;
            case Menu.Move: HandleMoveMenu(d); break;
        }
    }

    Menu CurrentMenu => stack.Count == 0 ? Menu.Root : stack.Peek();

    void RenderMenu(Menu m)
    {
        if (!screen) return;
        var sb = new StringBuilder();

        if (m == Menu.Root)
        {
            sb.AppendLine("[1] Осмотр");
            sb.AppendLine("[2] Бой >");
            sb.AppendLine("[3] Диагностика >");
            sb.AppendLine("[4] Движение >");
            sb.AppendLine("[5] Задачи");
            sb.AppendLine();
            sb.AppendLine("[9] Прокрутка логов ↑");
            sb.AppendLine("[6] Прокрутка логов ↓");
        }
        else if (m == Menu.Combat)
        {
            sb.AppendLine("[1] Атаковать >");
            sb.AppendLine("[2] Перезарядка");
            sb.AppendLine("[0] Назад");
        }
        else if (m == Menu.Diagnostics)
        {
            sb.AppendLine("[1] Состояние корпуса");
            sb.AppendLine("[2] Боезапас");
            sb.AppendLine("[3] Бензобак");
            sb.AppendLine("[0] Назад");
        }
        else if (m == Menu.AttackList)
        {
            if (attackTargets.Count == 0) sb.AppendLine("Целей нет в радиусе.");
            else for (int i = 0; i < attackTargets.Count && i < 9; i++)
                    sb.AppendLine($"[{i + 1}] Атаковать цель {attackTargets[i].label}");
            sb.AppendLine("[0] Назад");
        }
        else if (m == Menu.Move)
        {
            sb.AppendLine("[8] Север  (удерживать)");
            sb.AppendLine("[2] Юг     (удерживать)");
            sb.AppendLine("[6] Восток (удерживать)");
            sb.AppendLine("[4] Запад  (удерживать)");
            sb.AppendLine("[0] Назад");
        }

        screen.text = sb.ToString();
    }

    void HandleRoot(int d)
    {
        if (d == 1) { StartCommand("Осмотр", delayScan, DoScanNow); return; }
        if (d == 2) { stack.Push(Menu.Combat); RenderMenu(Menu.Combat); SetMoveMode(false); return; }
        if (d == 3) { stack.Push(Menu.Diagnostics); RenderMenu(Menu.Diagnostics); SetMoveMode(false); return; }
        if (d == 4) { stack.Push(Menu.Move); RenderMenu(Menu.Move); SetMoveMode(true); return; }

        if (d == 5) { GameRunManager.Instance?.AnnounceObjectives(); return; }

        if (d == 9) { EventBus.Publish(new ConsoleScrollRequest(+1f)); return; }
        if (d == 6) { EventBus.Publish(new ConsoleScrollRequest(-1f)); return; }
    }

    void HandleCombat(int d)
    {
        if (d == 1)
        {
            BuildAttackTargetsFromRegistry();
            stack.Push(Menu.AttackList);
            RenderMenu(Menu.AttackList);
            SetMoveMode(false);
            return;
        }
        if (d == 2)
        {
            StartCommand("Перезарядка", delayReload, () =>
            {
                bool ok = PlayerWeaponState.Reload();
                string msg = ok
                    ? $"Перезарядка: {PlayerWeaponState.InMag}/{PlayerWeaponState.MagSize}. Остаток боезапаса: {PlayerInventory.Ammo}"
                    : "Перезарядка невозможна: магазин полон или нет боеприпасов.";
                SoundManager.Instance.PlaySound(4);
                EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, msg));
            });
            return;
        }
    }

    void HandleDiagnostics(int d)
    {
        if (d is >= 1 and <= 3)
        {
            string title = d switch { 1 => "Диагностика: Корпус", 2 => "Диагностика: Боезапас", _ => "Диагностика: Топливо" };
            StartCommand(title, delayDiagnostics, () =>
            {
                var kind = d == 1 ? DiagKind.Health : d == 2 ? DiagKind.Ammo : DiagKind.Fuel;
                EventBus.Publish(new PlayerDiagnosticsReport(
                    kind,
                    PlayerInventory.Health, PlayerInventory.MaxHealth,
                    PlayerWeaponState.InMag, PlayerWeaponState.MagSize,
                    PlayerInventory.Ammo, PlayerInventory.MaxAmmo,
                    PlayerInventory.Fuel, PlayerInventory.MaxFuel
                ));
            });
        }
    }

    void HandleAttackList(int d)
    {
        int idx = d - 1;
        if ((uint)idx >= (uint)attackTargets.Count) return;
        var t = attackTargets[idx];
        StartCommand($"Атака {t.label}", delayAttack, () => PerformFireAtTarget(t));
    }

    void HandleMoveMenu(int d)
    {
        if (d == 0) { stack.Pop(); RenderMenu(CurrentMenu); SetMoveMode(false); }
    }

    // ====== задержки команд ======
    void StartCommand(string title, float delay, Action onDone)
    {
        if (busy)
        {
            SoundManager.Instance.PlaySound(2);
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, $"Выполняется: {busyTitle}..."));
            return;
        }
        busy = true; busyTitle = title;

        SoundManager.Instance.PlaySound(5);
        EventBus.Publish(new CommandExecutionStarted(title, delay));
        StartCoroutine(Delayed());

        System.Collections.IEnumerator Delayed()
        {
            float t = Time.time + Mathf.Max(0f, delay);
            while (Time.time < t) yield return null;

            try { onDone?.Invoke(); }
            finally
            {
                EventBus.Publish(new CommandExecutionFinished(title, title));
                busy = false; busyTitle = null;
            }
        }
    }

    // ====== бой ======
    void PerformFireAtTarget(TargetInfo t)
    {
        int lo = Mathf.Min(ammoSpentRange.x, ammoSpentRange.y);
        int hi = Mathf.Max(ammoSpentRange.x, ammoSpentRange.y);
        int spentReq = Mathf.Clamp(UnityEngine.Random.Range(lo, hi + 1), 1, 999);

        if (!PlayerWeaponState.CanSpend(spentReq))
        {
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
                $"Недостаточно патронов в магазине ({PlayerWeaponState.InMag}). Требуется {spentReq}. Перезарядка [2]."));
            return;
        }

        PlayerWeaponState.TrySpend(spentReq);
        EventBus.Publish(new PlayerFired(PlayerState.Pos));
        SoundManager.Instance.PlaySound(0);
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
            $"Огонь по {t.label}: расход {spentReq}. {PlayerWeaponState.InMag}/{PlayerWeaponState.MagSize} в магазине."));

        bool hit = UnityEngine.Random.value < hitChance;
        if (!hit) { EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, $"Промах по цели {t.label}.")); return; }

        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, $"Попадание по {t.label}."));

        bool kill = UnityEngine.Random.value < killChance;
        if (kill)
        {
            bool ok = AISystem.KillSquadById(t.id, t.pos);
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, ok
                ? $"Цель {t.label} уничтожена."
                : $"Цель {t.label} потеряна (не найдена)."));
        }
        else
        {
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, $"Цель {t.label} получила повреждения, остаётся боеспособной."));
        }
    }

    // ====== цели для атаки ======
    void BuildAttackTargetsFromRegistry()
    {
        var ws = WorldState.Instance; attackTargets.Clear();
        if (!ws) return;

        var vis = CombatTargetRegistry.GetVisibleOrdered(); // (id, idx)
        foreach (var (id, idx) in vis)
        {
            var s = ws.EnemySquads.FirstOrDefault(q => q.Id == id);
            if (s == null) continue;
            attackTargets.Add(new TargetInfo { id = id, label = CombatTargetRegistry.Nato(idx), pos = s.Pos });
            if (attackTargets.Count >= 9) break;
        }
        if (attackTargets.Count == 0)
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, "Целей для атаки не обнаружено."));
    }

    // ====== осмотр ======
    void DoScanNow()
    {
        var ws = WorldState.Instance; if (!ws) return;
        Vector2 me = PlayerState.Pos;

        var roadBest = new Dictionary<int, (int dir, float dist)>();
        foreach (var r in ws.Roads)
        {
            if (r.Path == null || r.Path.Count < 2) continue;
            float best = float.MaxValue; Vector2 bestP = me;
            for (int i = 0; i < r.Path.Count - 1; i++)
            {
                Vector2 p = ClosestPointOnSegment(me, r.Path[i], r.Path[i + 1]);
                float d = Vector2.Distance(me, p);
                if (d < best) { best = d; bestP = p; }
            }
            if (best <= scanRange)
            {
                int dir = Dir8Index(me, bestP);
                if (!roadBest.TryGetValue(r.Id, out var cur) || best < cur.dist)
                    roadBest[r.Id] = (dir, best);
            }
        }
        var roadDirs = PickDirsDistinct(roadBest.Values.ToList(), maxReportPerType);

        var cityDirs = PickDirsDistinct(ws.Cities.Select(c => (Dir8Index(me, c.Pos), Vector2.Distance(me, c.Pos)))
            .Where(t => t.Item2 <= scanRange).ToList(), maxReportPerType);

        var campDirs = PickDirsDistinct(ws.Camps.Select(c => (Dir8Index(me, c.Pos), Vector2.Distance(me, c.Pos)))
            .Where(t => t.Item2 <= scanRange).ToList(), maxReportPerType);

        var enemyDirs = PickDirsDistinct(ws.EnemySquads.Select(e => (Dir8Index(me, e.Pos), Vector2.Distance(me, e.Pos)))
            .Where(t => t.Item2 <= scanRange).ToList(), maxReportPerType);

        EventBus.Publish(new PlayerScanReport(ws.SampleBiome(me),
            roadDirs.ToArray(), cityDirs.ToArray(), campDirs.ToArray(), enemyDirs.ToArray()));
    }

    static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    List<int> PickDirsDistinct(List<(int dir, float dist)> cand, int max)
    {
        cand.Sort((a, b) => a.dist.CompareTo(b.dist));
        var result = new List<int>(max);
        bool[] blocked = new bool[8];
        foreach (var (dir, _) in cand)
        {
            if (blocked[dir]) continue;
            result.Add(dir);
            blocked[dir] = true; blocked[(dir + 1) & 7] = true; blocked[(dir + 7) & 7] = true;
            if (result.Count >= max) break;
        }
        return result;
    }

    int Dir8Index(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from; if (d.sqrMagnitude < 1e-6f) return 0;
        float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
        return Mathf.RoundToInt(ang / 45f) & 7;
    }
}

namespace Events
{
    public struct ConsoleMoveModeChanged
    {
        public bool Active;
        public ConsoleMoveModeChanged(bool active) { Active = active; }
    }
}