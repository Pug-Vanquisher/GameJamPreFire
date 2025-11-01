using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Events; // EventBus

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }

    public static string CurrentCode { get; private set; } = "ru";

    public static Locale CurrentLocale { get; private set; }

    private const string LanguageKey = "SelectedLanguage";
    private Coroutine setRoutine;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        // Ждём инициализации Unity Localization
        yield return LocalizationSettings.InitializationOperation;

        // Загружаем сохранённое или берём дефолт
        string saved = PlayerPrefs.GetString(LanguageKey, GuessDefaultCode());
        yield return SetLanguageInternal(saved);
    }

    /// Вызывай из UI: кнопки/выпадашки языков передают "en", "ru", ...
    public void ChangeLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return;

        if (setRoutine != null) StopCoroutine(setRoutine);
        setRoutine = StartCoroutine(SetLanguageInternal(languageCode));
    }

    // --- Внутреннее применение языка + оповещение через EventBus ---
    private IEnumerator SetLanguageInternal(string languageCode)
    {
        yield return LocalizationSettings.InitializationOperation;

        Locale target = FindLocale(languageCode)
                        ?? FindLocale("en")
                        ?? LocalizationSettings.ProjectLocale;

        LocalizationSettings.SelectedLocale = target;
        CurrentLocale = target;
        CurrentCode = target.Identifier.Code;

        PlayerPrefs.SetString(LanguageKey, CurrentCode);
        // PlayerPrefs.Save(); // по желанию

        EventBus.Publish(new LanguageChanged(CurrentCode, CurrentLocale));
        setRoutine = null;
    }

    private static Locale FindLocale(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        foreach (var loc in LocalizationSettings.AvailableLocales.Locales)
        {
            if (string.Equals(loc.Identifier.Code, code, StringComparison.OrdinalIgnoreCase))
                return loc;
        }
        return null;
    }

    private static string GuessDefaultCode()
    {
        // Простой маппинг системного языка в код
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.English: return "en";
            default: return "en";
        }
    }
}