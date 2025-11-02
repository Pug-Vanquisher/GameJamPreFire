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

    [Header("Осмотр (игрок)")]
    [TextArea] public string[] scanLeadPlayer = { "Сканирование завершено, результат:", "Произвёл осмотр, результат:" };

    // Описание окружения по биому
    [TextArea] public string[] envPlains = { "открытые поля, видимость хорошая" };
    [TextArea] public string[] envForest = { "множество деревьев, видимость ограничена" };
    [TextArea] public string[] envSwamp = { "болотистая местность, возможно затопление" };
    [TextArea] public string[] envHills = { "Холмистая местность, обзор частично закрыт" };

    // Подводка к перечислению ближайших объектов
    [TextArea] public string[] nearestLead = { "ближайшие объекты" };

    // Объектные названия
    public string[] cityWords = { "населённый пункт", "город" };
    public string[] roadWords = { "шоссе", "дорога" };
    public string[] campWords = { "лагерь" };
    public string[] enemyWords = { "колонна противников", "группа противников" };

    // Направления (фиксированный порядок: N, NE, E, SE, S, SW, W, NW)
    public string[] dir8 = { "север", "северо-восток", "восток", "юго-восток", "юг", "юго-запад", "запад", "северо-запад" };

    [Header("Диагностика (игрок)")]
    [TextArea] public string[] diagLeadPlayer = { "Отчёт о состоянии:", "Показания датчиков:" };
    public string[] hpNamesUI = { "Состояние корпуса" };
    public string[] ammoNamesUI = { "Боезапас" };
    public string[] fuelNamesUI = { "Топливо" };

    public string Pick(string[] arr) =>
        (arr == null || arr.Length == 0) ? "" : arr[Random.Range(0, arr.Length)];
}
