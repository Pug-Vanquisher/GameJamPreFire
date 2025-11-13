using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Events;
using DG.Tweening;
using TMPro;
using System.Collections;

public class InterfaceController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CommandConsoleUI console;

    [SerializeField] private RawImage mapImage;
    [SerializeField] private MeshRenderer mapMesh;
    [SerializeField] private MeshRenderer logMesh;
    [SerializeField] private MeshRenderer actionMesh;
    [SerializeField] private MeshRenderer radarMesh;

    [SerializeField] private GameObject lever;
    [SerializeField] private GameObject[] buttons;
    [SerializeField] private Camera targetCamera;

    private Vector3 nextLeverPosition = Vector3.zero;
    private Vector3 currentLeverPosition = Vector3.zero;
    private bool is_leverMoving = false;

    [Header("Buttons FX")]
    [SerializeField] private float buttonStroke = 0.01f;
    [SerializeField] private float buttonSpeed = 12f;

    [Header("Head/cockpit look")]
    [SerializeField] private Vector3 minCameraRotation = new Vector3(-3, -4, 0);
    [SerializeField] private Vector3 maxCameraRotation = new Vector3(3, 4, 0);
    [SerializeField] private float lookLerp = 5f;

    [Header("Shake")]
    [SerializeField] private string multProp = "_mult";
    [SerializeField] private float baseLogMult = 0.001f;
    [SerializeField] private float baseRadarMult = 0.028f;
    [SerializeField] private float baseActionMult = 0.001f;

    [System.Serializable]
    public struct ShakeProfile
    {
        public float duration;              
        public Vector3 strength;            
        public int vibrato;                 
        [Range(0f, 180f)] public float randomness; // разброс
        public bool fadeOut;                
        [Header("шум")]
        public float panelsPulse;          
        public Ease panelsEase;             
    }

    [Header("Shake Profiles")]
    [SerializeField]
    private ShakeProfile onHit = new ShakeProfile
    {
        duration = 0.25f,
        strength = new Vector3(0.05f, 0.02f, 0),
        vibrato = 25,
        randomness = 90,
        fadeOut = true,
        panelsPulse = 0.040f,
        panelsEase = Ease.OutQuad
    };
    [SerializeField]
    private ShakeProfile onShot = new ShakeProfile
    {
        duration = 0.12f,
        strength = new Vector3(0.02f, 0.01f, 0),
        vibrato = 20,
        randomness = 120,
        fadeOut = true,
        panelsPulse = 0.020f,
        panelsEase = Ease.OutQuad
    };

    [Header("Shake Control")]
    [SerializeField] private float minShakeInterval = 0.05f; // антиспам
    [SerializeField] private float globalShakeScale = 1f;   

    private int mouseInterracted = -1;

    private List<Vector3> initPoses;
    private Tweener camShakeTween;
    private Tween logMultTween, radarMultTween, actionMultTween;
    private float lastShakeTime;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PlayerDamaged>(OnPlayerDamagedShake);
        // EventBus.Subscribe<PlayerFired>(OnPlayerFiredShake);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDamaged>(OnPlayerDamagedShake);
        // EventBus.Unsubscribe<PlayerFired>(OnPlayerFiredShake);

        KillAllTweens();
    }

    private void Start()
    {
        if (buttons == null) buttons = new GameObject[0];
        initPoses = new List<Vector3>(buttons.Length);
        for (int i = 0; i < buttons.Length; i++)
            initPoses.Add(buttons[i] ? buttons[i].transform.position : Vector3.zero);

        SetPanelMults(baseLogMult, baseRadarMult, baseActionMult);
    }

    void Update()
    {

        CameraTrackMouse();
        HandleMouse();
        HandleLever();

        for (int i = 0; i < buttons.Length; i++)
            HandleButtonClick(i);
    }

    private void HandleButtonClick(int number)
    {
        if (number < 0 || number >= buttons.Length || buttons[number] == null) return;

        bool pressedDigit =
            Input.GetKey((KeyCode)(number + 256)) ||  
            Input.GetKey((KeyCode)(number + 48));     

        if (pressedDigit || mouseInterracted == number)
        {
            buttons[number].transform.position = initPoses[number] + Vector3.down * buttonStroke;
        }
        else
        {
            buttons[number].transform.position =
                Vector3.Lerp(buttons[number].transform.position, initPoses[number], Time.deltaTime * buttonSpeed);
        }
    }

    private void CameraTrackMouse()
    {
        if (!targetCamera) return;
        Vector3 mp = new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0f);
        var target = Quaternion.Euler(
            Mathf.Lerp(maxCameraRotation.x, minCameraRotation.x, mp.y),
            Mathf.Lerp(maxCameraRotation.y, minCameraRotation.y, mp.x),
            0f);
        targetCamera.transform.rotation =
            Quaternion.Lerp(targetCamera.transform.rotation, target, Time.deltaTime * Mathf.Max(0.01f, lookLerp));
    }

    private void HandleMouse()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            Ray ray = (targetCamera ? targetCamera : Camera.main).ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow, 0.1f);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (int.TryParse(hit.collider.name, out int number))
                {
                    mouseInterracted = number;
                    console?.PressDigit(number);
                }
                else mouseInterracted = -1;
            }
            else mouseInterracted = -1;
        }
        else
        {
            mouseInterracted = -1;
        }

        if (Input.GetKey(KeyCode.Mouse0))
        {
            Ray ray = (targetCamera ? targetCamera : Camera.main).ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow, 0.1f);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (int.TryParse(hit.collider.name, out int number))
                {
                    mouseInterracted = number;
                    Vector3 dir = Vector3.zero;
                    if (number == 2) dir = Vector2.down;
                    if (number == 8) dir = Vector2.up;
                    if (number == 4) dir = Vector2.left;
                    if (number == 6) dir = Vector2.right; 
                    EventBus.Publish(new ConsoleMoveInput(dir));
                }
            }
            else
            {
                mouseInterracted = -1;
            }
        }
        else
        {
            mouseInterracted = -1;
        }
    }

    private void SetMapTexture(MeshRenderer mesh, RawImage image)
    {
        if (!mesh || image == null) return;
        var mats = mesh.materials;
        if (mats.Length > 1 && image.texture)
        {
            mats[1].mainTexture = image.texture;
            mesh.materials = mats;
        }
    }

    private void OnPlayerDamagedShake(PlayerDamaged e)
    {

        float scale = 1f;
        TriggerShake(onHit, scale);
    }
    private void HandleLever()
    {
        Vector3 vector = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) vector.x = 1;
        if (Input.GetKey(KeyCode.A)) vector.z = 1;
        if (Input.GetKey(KeyCode.S)) vector.x = -1;
        if (Input.GetKey(KeyCode.D)) vector.z = -1;

        vector *= 5;

        lever.transform.rotation = Quaternion.Lerp(lever.transform.rotation, Quaternion.Euler(vector.x - 25, 85, vector.z), Time.deltaTime*10f);
    }

    // private void OnPlayerFiredShake(PlayerFired _)
    // {
    //     TriggerShake(onShot, 1f);
    // }


    public void TriggerShake(ShakeProfile profile, float scale = 1f)
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        if (Time.unscaledTime - lastShakeTime < minShakeInterval) return;
        lastShakeTime = Time.unscaledTime;

        float dur = Mathf.Max(0.01f, profile.duration);
        Vector3 str = profile.strength * (globalShakeScale * Mathf.Max(0.001f, scale));

        camShakeTween?.Kill(true);
        camShakeTween = targetCamera.transform.DOShakePosition(
            dur, str, profile.vibrato, profile.randomness, false, profile.fadeOut
        );

        float logTarget = baseLogMult + profile.panelsPulse * scale;
        float radarTarget = baseRadarMult + profile.panelsPulse * scale;
        float actionTarget = baseActionMult + profile.panelsPulse * scale;

        logMultTween?.Kill();
        radarMultTween?.Kill();
        actionMultTween?.Kill();

        float up = Mathf.Min(0.25f * dur, 0.1f);
        float down = Mathf.Max(dur - up, 0.05f);

        logMultTween = DOTween.Sequence()
            .Append(DOTween.To(() => GetMult(logMesh, baseLogMult), v => SetMult(logMesh, v), logTarget, up))
            .Append(DOTween.To(() => GetMult(logMesh, logTarget), v => SetMult(logMesh, v), baseLogMult, down).SetEase(profile.panelsEase));

        radarMultTween = DOTween.Sequence()
            .Append(DOTween.To(() => GetMult(radarMesh, baseRadarMult), v => SetMult(radarMesh, v), radarTarget, up))
            .Append(DOTween.To(() => GetMult(radarMesh, radarTarget), v => SetMult(radarMesh, v), baseRadarMult, down).SetEase(profile.panelsEase));

        actionMultTween = DOTween.Sequence()
            .Append(DOTween.To(() => GetMult(actionMesh, baseActionMult), v => SetMult(actionMesh, v), actionTarget, up))
            .Append(DOTween.To(() => GetMult(actionMesh, actionTarget), v => SetMult(actionMesh, v), baseActionMult, down).SetEase(profile.panelsEase));
    }

    private void KillAllTweens()
    {
        camShakeTween?.Kill();
        logMultTween?.Kill();
        radarMultTween?.Kill();
        actionMultTween?.Kill();
    }


    private float GetMult(MeshRenderer mr, float fallback)
    {
        if (!mr) return fallback;
        var mats = mr.materials;
        if (mats.Length <= 1 || mats[1] == null) return fallback;
        if (!mats[1].HasProperty(multProp)) return fallback;
        return mats[1].GetFloat(multProp);
    }

    private void SetMult(MeshRenderer mr, float v)
    {
        if (!mr) return;
        var mats = mr.materials;
        if (mats.Length <= 1 || mats[1] == null) return;
        if (!mats[1].HasProperty(multProp)) return;
        mats[1].SetFloat(multProp, v);
        mr.materials = mats;
    }

    private void SetPanelMults(float log, float radar, float action)
    {
        SetMult(logMesh, log);
        SetMult(radarMesh, radar);
        SetMult(actionMesh, action);
    }

    public void ShakeOnHit(float scale = 1f) => TriggerShake(onHit, Mathf.Max(0.1f, scale));
    public void ShakeOnShot(float scale = 1f) => TriggerShake(onShot, Mathf.Max(0.1f, scale));
}
