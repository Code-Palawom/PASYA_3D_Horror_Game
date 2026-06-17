using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One entry in the HUD side-effect tray. Shows icon + radial fill timer + label.
public class HUDEffectEntry : MonoBehaviour {
    [SerializeField] Image iconImage;
    [SerializeField] Image timerFill;       // set Image Type to Filled, Fill Method to Radial 360
    [SerializeField] TMP_Text timerLabel;
    [SerializeField] TMP_Text effectNameLabel;

    private float _duration;
    private float _elapsed;

    public void Setup(QuizSideEffect effect) {
        iconImage.sprite = effect.icon;
        iconImage.color = effect.tintColor;
        effectNameLabel.text = effect.effectName;
        timerFill.color = effect.tintColor;
        _duration = effect.duration;
        _elapsed = 0f;
    }

    void Update() {
        _elapsed += Time.deltaTime;
        float remaining = Mathf.Max(0f, _duration - _elapsed);

        timerFill.fillAmount = 1f - (_elapsed / _duration);
        timerLabel.text = remaining.ToString("F1") + "s";
    }
}