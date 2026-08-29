using UnityEngine;

// Common surface AchievementManager evaluates against, regardless of whether
// a definition is a local AchievementDefinitionSO (StatThreshold/CustomEvent,
// baked into the build — needs gameplay code or a fixed enum, so it stays
// local) or a RemoteAchievementDefinition (SubjectCompletion/QuizSetCompletion,
// live-synced from Firestore config/achievements — see RemoteAchievementSyncService).
public interface IAchievementDefinition {
    string AchievementId { get; }
    string DisplayName { get; }
    string Description { get; }
    Sprite Icon { get; }
    bool Hidden { get; }

    // Presentational only (e.g. picks unlock sound/VFX) — not used in evaluation.
    AchievementTier Tier { get; }

    AchievementTriggerType TriggerType { get; }

    // StatThreshold
    AchievementStat Stat { get; }
    long Threshold { get; }

    // CustomEvent
    string EventKey { get; }

    // SubjectCompletion
    string Subject { get; }
    float RequiredCompletionPercent { get; }

    // QuizSetCompletion
    string RequiredSetId { get; }

    // Reward
    int RewardCoins { get; }
    int RewardXp { get; }
    string RewardSkinId { get; }
}