using UnityEngine;

// Listens to AchievementManager.OnAchievementUnlocked and spawns one
// AchievementToastItemUI per unlock into a stacking container. Since
// AchievementManager.GrantAsync fires that event once per achievement even
// when several unlock in the same batch, multiple toasts naturally stack —
// no queue needed here.
//
// Setup:
//  1. Build the toast prefab (Image + up to 3 TMP_Text fields + CanvasGroup),
//     add AchievementToastItemUI to it.
//  2. Create a container RectTransform (e.g. bottom-right corner of your HUD
//     canvas) with a VerticalLayoutGroup + ContentSizeFitter (vertical:
//     Preferred Size) — that's what makes toasts auto-stack/reflow.
//  3. Add this component anywhere persistent (e.g. your HUD canvas), assign
//     toastPrefab + container, optionally skinDatabase for reward text.
public class AchievementToastSpawner : MonoBehaviour {
    [SerializeField] private AchievementToastItemUI toastPrefab;
    [SerializeField] private RectTransform container;

    [Tooltip("Optional — pass your SkinDatabaseSO to show the skin's actual display name in reward text instead of a generic \"New Skin\".")]
    [SerializeField] private SkinDatabaseSO skinDatabase;

    void Start() {
        if (AchievementManager.Instance != null) AchievementManager.Instance.OnAchievementUnlocked += HandleUnlocked;
        else Debug.LogWarning("[AchievementToastSpawner] AchievementManager.Instance not ready in Start — check script execution order / GameObject setup.");
    }

    void OnDestroy() {
        if (AchievementManager.Instance != null) AchievementManager.Instance.OnAchievementUnlocked -= HandleUnlocked;
    }

    private void HandleUnlocked(AchievementDefinitionSO def) {
        if (toastPrefab == null || container == null) {
            Debug.LogWarning("[AchievementToastSpawner] toastPrefab or container not assigned — skipping toast for '" + def.achievementId + "'.");
            return;
        }

        var toast = Instantiate(toastPrefab, container);
        toast.Show(def, skinDatabase);
    }
}