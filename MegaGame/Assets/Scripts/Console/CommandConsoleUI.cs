// CommandConsoleUI.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Events;

public class CommandConsoleUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text screen;      // поле текста для списка команд
    [SerializeField] private bool autoFocus = true; // если true — всегда принимает ввод цифр

    [Header("Scan settings")]
    [SerializeField] private float scanRange = 1500f;     // радиус осмотра
    [SerializeField] private float nearThreshold = 0.5f;  // доля от радиуса, что считаем «близко»
    [SerializeField] private int maxReportPerType = 3;    // сколько объектов каждого типа перечислять

    [Header("Reload defaults")]
    [SerializeField] private int defaultMagSize = 10;

    private enum Menu { Root, Combat, Diagnostics, AttackList }
    private readonly Stack<Menu> stack = new();
    private List<(string label, Vector2 pos)> attackTargets = new();

    void OnEnable()
    {
        if (screen) RenderMenu(Menu.Root);
        // инициализируем оружие, если не было
        PlayerWeaponState.Configure(defaultMagSize);
    }

    void Update()
    {
        if (!autoFocus) return;
        // цифры верхнего ряда
        if (Input.GetKeyDown(KeyCode.Alpha0)) PressDigit(0);
        if (Input.GetKeyDown(KeyCode.Alpha1)) PressDigit(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) PressDigit(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) PressDigit(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) PressDigit(4);
        if (Input.GetKeyDown(KeyCode.Alpha5)) PressDigit(5);
        if (Input.GetKeyDown(KeyCode.Alpha6)) PressDigit(6);
        if (Input.GetKeyDown(KeyCode.Alpha7)) PressDigit(7);
        if (Input.GetKeyDown(KeyCode.Alpha8)) PressDigit(8);
        if (Input.GetKeyDown(KeyCode.Alpha9)) PressDigit(9);
        // numpad
        if (Input.GetKeyDown(KeyCode.Keypad0)) PressDigit(0);
        if (Input.GetKeyDown(KeyCode.Keypad1)) PressDigit(1);
        if (Input.GetKeyDown(KeyCode.Keypad2)) PressDigit(2);
        if (Input.GetKeyDown(KeyCode.Keypad3)) PressDigit(3);
        if (Input.GetKeyDown(KeyCode.Keypad4)) PressDigit(4);
        if (Input.GetKeyDown(KeyCode.Keypad5)) PressDigit(5);
        if (Input.GetKeyDown(KeyCode.Keypad6)) PressDigit(6);
        if (Input.GetKeyDown(KeyCode.Keypad7)) PressDigit(7);
        if (Input.GetKeyDown(KeyCode.Keypad8)) PressDigit(8);
        if (Input.GetKeyDown(KeyCode.Keypad9)) PressDigit(9);
    }

    // можно вызывать из «физического нампада» кликом мыши
    public void PressDigit(int d)
    {
        var current = stack.Count == 0 ? Menu.Root : stack.Peek();
        if (d == 0)
        {
            if (current == Menu.Root) return;
            stack.Pop();
            RenderMenu(stack.Count == 0 ? Menu.Root : stack.Peek());
            return;
        }

        switch (current)
        {
            case Menu.Root: HandleRoot(d); break;
            case Menu.Combat: HandleCombat(d); break;
            case Menu.Diagnostics: HandleDiagnostics(d); break;
            case Menu.AttackList: HandleAttackList(d); break;
        }
    }

    // ---------- Меню и обработчики ----------
    void RenderMenu(Menu m)
    {
        if (screen == null) return;
        var sb = new StringBuilder();

        if (m == Menu.Root)
        {
            sb.AppendLine("[1] Осмотр");
            sb.AppendLine("[2] Бой >");
            sb.AppendLine("[3] Диагностика >");
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
            if (attackTargets.Count == 0)
                sb.AppendLine("Целей нет в радиусе.");
            else
            {
                for (int i = 0; i < attackTargets.Count && i < 9; i++)
                    sb.AppendLine($"[{i + 1}] {attackTargets[i].label}");
            }
            sb.AppendLine("[0] Назад");
        }

        screen.text = sb.ToString();
    }

    void HandleRoot(int d)
    {
        if (d == 1) { DoScan(); return; }
        if (d == 2) { stack.Push(Menu.Combat); RenderMenu(Menu.Combat); return; }
        if (d == 3) { stack.Push(Menu.Diagnostics); RenderMenu(Menu.Diagnostics); return; }
    }

    void HandleCombat(int d)
    {
        if (d == 1)
        {
            BuildAttackTargets();
            stack.Push(Menu.AttackList);
            RenderMenu(Menu.AttackList);
            return;
        }
        if (d == 2)
        {
            bool ok = PlayerWeaponState.Reload();
            if (ok)
            {
                EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
                    $"Перезарядка: {PlayerWeaponState.InMag}/{PlayerWeaponState.MagSize}. Остаток боезапаса: {PlayerInventory.Ammo}"));
            }
            else
            {
                EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
                    "Перезарядка невозможна: магазин полон или нет боеприпасов."));
            }
            return;
        }
    }

    void HandleDiagnostics(int d)
    {
        if (d == 1)
        {
            EventBus.Publish(new PlayerDiagnosticsReport(
                DiagKind.Health,
                PlayerInventory.Health, PlayerInventory.MaxHealth,
                PlayerWeaponState.InMag, PlayerWeaponState.MagSize,
                PlayerInventory.Ammo, PlayerInventory.MaxAmmo,
                PlayerInventory.Fuel, PlayerInventory.MaxFuel
            ));
            return;
        }
        if (d == 2)
        {
            EventBus.Publish(new PlayerDiagnosticsReport(
                DiagKind.Ammo,
                PlayerInventory.Health, PlayerInventory.MaxHealth,
                PlayerWeaponState.InMag, PlayerWeaponState.MagSize,
                PlayerInventory.Ammo, PlayerInventory.MaxAmmo,
                PlayerInventory.Fuel, PlayerInventory.MaxFuel
            ));
            return;
        }
        if (d == 3)
        {
            EventBus.Publish(new PlayerDiagnosticsReport(
                DiagKind.Fuel,
                PlayerInventory.Health, PlayerInventory.MaxHealth,
                PlayerWeaponState.InMag, PlayerWeaponState.MagSize,
                PlayerInventory.Ammo, PlayerInventory.MaxAmmo,
                PlayerInventory.Fuel, PlayerInventory.MaxFuel
            ));
            return;
        }
    }

    void HandleAttackList(int d)
    {
        int idx = d - 1;
        if (idx < 0 || idx >= attackTargets.Count) return;

        var t = attackTargets[idx];
        // здесь позже подключим боевую систему
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot,
            $"Цель «{t.label}»: наведение… (боевой модуль в разработке)"));
    }

    // ---------- Логика команд ----------
    void DoScan()
    {
        var ws = WorldState.Instance; if (!ws) return;
        Vector2 me = PlayerState.Pos;

        // Биом в точке игрока
        var biome = ws.SampleBiome(me);

        // ---- ДОРОГИ: кандидат = ближайшая точка на каждой дороге ----
        var roadBestById = new Dictionary<int, (int dir, float dist)>();
        foreach (var road in ws.Roads)
        {
            if (road.Path == null || road.Path.Count < 2) continue;
            float best = float.MaxValue; Vector2 bestP = me;
            for (int i = 0; i < road.Path.Count - 1; i++)
            {
                Vector2 p = ClosestPointOnSegment(me, road.Path[i], road.Path[i + 1]);
                float d = Vector2.Distance(me, p);
                if (d < best) { best = d; bestP = p; }
            }
            if (best <= scanRange)
            {
                int dir = Dir8Index(me, bestP);
                var tuple = (dir, best);
                if (!roadBestById.TryGetValue(road.Id, out var cur) || best < cur.dist)
                    roadBestById[road.Id] = tuple;
            }
        }
        var roadCandidates = new List<(int dir, float dist)>(roadBestById.Values);
        var roadDirs = PickDirsDistinct(roadCandidates, maxReportPerType);

        // ---- ГОРОДА ----
        var cityCandidates = new List<(int dir, float dist)>();
        foreach (var n in ws.Cities)
        {
            float d = Vector2.Distance(me, n.Pos);
            if (d > scanRange) continue;
            cityCandidates.Add((Dir8Index(me, n.Pos), d));
        }
        var cityDirs = PickDirsDistinct(cityCandidates, maxReportPerType);

        // ---- ЛАГЕРЯ ----
        var campCandidates = new List<(int dir, float dist)>();
        foreach (var n in ws.Camps)
        {
            float d = Vector2.Distance(me, n.Pos);
            if (d > scanRange) continue;
            campCandidates.Add((Dir8Index(me, n.Pos), d));
        }
        var campDirs = PickDirsDistinct(campCandidates, maxReportPerType);

        // ---- ПРОТИВНИКИ ----
        var enemyCandidates = new List<(int dir, float dist)>();
        foreach (var e in ws.EnemySquads)
        {
            float d = Vector2.Distance(me, e.Pos);
            if (d > scanRange) continue;
            enemyCandidates.Add((Dir8Index(me, e.Pos), d));
        }
        var enemyDirs = PickDirsDistinct(enemyCandidates, maxReportPerType);

        // Отправляем ДАННЫЕ в композер
        EventBus.Publish(new PlayerScanReport(
            biome,
            roadDirs.ToArray(),
            cityDirs.ToArray(),
            campDirs.ToArray(),
            enemyDirs.ToArray()
        ));
    }

    (Vector2 pos, float dist) FindNearestRoadPoint(WorldState ws, Vector2 me)
    {
        float best = float.MaxValue; Vector2 bestP = me;
        foreach (var r in ws.Roads)
        {
            var path = r.Path; if (path == null || path.Count < 2) continue;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p = ClosestPointOnSegment(me, path[i], path[i + 1]);
                float d = Vector2.Distance(me, p);
                if (d < best) { best = d; bestP = p; }
            }
        }
        return (bestP, best);
    }

    List<(string label, Vector2 pos)> NearestNodes(List<NodeData> nodes, Vector2 me, float range, int max)
    {
        var res = new List<(string label, Vector2 pos)>();
        foreach (var n in nodes)
        {
            float d = Vector2.Distance(me, n.Pos);
            if (d <= range) res.Add((label: n.Name, pos: n.Pos));
        }
        res.Sort((a, b) => Vector2.Distance(me, a.pos).CompareTo(Vector2.Distance(me, b.pos)));
        if (res.Count > max) res.RemoveRange(max, res.Count - max);
        return res;
    }

    List<(string label, Vector2 pos)> NearestSquads(WorldState ws, Vector2 me, float range, int max)
    {
        var res = new List<(string label, Vector2 pos)>();
        foreach (var s in ws.EnemySquads)
        {
            float d = Vector2.Distance(me, s.Pos);
            if (d <= range) res.Add((label: $"группа {s.Id}", pos: s.Pos));
        }
        res.Sort((a, b) => Vector2.Distance(me, a.pos).CompareTo(Vector2.Distance(me, b.pos)));
        if (res.Count > max) res.RemoveRange(max, res.Count - max);
        return res;
    }

    void BuildAttackTargets()
    {
        var ws = WorldState.Instance; if (!ws) { attackTargets.Clear(); return; }
        Vector2 me = PlayerState.Pos;
        attackTargets = NearestSquads(ws, me, scanRange, 9);
        if (attackTargets.Count == 0)
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, "Целей для атаки не обнаружено."));
    }

    // ---------- Геометрия / направление ----------
    static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    // выбирает до max направлений, блокируя соседние сектора
    List<int> PickDirsDistinct(List<(int dir, float dist)> candidates, int max)
    {
        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
        var result = new List<int>(max);
        bool[] blocked = new bool[8];

        foreach (var (dir, _) in candidates)
        {
            if (blocked[dir]) continue;
            result.Add(dir);
            blocked[dir] = true;
            blocked[(dir + 1) & 7] = true;   // сосед справа
            blocked[(dir + 7) & 7] = true;   // сосед слева
            if (result.Count >= max) break;
        }
        return result;
    }

    int Dir8Index(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        if (d.sqrMagnitude < 1e-6f) return 0; // север по умолчанию
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; // 0° = восток
        ang = (ang + 360f + 90f) % 360f; // 0° = север
        return Mathf.RoundToInt(ang / 45f) % 8;
    }
}
