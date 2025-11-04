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

    [Header("Уничтожение лагеря (игрок) — подставляется {camp}")]
    [TextArea] public string[] destroyCampPlayer = { "Лагерь {camp} уничтожен." };

    [Header("Направления (предложный падеж) по компасу N,NE,E,SE,S,SW,W,NW")]
    public string[] dir8Locative = new string[]
{
    "севере","северо-востоке","востоке","юго-востоке",
    "юге","юго-западе","западе","северо-западе"
};

    [Header("Вражеская связь: префиксы обращения")]
    [TextArea] public string[] enemyPrefixes = { "КП", "Всем постам", "Внимание" };

    [Header("Враг: движение к городу — {prefix}, это {call}. Выдвигаюсь к {city}.")]
    [TextArea] public string[] enemyMoveToCity = { "{prefix}, это {call}. Выдвигаюсь к {city}." };

    [Header("Враг: движение к лагерю с привязкой к городу — {prefix}, это {call}. Выдвигаюсь к лагерю {camp}, на {dir} от города {city}.")]
    [TextArea]
    public string[] enemyMoveToCampRelative =
    {
    "{prefix}, это {call}. Выдвигаюсь к лагерю {camp}, на {dir} от города {city}."
};

    [Header("Враг: отступление — {prefix}, это {call}. Нас прижали, отступаю.")]
    [TextArea]
    public string[] enemyRetreat =
    {
    "{prefix}, это {call}. Нас прижали, отступаю."
};

    [Header("Враг: запрос подкрепления — {prefix}, это {call}. Требуется подкрепление!")]
    [TextArea]
    public string[] enemyRequestHelp =
    {
    "{prefix}, это {call}. Требуется подкрепление!"
};

    [Header("Враг: вступление в бой — {prefix}, это {call}. Вступаем в бой с противником.")]
    [TextArea]
    public string[] enemyEngage =
    {
    "{prefix}, это {call}. Вступаем в бой с противником."
};
    [Header("Враг: слышали выстрелы — {prefix}, это {call}. Слышал(и) выстрелы на {dir} от нас, выдвигаемся на проверку.")]
    [TextArea]
    public string[] enemyHeardShots =
{
    "{prefix}, это {call}. Слышали выстрелы на {dir} от нас, выдвигаемся на проверку.",
    "{prefix}, это {call}. Отмечаем выстрелы на {dir}, идём проверить."
};

    [Header("Враг: принял запрос о помощи — Вас принял {requester}, это {call}, направляемся на помощь.")]
    [TextArea]
    public string[] enemyHelpAck =
    {
    "Вас принял {requester}, это {call}, направляемся на помощь.",
    "Принял {requester}. {call}, выдвигаемся на поддержку."
};

    [Header("Враг: пополнение в городе — {prefix}, это {call}. Привезли {what} {amount} в город {city}.")]
    [TextArea]
    public string[] enemyResupplyCity =
    {
    "{prefix}, это {call}. Привезли {what} {amount} в город {city}."
};

    [Header("Враг: пополнение в лагере — {prefix}, это {call}. Привезли {what} {amount} в лагерь {camp}, на {dir} от города {city}.")]
    [TextArea]
    public string[] enemyResupplyCampRelative =
    {
    "{prefix}, это {call}. Привезли {what} {amount} в лагерь {camp}, на {dir} от города {city}."
};


    public string Pick(string[] arr) =>
        (arr == null || arr.Length == 0) ? "" : arr[Random.Range(0, arr.Length)];
}
