using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Events;
public class InterfaceController : MonoBehaviour
{

    [SerializeField] private CommandConsoleUI console;

    [SerializeField] private RawImage mapImage;

    [SerializeField] private MeshRenderer mapMesh;
    [SerializeField] private MeshRenderer logMesh;
    [SerializeField] private MeshRenderer actionMesh;
    [SerializeField] private MeshRenderer radarMesh;

    [SerializeField] private GameObject[] buttons;

    [SerializeField] private float buttonStroke;
    [SerializeField] private float buttonSpeed;

    [SerializeField] private Vector3 minCameraRotation;
    [SerializeField] private Vector3 maxCameraRotation;


    private int mouseInterracted;

    [SerializeField] private float magnitudeMult;
    [SerializeField] private float shakeCooldown;
    private float _shakeCooldown;
    private float magnitude;

    private int activeLeg;

    private List<Vector3> initPoses;

    private void Start()
    {
        EventBus.Subscribe<PlayerDamaged>(StartShake);

        for (int i = 0; i < buttons.Length; i++)
        {
            initPoses.Add(buttons[i].transform.position);
        }
    }

    // Update is called once per frame
    void Update()
    {
        CameraTrackMouse();
        HandleMouse();
        HandleShake();

        for (int i = 0; i < buttons.Length; i++)
        {
            HandleButtonClick(i);
        }

    }

    private void HandleButtonClick(int number)
    {
        if (Input.GetKey((KeyCode)(number + 256)) || Input.GetKey((KeyCode)(number + 48)) || mouseInterracted == number)
        {
            buttons[number].transform.position = initPoses[number] + Vector3.down * buttonStroke;
        }
        else
        {
            buttons[number].transform.position = Vector3.Lerp(buttons[number].transform.position, initPoses[number], Time.deltaTime * buttonSpeed);
        }
    }
    private void CameraTrackMouse() 
    {
        Vector3 mousePos = new Vector3(Input.mousePosition.x/(Screen.width), Input.mousePosition.y/(Screen.height), 0);

        Camera.main.transform.rotation = Quaternion.Lerp(Camera.main.transform.rotation, Quaternion.Euler(Mathf.Lerp(maxCameraRotation.x, minCameraRotation.x, mousePos.y),
                                                    Mathf.Lerp(maxCameraRotation.y, minCameraRotation.y, mousePos.x),
                                                    0), Time.deltaTime * 5);
    }
    private void HandleMouse()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                int number;
                if (int.TryParse(hit.collider.name, out number))
                {
                    mouseInterracted = number;
                    console.PressDigit(number);
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

        if (Input.GetKey(KeyCode.Mouse0))
        {
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                int number;
                if (int.TryParse(hit.collider.name, out number))
                {
                    mouseInterracted = number;
                    Vector3 dir = Vector3.zero;
                    if (number == 2) { dir = Vector2.down; }
                    if (number == 8) { dir = Vector2.up; }
                    if (number == 4) { dir = Vector2.left; }
                    if (number == 5) { dir = Vector2.right; }
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
    public void HandleShake()
    {
        if(_shakeCooldown < shakeCooldown)
        {
            _shakeCooldown += Time.deltaTime;

            Camera.main.transform.localPosition = new Vector3(Mathf.Sin(Time.time * 50) * magnitude, 
                                                        0, 0);

            logMesh.materials[1].SetFloat("_mult", Mathf.Lerp(0.05f * magnitude, 0.001f, Mathf.Clamp01(_shakeCooldown / shakeCooldown * 0.75f)));
            radarMesh.materials[1].SetFloat("_mult", Mathf.Lerp(0.05f * magnitude, 0.028f, Mathf.Clamp01(_shakeCooldown / shakeCooldown * 0.75f)));
            actionMesh.materials[1].SetFloat("_mult", Mathf.Lerp(0.05f * magnitude, 0.001f, Mathf.Clamp01(_shakeCooldown / shakeCooldown * 0.75f)));
        }
        else if(_shakeCooldown > shakeCooldown)
        {
            _shakeCooldown = shakeCooldown;
        }
        else
        {
            Camera.main.transform.localPosition = Vector3.Lerp(Camera.main.transform.localPosition, Vector3.zero, Time.deltaTime * 5);
            logMesh.materials[1].SetFloat("_mult", 0.001f);
            radarMesh.materials[1].SetFloat("_mult", 0.028f);
            actionMesh.materials[1].SetFloat("_mult", 0.001f);
        }
    }

    private void SetMapTexture(MeshRenderer mesh, RawImage image)
    {
        Material material = mesh.materials[1];

        material.mainTexture = image.texture;

        mesh.materials[1] = material;
    }


    public void StartShake(PlayerDamaged pdEvent)
    {
        magnitude = magnitudeMult;
        _shakeCooldown = 0;
    }
}
