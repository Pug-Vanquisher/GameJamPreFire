using UnityEngine;

[CreateAssetMenu(menuName = "World/Name Bank")]
public class NameBank : ScriptableObject
{
    [TextArea] public string comment;

    [Header("Города")]
    public string[] cityNames;        

    [Header("Лагеря (префиксы/суффиксы)")]
    public string[] campPrefixes;     
    public string[] campRoots;        
    public string[] campSuffixes;    

    public string PickCityName(System.Random rng)
    {
        if (cityNames == null || cityNames.Length == 0) return "Город";
        return cityNames[rng.Next(cityNames.Length)];
    }

    public string PickCampName(System.Random rng)
    {
        string p = Pick(campPrefixes, "Лагерь", rng);
        string r = Pick(campRoots, "Сектора", rng);
        string s = Pick(campSuffixes, "", rng);
        return $"{p} {r}{s}";
    }

    private static string Pick(string[] arr, string def, System.Random rng)
        => (arr == null || arr.Length == 0) ? def : arr[rng.Next(arr.Length)];
}
