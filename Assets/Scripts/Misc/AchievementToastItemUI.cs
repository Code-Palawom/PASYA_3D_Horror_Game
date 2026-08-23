using PrimeTween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One stacked achievement toast instance. Instantiate this prefab into a
// container that has a VerticalLayoutGroup (+ ContentSizeFitter, vertical
// "Preferred Size") — that combo gives you the Steam-style stack for free:
// each new toast pushes existing ones along, and when a toast destroys
// itself after its display duration, the layout group reflows the rest
// automatically. No manual positioning needed here beyond the slide offset.
[RequireComponent(typeof(CanvasGroup))]
public class AchievementToastItemUI : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite fallbackIcon; // shown if the achievement has no icon assigned
    [SerializeField] private TMP_Text headerText; // e.g. static "Achievement Unlocked" — set in the prefab, not here
    [SerializeField] private TMP_Text nameText;   // achievement displayName
    [SerializeField] private TMP_Text rewardText; // "+50 Coins   +100 XP   New Skin: Foo" — hidden if no reward
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rect;

    [Header("Animation")]
    [SerializeField] private float slideDistance = 60f; // pixels; slides in from this offset on the X axis
    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private Ease animEase = Ease.OutQuad;
    [SerializeField] private float displayDuration = 4f; // time fully visible before animating out

    private Sequence _sequence;

    private void Reset() {
        canvasGroup = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
    }

    // Call once, right after Instantiate. skinDatabase is optional — pass it
    // if you want reward text to show the skin's actual display name instead
    // of a generic "New Skin" fallback.
    public void Show(AchievementDefinitionSO def, SkinDatabaseSO skinDatabase = null) {
        if (iconImage != null) iconImage.sprite = def.icon != null ? def.icon : fallbackIcon;
        if (nameText != null) nameText.text = def.displayName;

        if (rewardText != null) {
            string reward = BuildRewardText(def, skinDatabase);
            rewardText.text = reward;
            rewardText.gameObject.SetActive(!string.IsNullOrEmpty(reward));
        }

        canvasGroup.alpha = 0f;
        Vector2 restPos = rect.anchoredPosition;
        Vector2 offscreenPos = restPos + Vector2.right * slideDistance;
        rect.anchoredPosition = offscreenPos;

        _sequence = Sequence.Create()
            .Group(Tween.Alpha(canvasGroup, endValue: 1f, duration: animDuration, ease: animEase))
            .Group(Tween.UIAnchoredPosition(rect, endValue: restPos, duration: animDuration, ease: animEase))
            .ChainDelay(displayDuration)
            .Chain(Tween.Alpha(canvasGroup, endValue: 0f, duration: animDuration, ease: animEase))
            .Group(Tween.UIAnchoredPosition(rect, endValue: offscreenPos, duration: animDuration, ease: animEase))
            .ChainCallback(() => Destroy(gameObject));
    }

    private void OnDestroy() => _sequence.Stop();

    private static string BuildRewardText(AchievementDefinitionSO def, SkinDatabaseSO skinDatabase) {
        var parts = new List<string>();
        if (def.rewardCoins > 0) parts.Add($"+{def.rewardCoins} Coins");
        if (def.rewardXp > 0) parts.Add($"+{def.rewardXp} XP");

        if (!string.IsNullOrEmpty(def.rewardSkinId)) {
            var skin = skinDatabase != null ? skinDatabase.GetById(def.rewardSkinId) : null;
            parts.Add(skin != null ? $"New Skin: {skin.name}" : "New Skin");
        }

        return string.Join("   ", parts);
    }
}