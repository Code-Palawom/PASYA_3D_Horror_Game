using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GamePlayUIToggle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    [SerializeField] private Image icon;
    [SerializeField] private bool isHold;

    private bool isActive = false;

    public void OnPointerDown(PointerEventData eventData) {
        var c = icon.color;
        if (isHold) {
            icon.color = new Color(c.r, c.g, c.b, 0.50f);
            return;
        }

        isActive = !isActive;
        if (isActive) {
            icon.color = new Color(c.r, c.g, c.b, 0.50f);
        } else {
            icon.color = new Color(c.r, c.g, c.b, 0.25f);
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (isHold) {
            var c = icon.color;
            icon.color = new Color(c.r, c.g, c.b, 0.25f);
        }
    }
}
