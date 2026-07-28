using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

public class HeartsUI : MonoBehaviour {
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image[] heartIcons; // ordered left-to-right, index 0 = first heart

    [Header("Loss animation")]
    [SerializeField] private float fallDistance = 150f;
    [SerializeField] private float fallDuration = 0.6f;
    [SerializeField] private float fallRotation = 60f;
    [SerializeField] private Ease fallEase = Ease.InQuad;

    private RectTransform[] heartRects;
    private Vector2[] originalPositions;

    void Start() {
        heartRects = new RectTransform[heartIcons.Length];
        originalPositions = new Vector2[heartIcons.Length];
        for (int i = 0; i < heartIcons.Length; i++) {
            heartRects[i] = heartIcons[i].rectTransform;
            originalPositions[i] = heartRects[i].anchoredPosition;
        }

        SnapTo(playerHealth.currentHearts); // sync current value immediately on spawn, no animation
    }

    public void heartsChanged(int previous, int current) {
        if (current < previous) {
            // Animate every heart lost this change (normally just one, but
            // covers multi-hit edge cases without skipping icons).
            for (int i = current; i < previous && i < heartIcons.Length; i++)
                AnimateHeartLoss(i);
        } else {
            // Heal / reset (e.g. round restart) — just snap to the new state.
            SnapTo(current);
        }
    }

    private void AnimateHeartLoss(int index) {
        var icon = heartIcons[index];
        var rect = heartRects[index];

        icon.enabled = true; // make sure it's visible for the animation
        Color startColor = icon.color;
        startColor.a = 1f;
        icon.color = startColor;

        Sequence.Create()
            .Group(Tween.Custom(rect.anchoredPosition, originalPositions[index] + new Vector2(0f, -fallDistance),
                fallDuration, onValueChange: v => rect.anchoredPosition = v, ease: fallEase))
            .Group(Tween.Custom(0f, fallRotation, fallDuration,
                onValueChange: z => rect.localRotation = Quaternion.Euler(0f, 0f, z), ease: fallEase))
            .Group(Tween.Alpha(icon, 0f, fallDuration, Ease.InQuad))
            .OnComplete(() => {
                icon.enabled = false;
                // reset transform/alpha so the icon is ready to reappear cleanly
                // if hearts are ever restored (heal pickup, round reset, etc).
                rect.anchoredPosition = originalPositions[index];
                rect.localRotation = Quaternion.identity;
                Color c = icon.color;
                c.a = 1f;
                icon.color = c;
            });
    }

    private void SnapTo(int hearts) {
        for (int i = 0; i < heartIcons.Length; i++) {
            heartIcons[i].enabled = i < hearts;
            heartRects[i].anchoredPosition = originalPositions[i];
            heartRects[i].localRotation = Quaternion.identity;
            Color c = heartIcons[i].color;
            c.a = 1f;
            heartIcons[i].color = c;
        }
    }
}