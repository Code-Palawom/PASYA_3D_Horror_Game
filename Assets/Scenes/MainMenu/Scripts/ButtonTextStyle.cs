using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
public class ButtonTextStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] TextMeshProUGUI text;
    private float textSize;

    void Start() {
        textSize = text.fontSize;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        text.fontSize = textSize + 12;
    }

    public void OnPointerExit(PointerEventData eventData) {
        text.fontSize = textSize;
    }
}