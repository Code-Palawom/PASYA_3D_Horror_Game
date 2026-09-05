using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

public class AnimatedToggle : MonoBehaviour {
    public Toggle toggle;
    public RectTransform knob;
    public Image background;
    public Color offColor = new Color(0.25f, 0.25f, 0.25f);
    public Color onColor = new Color(0.2f, 0.6f, 1f);
    public float onX = 20f;
    public float offX = -20f;
    public float duration = 0.15f;

    void Start() {
        // Set instantly, no animation, no warning
        knob.anchoredPosition = new Vector2(toggle.isOn ? onX : offX, knob.anchoredPosition.y);
        background.color = toggle.isOn ? onColor : offColor;

        toggle.onValueChanged.AddListener(SetState);
    }

    void SetState(bool isOn) {
        Tween.UIAnchoredPositionX(knob, isOn ? onX : offX, duration, Ease.OutQuad);
        Tween.Color(background, isOn ? onColor : offColor, duration, Ease.OutQuad);
    }
}