using System;
using System.Collections;
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

    private Tween twFade, twPulse;
    private Scene mainScene;

    void Start()
    {
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
        // 1) Грузим основную сцену ADDITIVE (чтобы объекты уже были и мы могли вызвать их async-процедуры)
        var op = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        mainScene = SceneManager.GetSceneByName(mainSceneName);
        if (!mainScene.IsValid())
        {
            Debug.LogError("[Boot] Main scene not valid");
            yield break;
        }

        // 2) Находим генератор и рендерер карты
        MapGenerator gen = null;
        WorldMapRenderer wm = null;

        foreach (var root in mainScene.GetRootGameObjects())
        {
            gen ??= root.GetComponentInChildren<MapGenerator>(true);
            wm ??= root.GetComponentInChildren<WorldMapRenderer>(true);
            if (gen && wm) break;
        }

        if (!gen) Debug.LogError("[Boot] MapGenerator not found in main scene");
        if (!wm) Debug.LogError("[Boot] WorldMapRenderer not found in main scene");

        float stageWeightGen = 0.65f;  // сколько «веса» отдаём генерации мира
        float stageWeightBake = 0.35f; // и запеканию текстуры

        Action<float> setGenProg = t => UpdateProgress(stageWeightGen * Mathf.Clamp01(t));
        Action<float> setBakeProg = t => UpdateProgress(stageWeightGen + stageWeightBake * Mathf.Clamp01(t));

        // 3) Запускаем асинхронную генерацию мира (без автозапуска на Start)
        if (gen) yield return StartCoroutine(gen.GenerateAsync(setGenProg));

        // 4) Печём карту асинхронно
        if (wm) yield return StartCoroutine(wm.BakeAsync(setBakeProg));

        // 5) Готово — показываем «нажмите любую кнопку»
        if (progressGroup) progressGroup.SetActive(false);
        if (pressAnyKeyGroup) pressAnyKeyGroup.SetActive(true);

        if (pressAnyKeyText)
        {
            var tr = pressAnyKeyText.transform as RectTransform;
            tr.localScale = Vector3.one;
            twPulse = tr.DOScale(pressPulseScale, pressPulseTime)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        // Ждём нажатия
        yield return new WaitUntil(() => Input.anyKeyDown);

        // Делаем основную сцену активной и выгружаем загрузочную
        SceneManager.SetActiveScene(mainScene);
        var boot = SceneManager.GetActiveScene(); // это загрузочная
        SceneManager.UnloadSceneAsync(boot);
    }

    void UpdateProgress(float t01)
    {
        if (progressFill) progressFill.fillAmount = t01;
        if (progressLabel) progressLabel.text = $"{Mathf.RoundToInt(t01 * 100f)}%";
    }
}
