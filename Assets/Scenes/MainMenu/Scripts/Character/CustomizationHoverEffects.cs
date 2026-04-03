using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CustomizationHoverEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color textColorOnHover;

    private FontStyles textNormal;
    private Color textColor;

    void Start() {
        textNormal = text.fontStyle;
        textColor = text.color;
    }
    public void OnPointerEnter(PointerEventData eventData) {
        DecorateText();
    }

    public void OnPointerExit(PointerEventData eventData) {
        UndecorateText();
    }

    private void DecorateText() {
        if(text.color == textColor) {
            text.color = textColorOnHover;
            text.fontStyle = FontStyles.Bold;
        }else{
            text.fontStyle = textNormal;
        }
    }

    private void UndecorateText() {
        if(text.color == textColorOnHover) {
            text.color = textColor;
            text.fontStyle = textNormal;
        }else{
            text.fontStyle = FontStyles.Bold;
        }
    }
}
