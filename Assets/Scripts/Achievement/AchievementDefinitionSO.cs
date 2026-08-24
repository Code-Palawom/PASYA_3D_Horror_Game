using UnityEngine;

// How an achievement gets checked/unlocked. Start by only authoring
// StatThreshold entries — every stat here already flows through
// AuthManager.OnPlayerStatsLoaded on every mutation (guest or signed-in,
// optimistic or confirmed), so those need zero new instrumentation anywhere
// else in the codebase. CustomEvent is here so one-off achievements (e.g.
// "survived an enemy encounter") can be added later without restructuring —
// gameplay code just calls AchievementManager.Instance.ReportEvent(eventKey)
// at the moment it happens.
public enum AchievementTriggerType {
    StatThreshold,
    CustomEvent,
    SubjectCompletion,
    QuizSetCompletion
}

// Which PlayerProfile stat a StatThreshold achievement watches.
// Add new cases here + in AchievementManager.GetStatValue as new stats appear.
public enum AchievementStat {
    GamesPlayed,
    Xp,
    CorrectAnswers,
    IncorrectAnswers,
    Coins,
    OwnedSkinsCount,
    CompletedQuizSetsCount
}

[CreateAssetMenu(fileName = "Achievement", menuName = "Pasya/Achievement")]
public class AchievementDefinitionSO : ScriptableObject, IAchievementDefinition {
    [Tooltip("Stable id stored in PlayerProfile.UnlockedAchievementIds. " +
             "Never rename after players can have already unlocked it.")]
    public string achievementId;

    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Tooltip("If true, UI should show a '???' placeholder (name/icon/description) until unlocked.")]
    public bool hidden;

    public AchievementTriggerType triggerType = AchievementTriggerType.StatThreshold;

    [Header("Stat Threshold (used when triggerType == StatThreshold)")]
    public AchievementStat stat;
    public long threshold;

    [Header("Custom Event (used when triggerType == CustomEvent)")]
    [Tooltip("Matches the string passed to AchievementManager.Instance.ReportEvent(...).")]
    public string eventKey;

    [Header("Subject Completion (used when triggerType == SubjectCompletion)")]
    [Tooltip("Matches QuizSetMetaEntry.subject exactly.")]
    public string subject;

    [Range(1f, 100f)]
    [Tooltip("100 = every playable set in the subject must be completed. Lower values allow partial completion (e.g. 50 = half the subject's sets).")]
    public float requiredCompletionPercent = 100f;

    [Header("Quiz Set Completion (used when triggerType == QuizSetCompletion)")]
    [Tooltip("Matches QuizSetMetaEntry.setId exactly — completing this one specific set unlocks the achievement.")]
    public string requiredSetId;

    [Header("Reward (0 / blank = none)")]
    public int rewardCoins;
    public int rewardXp;
    [Tooltip("Optional skinId to grant on unlock — must match a CharacterSkinSO.skinId.")]
    public string rewardSkinId;

    // Explicit interface implementation — maps the Inspector-facing fields
    // above onto IAchievementDefinition so AchievementManager can evaluate
    // this alongside RemoteAchievementDefinition without either one caring
    // where the other came from. Kept explicit (not public properties) so
    // existing code (and AchievementDefinitionSOEditor) keeps using the
    // lowercase fields directly with zero changes.
    string IAchievementDefinition.AchievementId => achievementId;
    string IAchievementDefinition.DisplayName => displayName;
    string IAchievementDefinition.Description => description;
    Sprite IAchievementDefinition.Icon => icon;
    bool IAchievementDefinition.Hidden => hidden;
    AchievementTriggerType IAchievementDefinition.TriggerType => triggerType;
    AchievementStat IAchievementDefinition.Stat => stat;
    long IAchievementDefinition.Threshold => threshold;
    string IAchievementDefinition.EventKey => eventKey;
    string IAchievementDefinition.Subject => subject;
    float IAchievementDefinition.RequiredCompletionPercent => requiredCompletionPercent;
    string IAchievementDefinition.RequiredSetId => requiredSetId;
    int IAchievementDefinition.RewardCoins => rewardCoins;
    int IAchievementDefinition.RewardXp => rewardXp;
    string IAchievementDefinition.RewardSkinId => rewardSkinId;
}