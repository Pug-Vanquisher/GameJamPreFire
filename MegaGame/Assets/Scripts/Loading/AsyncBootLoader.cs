using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class AsyncBootLoader : MonoBehaviour
{
    [Header("Main scene")]
    [SerializeField] private string mainSceneName = "Main";

    [Header("UI")]
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private float fadeMin = 0.65f, fadeMax = 1f, fadeSpeed = 1.6f;
    [SerializeField] private GameObject progressGroup;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private GameObject pressAnyKeyGroup;
    [SerializeField] private TMP_Text pressAnyKeyText;
    [SerializeField] private float pressPulseScale = 1.06f, pressPulseTime = 0.7f;

    [Header("Prep options")]
    [Tooltip("Пока готовим мир — скрывать все корневые объекты основной сцены, кроме нужных для подготовки.")]
    [SerializeField] private bool hideMainSceneDuringPrep = true;

    private Tween twFade, twPulse;

    // сцены
    private Scene bootScene;
    private Scene mainScene;

    // кого временно отключили
    private readonly List<GameObject> deactivatedRoots = new();

    void Start()
    {
        bootScene = gameObject.scene; // запомним свою сцену

        if (progressGroup) progressGroup.SetActive(true);
        if (pressAnyKeyGroup) pressAnyKeyGroup.SetActive(false);

        if (backdrop)
            twFade = backdrop.DOFade(fadeMin, fadeSpeed)
                             .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                             .SetUpdate(true);

        StartCoroutine(BootFlow());
    }

    void OnDestroy()
    {
        twFade?.Kill();
        twPulse?.Kill();
    }

    IEnumerator BootFlow()
    {
        // 1) Грузим основную сцену ADDITIVE
        var op = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        mainScene = SceneManager.GetSceneByName(mainSceneName);
        if (!mainScene.IsValid())
        {
            Debug.LogError("[Boot] Main scene not valid");
            yield break;
        }

        // 2) Найдём нужные объекты в основной сцене
        MapGenerator gen = null;
        WorldMapRenderer wm = null;
        GameRunManager grm = null;
        WorldState ws = WorldState.Instance; // он DontDestroyOnLoad — может уже существовать

        foreach (var root in mainScene.GetRootGameObjects())
        {
            gen ??= root.GetComponentInChildren<MapGenerator>(true);
            wm ??= root.GetComponentInChildren<WorldMapRenderer>(true);
            grm ??= root.GetComponentInChildren<GameRunManager>(true);
        }

        if (!gen) Debug.LogError("[Boot] MapGenerator not found in main scene");
        if (!wm) Debug.LogError("[Boot] WorldMapRenderer not found in main scene");

        // 3) При необходимости — скрыть всю основную сцену, оставить только нужных
        GameObject genRoot = gen ? gen.transform.root.gameObject : null;
        GameObject wmRoot = wm ? wm.transform.root.gameObject : null;
        GameObject grmRoot = grm ? grm.transform.root.gameObject : null;

        if (hideMainSceneDuringPrep)
        {
            var keep = new HashSet<GameObject>();
            if (genRoot) keep.Add(genRoot);
            if (wmRoot) keep.Add(wmRoot);
            if (grmRoot) keep.Add(grmRoot);
            // WorldState висит в DontDestroyOnLoad — на корни основной сцены не влияет

            foreach (var root in mainScene.GetRootGameObjects())
            {
                if (keep.Contains(root)) { root.SetActive(true); continue; }
                if (root.activeSelf)
                {
                    root.SetActive(false);
                    deactivatedRoots.Add(root);
                }
            }
        }

        // На всякий случай остановим автокорутины, если кто-то успел стартануть сам
        if (gen) gen.StopAllCoroutines();
        if (wm) wm.StopAllCoroutines();

        // 4) Асинхронно готовим мир + выпекаем карту
        float stageWeightGen = 0.65f;
        float stageWeightBake = 0.35f;
        Action<float> setGenProg = t => UpdateProgress(stageWeightGen * Mathf.Clamp01(t));
        Action<float> setBakeProg = t => UpdateProgress(stageWeightGen + stageWeightBake * Mathf.Clamp01(t));

        if (gen) yield return StartCoroutine(gen.GenerateAsync(setGenProg));
        if (wm) yield return StartCoroutine(wm.BakeAsync(setBakeProg));

        // 5) Готово — «нажмите любую кнопку»
        if (progressGroup) progressGroup.SetActive(false);
        if (pressAnyKeyGroup) pressAnyKeyGroup.SetActive(true);

        if (pressAnyKeyText)
        {
            var tr = pressAnyKeyText.transform as RectTransform;
            tr.localScale = Vector3.one;
            twPulse = tr.DOScale(pressPulseScale, pressPulseTime)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        yield return new WaitUntil(() => Input.anyKeyDown);

        // 6) Реактивируем корни основной сцены
        foreach (var go in deactivatedRoots) if (go) go.SetActive(true);
        deactivatedRoots.Clear();

        // 7) Делаем основную сцену активной
        SceneManager.SetActiveScene(mainScene);

        // 8) Ставим игру на паузу на старте (меню открыто)
        var menu = FindObjectOfType<MenuScript>(true);
        if (menu) menu.Pause(true);

        // 9) Выгружаем загрузочную сцену (ИМЕННО bootScene!)
        SceneManager.UnloadSceneAsync(bootScene);
    }

    void UpdateProgress(float t01)
    {
        if (progressFill) progressFill.fillAmount = t01;
        if (progressLabel) progressLabel.text = $"{Mathf.RoundToInt(t01 * 100f)}%";
    }
}
