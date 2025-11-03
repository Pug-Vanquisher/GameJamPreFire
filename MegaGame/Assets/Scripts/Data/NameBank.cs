using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "World/Name Bank")]
public class NameBank : ScriptableObject
{
    [TextArea] public string comment;

    [Header("Города (уникальные)")]
    public string[] cityNames;

    [Header("Лагеря (две части)")]
    [FormerlySerializedAs("campPrefixes")] public string[] campFirst;   // было: префиксы
    [FormerlySerializedAs("campRoots")] public string[] campSecond;  // было: корни
    // campSuffixes больше не используем

    public string PickCityNameUnique(System.Random rng, HashSet<string> used)
    {
        if (cityNames != null && cityNames.Length > 0)
        {
            // пробуем до 200 раз найти уникальное
            for (int t = 0; t < 200; t++)
            {
                string s = cityNames[rng.Next(cityNames.Length)].Trim();
                if (s.Length == 0) continue;
                if (used.Add(s)) return s;
            }
        }
        // fallback (дебаг)
        string rnd = $"Город-{rng.Next(1000, 9999)}";
        used.Add(rnd);
        return rnd;
    }

    public string PickCampName(System.Random rng)
    {
        string a = Pick(campFirst, "Застава", rng);
        string b = Pick(campSecond, "Северная", rng);
        return $"{a} {b}";
    }

    private static string Pick(string[] arr, string def, System.Random rng)
        => (arr == null || arr.Length == 0) ? def : arr[rng.Next(arr.Length)];
}
