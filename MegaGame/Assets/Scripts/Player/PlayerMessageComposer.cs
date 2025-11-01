using System.Text;
using UnityEngine;
using Events;

public class PlayerMessageComposer : MonoBehaviour
{
    [SerializeField] private PhraseBank phrases;

    void OnEnable() => EventBus.Subscribe<CityCaptured>(OnCityCaptured);
    void OnDisable() => EventBus.Unsubscribe<CityCaptured>(OnCityCaptured);

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
}
