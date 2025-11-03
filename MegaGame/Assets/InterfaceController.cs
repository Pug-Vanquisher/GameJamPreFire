using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InterfaceController : MonoBehaviour
{

    [SerializeField] private CommandConsoleUI console;

    [SerializeField] private RawImage mapImage;

    [SerializeField] private MeshRenderer mapMesh;

    [SerializeField] private GameObject[] buttons;

    [SerializeField] private float buttonStroke;
    [SerializeField] private float buttonSpeed;

    [SerializeField] private Vector3 minCameraRotation;
    [SerializeField] private Vector3 maxCameraRotation;


    private int mouseInterracted;

    [SerializeField] private float stepCooldown;
    [SerializeField] private float magnitude;
    [SerializeField] private Vector3[] walkShakes;
    private float _stepCooldown;
    private int activeLeg;
    private static bool moveShakes;
    private Vector3 cameraInitPose = new Vector3(18.1769905f, -11.5327806f, -4.4314208f);

    private List<Vector3> initPoses;

    private void Start()
    {
        moveShakes = false;
        for (int i = 0; i < buttons.Length; i++)
        {
            initPoses.Add(buttons[i].transform.position);
        }
    }

    // Update is called once per frame
    void Update()
    {
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, cameraInitPose, Time.deltaTime * 5);

        SetMapTexture(mapMesh, mapImage);
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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ShakeOnMove();
        }
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
    }

    private void HandleShake()
    {
        if(_stepCooldown < stepCooldown)
        {
            _stepCooldown += Time.deltaTime;
            Camera.main.transform.position += Vector3.forward * Mathf.Sin(Time.time * (_stepCooldown/ stepCooldown)) * magnitude;
        }
        else { _stepCooldown = stepCooldown; }


        if(moveShakes && _stepCooldown >= stepCooldown)
        {
            moveShakes = false;
            _stepCooldown = 0;
            if (activeLeg == 1) { activeLeg = 0; } else { activeLeg = 1; }
            Camera.main.transform.forward = Vector3.Lerp(Camera.main.transform.forward, walkShakes[activeLeg].normalized, 0.05f);//AngleAxis(0.01f, Vector3.Cross(, Camera.main.transform.forward));
            Camera.main.transform.position = cameraInitPose + walkShakes[activeLeg];
        }
    }

    private void SetMapTexture(MeshRenderer mesh, RawImage image)
    {
        Material material = mesh.materials[1];

        material.mainTexture = image.texture;

        mesh.materials[1] = material;
    }

    public static void ShakeOnMove()
    {
        moveShakes = true;
    }

}
