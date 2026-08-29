using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row in the achievements list. Populated by AchievementListUI per
// IAchievementDefinition — works identically for local (AchievementDefinitionSO)
// and remote (RemoteAchievementDefinition) achievements, and for guest or
// signed-in profiles, since it only reads through IAchievementDefinition +
// AchievementManager.AchievementProgress — never the concrete types directly.
public class AchievementRowUI : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite fallbackIcon; // shown when an unlocked/visible achievement has no icon assigned
    [SerializeField] private Sprite hiddenIcon;   // shown for hidden + not-yet-unlocked achievements; falls back to fallbackIcon if unset
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Slider progressBar; // expects 0..1 range
    [SerializeField] private CanvasGroup lockedDimGroup; // optional — dims the whole row while locked
    [SerializeField] private float lockedAlpha = 0.5f;

    public void Show(IAchievementDefinition def, AchievementManager.AchievementProgress progress) {
        // Hidden achievements show a "???" placeholder for everything —
        // name, description, AND progress — until unlocked. Showing "3/5"
        // on a secret achievement would leak its existence/target early.
        bool showHiddenPlaceholder = def.Hidden && !progress.IsUnlocked;

        if (iconImage != null) {
            Sprite sprite = showHiddenPlaceholder
                ? (hiddenIcon != null ? hiddenIcon : fallbackIcon)
                : (def.Icon != null ? def.Icon : fallbackIcon);
            iconImage.sprite = sprite;
        }

        if (nameText != null) nameText.text = showHiddenPlaceholder ? "???" : def.DisplayName;

        if (descriptionText != null) {
            string desc = showHiddenPlaceholder ? "???" : def.Description;
            descriptionText.text = desc;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(desc));
        }

        // No point showing a progress bar/text once already unlocked, or for
        // trigger types with nothing measurable (CustomEvent) — see
        // AchievementManager.GetProgress's HasProgress.
        bool showProgress = progress.HasProgress && !progress.IsUnlocked && !showHiddenPlaceholder;

        if (progressText != null) {
            progressText.gameObject.SetActive(showProgress);
            if (showProgress) progressText.text = progress.DisplayText;
        }

        if (progressBar != null) {
            progressBar.gameObject.SetActive(showProgress);
            if (showProgress) progressBar.value = progress.Percent01;
        }

        if (lockedDimGroup != null) {
            lockedDimGroup.alpha = progress.IsUnlocked ? 1f : lockedAlpha;
        }
    }

    // For an achievement id the player already has unlocked (see
    // PlayerProfile.UnlockedAchievements) but that no longer has an active
    // definition anywhere on the client — removed from a later build (local),
    // or deleted from config/achievements (remote; RemoteAchievementSyncService's
    // cache drops it the moment that syncs). There's nothing preserved about
    // what it used to look like, so this only ever has the bare id to show.
    public void ShowLegacy(string achievementId) {
        if (iconImage != null) iconImage.sprite = fallbackIcon;
        if (nameText != null) nameText.text = $"{achievementId} (Legacy)";

        if (descriptionText != null) descriptionText.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);
        if (progressBar != null) progressBar.gameObject.SetActive(false);

        // Always "unlocked" styling — a legacy row only ever exists because
        // the player already has this id in UnlockedAchievements.
        if (lockedDimGroup != null) lockedDimGroup.alpha = 1f;
    }
}