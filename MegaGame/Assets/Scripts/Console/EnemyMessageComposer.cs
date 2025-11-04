using System.Collections.Generic;
using UnityEngine;
using Events;

public class EnemyMessageComposer : MonoBehaviour
{
    [SerializeField] private PhraseBank phrases;

    [Header("Шансы отправки сообщений")]
    [Range(0, 1)] public float moveIntentChance = 0.10f;
    [Range(0, 1)] public float retreatAnnounceChance = 0.70f; 
    [Range(0, 1)] public float helpAnnounceChance = 0.70f; 
    [Range(0, 1)] public float engageAnnounceChance = 1.00f;

    [Header("Кулдауны (сек)")]
    public float minGlobalPerSquadCooldown = 5f;
    public float moveIntentCooldown = 10f;
    public float retreatCooldown = 8f;
    public float helpCooldown = 8f;
    public float engageCooldown = 2f;

    private readonly Dictionary<string, float> lastGlobalBySquad = new();
    private readonly Dictionary<string, float> lastByKey = new();

    void OnEnable()
    {
        EventBus.Subscribe<EnemyPlannedMove>(OnEnemyPlannedMove);
        EventBus.Subscribe<EnemyRetreatDeclared>(OnEnemyRetreat);
        EventBus.Subscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Subscribe<EnemyEngaged>(OnEnemyEngaged);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<EnemyPlannedMove>(OnEnemyPlannedMove);
        EventBus.Unsubscribe<EnemyRetreatDeclared>(OnEnemyRetreat);
        EventBus.Unsubscribe<ReinforcementRequested>(OnReinforcementRequested);
        EventBus.Unsubscribe<EnemyEngaged>(OnEnemyEngaged);
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
                string dir = phrases.dir8Locative != null && phrases.dir8Locative.Length == 8
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
                // fallback без привязки к городу
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

        EventBus.Publish(new ConsoleMessage(ConsoleSender.Enemy, msg));
    }

    void OnEnemyRetreat(EnemyRetreatDeclared e)
    {
        if (!phrases) return;
        if (!Passes("retreat", e.SquadId, retreatCooldown, retreatAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string msg = phrases.Pick(phrases.enemyRetreat)
                             .Replace("{prefix}", prefix)
                             .Replace("{call}", e.Callsign);
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Enemy, msg));
    }

    void OnReinforcementRequested(ReinforcementRequested e)
    {
        if (!phrases) return;
        if (!Passes("help", e.SquadId, helpCooldown, helpAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string msg = phrases.Pick(phrases.enemyRequestHelp)
                             .Replace("{prefix}", prefix)
                             .Replace("{call}", e.Callsign);
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Enemy, msg));
    }

    void OnEnemyEngaged(EnemyEngaged e)
    {
        if (!phrases) return;
        if (!Passes("engage", e.SquadId, engageCooldown, engageAnnounceChance)) return;

        string prefix = phrases.Pick(phrases.enemyPrefixes);
        string msg = phrases.Pick(phrases.enemyEngage)
                             .Replace("{prefix}", prefix)
                             .Replace("{call}", e.Callsign);
        EventBus.Publish(new ConsoleMessage(ConsoleSender.Enemy, msg));
    }

    // ---------- cooldown utils ----------

    bool Passes(string type, string squadId, float perTypeCooldown, float chance)
    {
        float now = Time.time;

        if (lastGlobalBySquad.TryGetValue(squadId, out var gLast))
            if (now - gLast < minGlobalPerSquadCooldown) return false;

        string key = $"{type}:{squadId}";
        if (lastByKey.TryGetValue(key, out var tLast))
            if (now - tLast < perTypeCooldown) return false;

        if (UnityEngine.Random.value > Mathf.Clamp01(chance)) return false;

        lastGlobalBySquad[squadId] = now;
        lastByKey[key] = now;
        return true;
    }

    // ---------- helpers ----------

    NodeData FindNearestCity(WorldState ws, Vector2 pos)
    {
        if (ws == null) return null;

        NodeData best = ws.Capital;
        float bestD = (best != null) ? Vector2.Distance(pos, best.Pos) : float.PositiveInfinity;

        foreach (var c in ws.Cities)
        {
            float d = Vector2.Distance(pos, c.Pos);
            if (d < bestD)
            {
                bestD = d;
                best = c;
            }
        }

        return best; // может вернуть null, если нет столицы и список городов пуст
    }

    // 0..7: N,NE,E,SE,S,SW,W,NW  (N = вверх, т.е. +Y)
    int Dir8Index(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        if (d.sqrMagnitude < 1e-6f) return 0;
        float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; // угол от "север" (+Y)
        if (ang < 0f) ang += 360f;
        return Mathf.RoundToInt(ang / 45f) % 8;
    }
}
