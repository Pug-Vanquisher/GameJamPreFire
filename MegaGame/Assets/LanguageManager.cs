using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections;

public class LanguageManager : MonoBehaviour
{
    private const string LanguageKey = "SelectedLanguage";

    private void Start()
    {
        string savedLanguage = PlayerPrefs.GetString(LanguageKey, "ru");
        StartCoroutine(SetLanguage(savedLanguage));
    }

    public void ChangeLanguage(string languageCode)
    {
        PlayerPrefs.SetString(LanguageKey, languageCode);
        StartCoroutine(SetLanguage(languageCode));
    }

    private IEnumerator SetLanguage(string languageCode)
    {
        yield return LocalizationSettings.InitializationOperation;

        foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
        {
            if (locale.Identifier.Code == languageCode)
            {
                LocalizationSettings.SelectedLocale = locale;
                break;
            }
        }

        EventManager.Instance.TriggerEvent("LanguageChanged");
    }
}
