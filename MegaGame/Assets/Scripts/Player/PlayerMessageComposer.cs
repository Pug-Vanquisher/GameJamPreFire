using System.Text;
using UnityEngine;
using Events;

public class PlayerMessageComposer : MonoBehaviour
{
    [SerializeField] private PhraseBank phrases;

    void OnEnable()
    {
        EventBus.Subscribe<CityCaptured>(OnCityCaptured);
        EventBus.Subscribe<PlayerScanReport>(OnPlayerScanReport);
        EventBus.Subscribe<PlayerDiagnosticsReport>(OnPlayerDiagnosticsReport);
        EventBus.Subscribe<CampDestroyed>(OnCampDestroyed);       
    }
    void OnDisable()
    {
        EventBus.Unsubscribe<CityCaptured>(OnCityCaptured);
        EventBus.Unsubscribe<PlayerScanReport>(OnPlayerScanReport);
        EventBus.Unsubscribe<PlayerDiagnosticsReport>(OnPlayerDiagnosticsReport);
        EventBus.Unsubscribe<CampDestroyed>(OnCampDestroyed);        
    }
    void OnCampDestroyed(CampDestroyed e)
    {
        if (!phrases)
        {
            Debug.LogWarning("[PlayerMessageComposer] PhraseBank is missing");
            return;
        }

        string tpl = phrases.Pick(phrases.destroyCampPlayer);
        string msg = string.IsNullOrEmpty(tpl) ? $"Лагерь {e.Name} уничтожен."
                                               : tpl.Replace("{camp}", e.Name);

        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, msg));
    }

    void OnCityCaptured(CityCaptured e)
    {
        if (!phrases)
        {
            Debug.LogWarning("[PlayerMessageComposer] PhraseBank is missing");
            return;
        }

        string tpl = phrases.Pick(phrases.captureCityPlayer);
        string msg = string.IsNullOrEmpty(tpl) ? $"Захвачен город {e.Name}."
                                               : tpl.Replace("{city}", e.Name);
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, msg));

        var list = BuildLootList(e.fuel, e.meds, e.ammo);
        string tail = phrases.Pick(phrases.readyToGoPlayer);

        if (!string.IsNullOrEmpty(list))
        {
            string lead = phrases.Pick(phrases.lootLead);
            if (string.IsNullOrEmpty(lead)) lead = "Получено:";
            string lootMsg = $"{lead} {list}. {tail}";
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, lootMsg));
        }
        else if (!string.IsNullOrEmpty(tail))
        {
            EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, tail));
        }
    }

    string BuildLootList(int fuel, int hp, int ammo)
    {
        var sb = new StringBuilder();
        void add(string s) { if (sb.Length > 0) sb.Append(", "); sb.Append(s); }

        if (hp > 0) add($"{hp} {phrases.Pick(phrases.hpNames)}");
        if (ammo > 0) add($"{ammo} {phrases.Pick(phrases.ammoNames)}");
        if (fuel > 0) add($"{fuel} {phrases.Pick(phrases.fuelNames)}");

        return sb.ToString();
    }

    void OnPlayerScanReport(PlayerScanReport r)
    {
        if (!phrases) return;

        string lead = phrases.Pick(phrases.scanLeadPlayer);
        if (string.IsNullOrEmpty(lead)) lead = "Сканирование завершено, результат:";

        string env = r.biome switch
        {
            Biome.Forest => phrases.Pick(phrases.envForest),
            Biome.Swamp => phrases.Pick(phrases.envSwamp),
            Biome.Hills => phrases.Pick(phrases.envHills),
            _ => phrases.Pick(phrases.envPlains)
        };
        if (string.IsNullOrEmpty(env)) env = "окружение не определено";

        string nearLead = phrases.Pick(phrases.nearestLead);
        if (string.IsNullOrEmpty(nearLead)) nearLead = "ближайшие объекты";

        string Dir(int i) => phrases.dir8 != null && phrases.dir8.Length == 8
            ? phrases.dir8[(i % 8 + 8) % 8] : "направление";

        var items = new System.Collections.Generic.List<string>();

        // порядок можно менять; сейчас: дороги → города → противники → лагеря
        foreach (var d in r.roadDirs) items.Add($"{phrases.Pick(phrases.roadWords)} на {Dir(d)}");
        foreach (var d in r.cityDirs) items.Add($"{phrases.Pick(phrases.cityWords)} на {Dir(d)}");
        foreach (var d in r.enemyDirs) items.Add($"{phrases.Pick(phrases.enemyWords)} на {Dir(d)}");
        foreach (var d in r.campDirs) items.Add($"{phrases.Pick(phrases.campWords)} на {Dir(d)}");

        string list = items.Count > 0 ? string.Join(", ", items) : "контактов нет";
        string text = $"{lead} {env}, {nearLead} - {list}";
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, text));
    }

    // Диагностика → одна строка в консоль
    void OnPlayerDiagnosticsReport(PlayerDiagnosticsReport r)
    {
        if (!phrases) return;
        string lead = phrases.Pick(phrases.diagLeadPlayer);
        if (string.IsNullOrEmpty(lead)) lead = "Отчёт о состоянии:";

        string line = r.kind switch
        {
            DiagKind.Health => $"{phrases.Pick(phrases.hpNamesUI)}: {r.hp}/{r.hpMax}",
            DiagKind.Ammo => $"{phrases.Pick(phrases.ammoNamesUI)}: в магазине {r.inMag}/{r.magSize}, запас {r.ammo}/{r.ammoMax}",
            DiagKind.Fuel => $"{phrases.Pick(phrases.fuelNamesUI)}: {r.fuel:0.0}/{r.fuelMax:0}",
            _ => "Неизвестный тип диагностики"
        };

        EventBus.Publish(new ConsoleMessage(ConsoleSender.Robot, $"{lead} {line}"));
    }
}

namespace Events
{
    public enum DiagKind { Health, Ammo, Fuel }

    // Отчёт об осмотре игрока (уже готовые данные для форматирования)
    public readonly struct PlayerScanReport
    {
        public readonly Biome biome;
        public readonly int[] roadDirs;   // 0..7
        public readonly int[] cityDirs;   // 0..7
        public readonly int[] campDirs;   // 0..7
        public readonly int[] enemyDirs;  // 0..7

        public PlayerScanReport(
            Biome biome,
            int[] roadDirs,
            int[] cityDirs,
            int[] campDirs,
            int[] enemyDirs)
        {
            this.biome = biome;
            this.roadDirs = roadDirs ?? System.Array.Empty<int>();
            this.cityDirs = cityDirs ?? System.Array.Empty<int>();
            this.campDirs = campDirs ?? System.Array.Empty<int>();
            this.enemyDirs = enemyDirs ?? System.Array.Empty<int>();
        }
    }

    // Диагностика (единичная метрика)
    public readonly struct PlayerDiagnosticsReport
    {
        public readonly DiagKind kind;
        public readonly int hp, hpMax;
        public readonly int inMag, magSize, ammo, ammoMax;
        public readonly float fuel, fuelMax;

        public PlayerDiagnosticsReport(DiagKind kind,
            int hp, int hpMax,
            int inMag, int magSize, int ammo, int ammoMax,
            float fuel, float fuelMax)
        {
            this.kind = kind;
            this.hp = hp; this.hpMax = hpMax;
            this.inMag = inMag; this.magSize = magSize;
            this.ammo = ammo; this.ammoMax = ammoMax;
            this.fuel = fuel; this.fuelMax = fuelMax;
        }
    }
}
