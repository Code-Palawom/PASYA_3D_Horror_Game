using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonStyling : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    private Image imageComponent;
    public Color textBgColor;

    void Start() {
        imageComponent = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        imageComponent.color = textBgColor;
    }

    public void OnPointerExit(PointerEventData eventData) {
        imageComponent.color = new Color(textBgColor.r, textBgColor.g, textBgColor.b, 0f);
    }
}
