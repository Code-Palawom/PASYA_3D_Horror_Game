using UnityEngine;

// Achievement definition sourced from Firestore config/achievements — see
// RemoteAchievementSyncService for the document shape and parsing.
//
// StatThreshold, SubjectCompletion, and QuizSetCompletion are supported
// remotely. CustomEvent is not — it needs gameplay code to call
// AchievementManager.ReportEvent(eventKey) at a specific moment, so it stays
// a local AchievementDefinitionSO only.
//
// No direct Sprite support: Firestore can't ship new sprites into an
// already-built client. Instead, "IconId" is a free-text key (set in the
// admin dashboard) resolved locally against AchievementIconDatabaseSO — so
// new remote achievements can reuse any sprite already bundled in the build.
// If IconId is blank/unset or doesn't match an entry in that database,
// callers fall back to AchievementToastItemUI/AchievementRowUI's fallbackIcon.
public class RemoteAchievementDefinition : IAchievementDefinition {
    public string AchievementId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string IconId { get; set; }
    public bool Hidden { get; set; }

    // Defaults to Normal if the Firestore doc doesn't set it — see
    // RemoteAchievementSyncService for how this gets parsed (e.g. a
    // "tier": "epic" string field, missing/unrecognized -> Normal).
    public AchievementTier Tier { get; set; } = AchievementTier.Normal;

    public AchievementTriggerType TriggerType { get; set; }

    public AchievementStat Stat { get; set; }
    public long Threshold { get; set; }

    public string Subject { get; set; }
    public float RequiredCompletionPercent { get; set; }

    public string RequiredSetId { get; set; }

    public int RewardCoins { get; set; }
    public int RewardXp { get; set; }
    public string RewardSkinId { get; set; }

    // No direct sprite reference remotely — see IconId + class comment.
    public Sprite Icon => null;
    // Not used remotely — CustomEvent stays local-only (see class comment).
    public string EventKey => null;
}