using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
public class ButtonScript : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string title;
    public TMP_Text buttonName;
    public Button button;
    public Color defaultColor = Color.white;
    public Color hoverColor = Color.yellow;
    public bool isChecked;
    void Start()
    {
        buttonName.text = " " + title;
    }
    public void OnPointerEnter(PointerEventData pointerEventData)
    {
        buttonName.color = hoverColor;
        if (!isChecked)
        {
            buttonName.text = " >" + title;
        }
    }
    public void OnPointerExit(PointerEventData pointerEventData)
    {
        buttonName.color = defaultColor;
        buttonName.text = " " + title;
    }
}
