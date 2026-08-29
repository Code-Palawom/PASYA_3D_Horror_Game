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
[RequireComponent(typeof(AudioSource))]
public class AchievementToastItemUI : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite fallbackIcon; // shown if the achievement has no icon assigned
    [SerializeField] private TMP_Text headerText; // e.g. static "Achievement Unlocked" — set in the prefab, not here
    [SerializeField] private TMP_Text nameText;   // achievement displayName
    [SerializeField] private TMP_Text rewardText; // "+50 Coins   +100 XP   New Skin: Foo" — hidden if no reward
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rect;
    [SerializeField] private AudioSource audioSource; // 2D UI sound; set AudioSource.spatialBlend = 0 in the prefab

    [Header("Animation")]
    [SerializeField] private float slideDistance = 60f; // pixels; slides in from this offset on the X axis
    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private Ease animEase = Ease.OutQuad;
    [SerializeField] private float displayDuration = 4f; // time fully visible before animating out

    [Header("Audio")]
    [SerializeField] private AudioClip normalUnlockClip; // played for regular achievements
    [SerializeField] private AudioClip epicUnlockClip;   // played when def.IsEpic is true
    [Range(0f, 1f)]
    [SerializeField] private float unlockVolume = 1f;

    private Sequence _sequence;

    private void Reset() {
        canvasGroup = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        audioSource = GetComponent<AudioSource>();
    }

    // Call once, right after Instantiate. skinDatabase is optional — pass it
    // if you want reward text to show the skin's actual display name instead
    // of a generic "New Skin" fallback. iconDatabase is optional too — pass
    // it to resolve a remote achievement's IconId to a bundled sprite;
    // without it (or if IconId doesn't match anything), remote achievements
    // fall back to fallbackIcon. Accepts IAchievementDefinition (not
    // AchievementDefinitionSO directly) so this same prefab renders both local
    // ScriptableObject achievements and ones synced from Firestore via
    // RemoteAchievementDefinition.
    public void Show(IAchievementDefinition def, SkinDatabaseSO skinDatabase = null, AchievementIconDatabaseSO iconDatabase = null) {
        if (iconImage != null) iconImage.sprite = ResolveIcon(def, iconDatabase);
        if (nameText != null) nameText.text = def.DisplayName;

        if (rewardText != null) {
            string reward = BuildRewardText(def, skinDatabase);
            rewardText.text = reward;
            rewardText.gameObject.SetActive(!string.IsNullOrEmpty(reward));
        }

        PlayUnlockSound(def);

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

    private void PlayUnlockSound(IAchievementDefinition def) {
        if (audioSource == null) return;

        AudioClip clip = def.Tier == AchievementTier.Epic ? epicUnlockClip : normalUnlockClip;
        if (clip != null) audioSource.PlayOneShot(clip, unlockVolume);
    }

    private void OnDestroy() => _sequence.Stop();

    private Sprite ResolveIcon(IAchievementDefinition def, AchievementIconDatabaseSO iconDatabase) {
        if (def.Icon != null) return def.Icon;
        Sprite resolved = iconDatabase != null ? iconDatabase.GetById(def.IconId) : null;
        return resolved != null ? resolved : fallbackIcon;
    }

    private static string BuildRewardText(IAchievementDefinition def, SkinDatabaseSO skinDatabase) {
        var parts = new List<string>();
        if (def.RewardCoins > 0) parts.Add($"+{def.RewardCoins} Coins");
        if (def.RewardXp > 0) parts.Add($"+{def.RewardXp} XP");

        if (!string.IsNullOrEmpty(def.RewardSkinId)) {
            var skin = skinDatabase != null ? skinDatabase.GetById(def.RewardSkinId) : null;
            parts.Add(skin != null ? $"New Skin: {skin.name}" : "New Skin");
        }

        return string.Join("   ", parts);
    }
}