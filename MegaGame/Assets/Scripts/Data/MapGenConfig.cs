using UnityEngine;

[CreateAssetMenu(menuName = "World/Map Gen Config")]
public class MapGenConfig : ScriptableObject
{
    [Header("Размеры мира (в юнитах)")]
    public float mapHalfSize = 8000f;

    [Header("Регионы (Вороной)")]
    public int regionSeeds = 28;
    public float regionSeedMinSpacing = 1200f;
    public float regionSafeMargin = 600f; 

    [Header("Биомы (Перлин)")]
    public float biomeFrequency = 0.0006f; 
    public float biomeNoiseEpsilon = 0.03f; 
    public float biomeProbeRadius = 120f;   

    [Header("Размещение узлов")]
    public int citiesCount = 16;
    public int campsCount = 8;
    public float minNodeSpacing = 650f;
    public float capitalSafeRadius = 1400f;

    [Header("База игрока / спавн")]
    public float playerBaseDistanceFromCapital = 1100f;
    public float spawnOffset = 150f;

    [Header("Враги")]
    public int enemySquads = 6;
    public Vector2Int squadStrengthRange = new Vector2Int(3, 7);

    [Header("Дороги")]
    public int roadKNearest = 3;        
    public int extraConnections = 4;    
    public int roadSegments = 24;       
    public float roadCurviness = 0.25f; 
    public float roadFrequency = 1.7f; 

    [Header("Случайность")]
    public int seed = 12345;
    public bool useSystemTimeSeed = true;
}