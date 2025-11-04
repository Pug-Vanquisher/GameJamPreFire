using System.Collections.Generic;
using UnityEngine;
using Events;

public class EnemyMessageComposer : MonoBehaviour
{
    [SerializeField] private PhraseBank phrases;

    [Header("Шансы отправки сообщений")]
    [Range(0, 1)] public float moveIntentChance = 0.10f;
    [Range(0, 1)] public float retreatAnnounceChance = 0.70f;
    [Range(0, 1)] public float helpAnnounceChance = 0.70f;      // запрос помощи
    [Range(0, 1)] public float heardAnnounceChance = 0.50f;      // слышали выстрелы
    [Range(0, 1)] public float helpAckAnnounceChance = 0.70f;    // вас принял, иду на помощь
    [Range(0, 1)] public float resupplyAnnounceChance = 0.35f;   // привезли припасы
    [Range(0, 1)] public float engageAnnounceChance = 1.00f;     // вступаем в бой

    [Header("Кулдауны (сек)")]
    public float minGlobalPerSquadCooldown = 5f;
    public float moveIntentCooldown = 10f;
    public float retreatCooldown = 8f;
    public float helpCooldown = 8f;
    public float heardCooldown = 6f;
    public float helpAckCooldown = 6f;
    public float resupplyCooldown = 12f;
    public float engageCooldown = 2f;

    private readonly Dictionary<string, float> lastGlobalBySquad = new();
    private readonly Dictionary<string, float> lastByKey = new();

    void OnEnable()
    {
        EventBus.Subscribe<EnemyPlannedMove>(OnEnemyPlannedMove);
        EventBus.Subscribe<EnemyRetreatDeclared>(OnEnemyRetreat);
        EventBus.Subscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Subscribe<EnemyHeardShots>(OnHeardShots);
        EventBus.Subscribe<EnemyHelpAccepted>(OnHelpAccepted);
        EventBus.Subscribe<EnemyResupplied>(OnResupplied);
        EventBus.Subscribe<EnemyEngaged>(OnEngaged);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<EnemyPlannedMove>(OnEnemyPlannedMove);
        EventBus.Unsubscribe<EnemyRetreatDeclared>(OnEnemyRetreat);
        EventBus.Unsubscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Unsubscribe<EnemyHeardShots>(OnHeardShots);
        EventBus.Unsubscribe<EnemyHelpAccepted>(OnHelpAccepted);
        EventBus.Unsubscribe<EnemyResupplied>(OnResupplied);
        EventBus.Unsubscribe<EnemyEngaged>(OnEngaged);
    }

    void OnEnemyPlannedMove(EnemyPlannedMove e)
    {
        if (!phrases) return;
        if (!Passes("move", e.SquadId, moveIntentCooldown, moveIntentChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string msg;

        if (e.DestKind == NodeKind.Camp)
        {
            var ws = WorldState.Instance;
            var city = FindNearestCity(ws, e.DestPos);
            if (city != null)
            {
                int dirIdx = Dir8Index(city.Pos, e.DestPos);
                string dir = (phrases.dir8Locative != null && phrases.dir8Locative.Length == 8)
                           ? phrases.dir8Locative[dirIdx] : "севере";
                msg = phrases.Pick(phrases.enemyMoveToCampRelative)
                             .Replace("{prefix}", prefix)
                             .Replace("{call}", e.Callsign)
                             .Replace("{camp}", e.DestName)
                             .Replace("{dir}", dir)
                             .Replace("{city}", city.Name);
            }
            else
            {
                msg = $"{prefix}, это {e.Callsign}. Выдвигаюсь к лагерю {e.DestName}.";
            }
        }
        else // City
        {
            msg = phrases.Pick(phrases.enemyMoveToCity)
                         .Replace("{prefix}", prefix)
                         .Replace("{call}", e.Callsign)
                         .Replace("{city}", e.DestName);
        }

        PostEnemy(msg);
    }

    void OnEnemyRetreat(EnemyRetreatDeclared e)
    {
        if (!phrases) return;
        if (!Passes("retreat", e.SquadId, retreatCooldown, retreatAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string msg = phrases.Pick(phrases.enemyRetreat)
                             .Replace("{prefix}", prefix)
                             .Replace("{call}", e.Callsign);
        PostEnemy(msg);
    }

    void OnReinforcementRequested(ReinforcementRequested e)
    {
        if (!phrases) return;
        if (!Passes("help", e.CallerId, helpCooldown, helpAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string msg = phrases.Pick(phrases.enemyRequestHelp)
                             .Replace("{prefix}", prefix)
                             .Replace("{call}", e.CallerCallsign);
        PostEnemy(msg);
    }

    void OnHeardShots(EnemyHeardShots e)
    {
        if (!phrases) return;
        if (!Passes("heard", e.SquadId, heardCooldown, heardAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string dir = (phrases.dir8 != null && phrases.dir8.Length == 8) ? phrases.dir8[e.DirIndex] : "севере";
        string tpl = phrases.Pick(phrases.enemyHeardShots);
        string msg = tpl.Replace("{prefix}", prefix)
                        .Replace("{call}", e.Callsign)
                        .Replace("{dir}", dir);
        PostEnemy(msg);
    }

    void OnHelpAccepted(EnemyHelpAccepted e)
    {
        if (!phrases) return;
        if (!Passes("helpAck", e.ResponderId, helpAckCooldown, helpAckAnnounceChance)) return;

        string tpl = phrases.Pick(phrases.enemyHelpAck);
        string msg = tpl.Replace("{requester}", e.RequesterCallsign)
                        .Replace("{call}", e.ResponderCallsign);
        PostEnemy(msg);
    }

    void OnResupplied(EnemyResupplied e)
    {
        if (!phrases) return;
        if (!Passes("resupply", e.SquadId, resupplyCooldown, resupplyAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);

        if (e.DestKind == NodeKind.City)
        {
            string what = (e.Kind == SupplyKind.Ammo) ? phrases.ammoNames[0] : phrases.hpNames[0];
            string tpl = phrases.Pick(phrases.enemyResupplyCity);
            PostEnemy(tpl.Replace("{prefix}", prefix)
                         .Replace("{call}", e.Callsign)
                         .Replace("{what}", what)
                         .Replace("{amount}", e.Amount.ToString())
                         .Replace("{city}", e.DestName));
        }
        else
        {
            var ws = WorldState.Instance;
            var city = FindNearestCity(ws, e.DestPos);
            if (city == null) return;

            int dirIdx = Dir8Index(city.Pos, e.DestPos);
            string dir = (phrases.dir8Locative != null && phrases.dir8Locative.Length == 8) ? phrases.dir8Locative[dirIdx] : "севере";
            string what = (e.Kind == SupplyKind.Ammo) ? phrases.ammoNames[0] : phrases.hpNames[0];
            string tpl = phrases.Pick(phrases.enemyResupplyCampRelative);
            PostEnemy(tpl.Replace("{prefix}", prefix)
                         .Replace("{call}", e.Callsign)
                         .Replace("{what}", what)
                         .Replace("{amount}", e.Amount.ToString())
                         .Replace("{camp}", e.DestName)
                         .Replace("{dir}", dir)
                         .Replace("{city}", city.Name));
        }
    }

    void OnEngaged(EnemyEngaged e)
    {
        if (!phrases) return;
        if (!Passes("engage", e.SquadId, engageCooldown, engageAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string tpl = phrases.Pick(phrases.enemyEngage); 
        string msg = tpl.Replace("{prefix}", prefix)
                        .Replace("{call}", e.Callsign);
        PostEnemy(msg);
    }

    bool Passes(string type, string squadId, float perTypeCooldown, float chance)
    {
        float now = Time.time;

        if (lastGlobalBySquad.TryGetValue(squadId, out var gLast))
            if (now - gLast < minGlobalPerSquadCooldown) return false;

        string key = $"{type}:{squadId}";
        if (lastByKey.TryGetValue(key, out var tLast))
            if (now - tLast < perTypeCooldown) return false;

        if (Random.value > Mathf.Clamp01(chance)) return false;

        lastGlobalBySquad[squadId] = now;
        lastByKey[key] = now;
        return true;
    }

    void PostEnemy(string msg)
    {
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Enemy, msg));
    }

    NodeData FindNearestCity(WorldState ws, Vector2 pos)
    {
        if (ws == null) return null;

        NodeData best = ws.Capital;
        float bestD = (best != null) ? Vector2.Distance(pos, best.Pos) : float.PositiveInfinity;

        foreach (var c in ws.Cities)
        {
            float d = Vector2.Distance(pos, c.Pos);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best; 
    }

    // 0..7: N,NE,E,SE,S,SW,W,NW
    int Dir8Index(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        if (d.sqrMagnitude < 1e-6f) return 0;
        float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; 
        if (ang < 0f) ang += 360f;
        return Mathf.RoundToInt(ang / 45f) % 8;
    }
}
