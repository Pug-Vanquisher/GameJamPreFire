using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "World/Console Phrase Bank")]
public class PhraseBank : ScriptableObject
{
    [FormerlySerializedAs("captureCity")]
    [Header("Захват города (игрок) — подставляется {city}")]
    [TextArea] public string[] captureCityPlayer;

    [Header("Подводка к луту")]
    [TextArea] public string[] lootLead;

    public string[] hpNames = { "комплекты для лечения", "медикаменты", "ремкомплекты" };
    public string[] ammoNames = { "боеприпасы", "патроны", "боезапас" };
    public string[] fuelNames = { "топливо", "горючее" };

    [FormerlySerializedAs("readyToGo")]
    [Header("Фраза-окончание (игрок)")]
    [TextArea] public string[] readyToGoPlayer = { "Готов продолжать движение.", "Маршрут открыт.", "Можно двигаться дальше." };

    public string Pick(string[] arr) =>
        (arr == null || arr.Length == 0) ? "" : arr[Random.Range(0, arr.Length)];
}
