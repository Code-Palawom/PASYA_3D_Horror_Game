using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonStyling : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Color textColor;
    [SerializeField] private Color textColorOnHover;

    private FontStyles textNormal;

    void Start() {
        textNormal = text.fontStyle;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        text.color = textColorOnHover;
        text.fontStyle = FontStyles.Bold;
    }

    public void OnPointerExit(PointerEventData eventData) {
        text.color = textColor;
        text.fontStyle = textNormal;
    }
}
