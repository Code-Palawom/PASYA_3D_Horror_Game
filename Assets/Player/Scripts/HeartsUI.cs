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
        // Guards against instances that never ran Start() (e.g. this
        // GameObject/Canvas was inactive at spawn on a non-owner client) —
        // there's nothing to animate on this client, so just no-op safely.
        if (heartIcons == null || heartIcons.Length == 0) return;

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
        if (index < 0 || index >= heartIcons.Length) return;

        var icon = heartIcons[index];
        if (icon == null) return; // slot not assigned in the Inspector — skip, don't throw

        // Fall back gracefully if Start() hasn't populated the caches yet
        // (shouldn't normally happen once heartsChanged is owner-gated, but
        // keeps this method safe to call from anywhere).
        RectTransform rect = (heartRects != null && index < heartRects.Length && heartRects[index] != null)
            ? heartRects[index]
            : icon.rectTransform;

        Vector2 originalPos = (originalPositions != null && index < originalPositions.Length)
            ? originalPositions[index]
            : rect.anchoredPosition;

        icon.enabled = true; // make sure it's visible for the animation
        Color startColor = icon.color;
        startColor.a = 1f;
        icon.color = startColor;

        Sequence.Create()
            .Group(Tween.Custom(rect.anchoredPosition, originalPos + new Vector2(0f, -fallDistance),
                fallDuration, onValueChange: v => rect.anchoredPosition = v, ease: fallEase))
            .Group(Tween.Custom(0f, fallRotation, fallDuration,
                onValueChange: z => rect.localRotation = Quaternion.Euler(0f, 0f, z), ease: fallEase))
            .Group(Tween.Alpha(icon, 0f, fallDuration, Ease.InQuad))
            .OnComplete(() => {
                icon.enabled = false;
                // reset transform/alpha so the icon is ready to reappear cleanly
                // if hearts are ever restored (heal pickup, round reset, etc).
                rect.anchoredPosition = originalPos;
                rect.localRotation = Quaternion.identity;
                Color c = icon.color;
                c.a = 1f;
                icon.color = c;
            });
    }

    private void SnapTo(int hearts) {
        if (heartIcons == null) return;

        for (int i = 0; i < heartIcons.Length; i++) {
            if (heartIcons[i] == null) continue;

            heartIcons[i].enabled = i < hearts;

            if (heartRects != null && i < heartRects.Length && heartRects[i] != null) {
                heartRects[i].anchoredPosition = (originalPositions != null && i < originalPositions.Length)
                    ? originalPositions[i]
                    : heartRects[i].anchoredPosition;
                heartRects[i].localRotation = Quaternion.identity;
            }

            Color c = heartIcons[i].color;
            c.a = 1f;
            heartIcons[i].color = c;
        }
    }
}