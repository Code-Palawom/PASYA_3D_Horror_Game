using UnityEngine;
using UnityEngine.UI;

// A single icon+bar inside the world-space billboard above a player.
public class WorldEffectIcon : MonoBehaviour {
    [SerializeField] Image icon;
    [SerializeField] Image timerBar;    // horizontal fill bar

    private float _duration;
    private float _elapsed;

    public void Setup(QuizSideEffect effect) {
        icon.sprite = effect.icon;
        icon.color = effect.tintColor;
        timerBar.color = effect.tintColor;
        _duration = effect.duration;
        _elapsed = 0f;
    }

    void Update() {
        _elapsed += Time.deltaTime;
        timerBar.fillAmount = Mathf.Clamp01(1f - (_elapsed / _duration));
    }
}