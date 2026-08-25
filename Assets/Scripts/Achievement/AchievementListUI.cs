using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Scrollable list of every achievement (local AchievementDefinitionSO +
// remote config/achievements entries), one AchievementRowUI per entry.
// Refreshes on every profile change (covers guest <-> signed-in switches,
// new unlocks, guest-to-account merges) and whenever config/achievements
// updates (so a newly-added remote achievement appears without needing an
// unrelated profile change to trigger a rebuild).
public class AchievementListUI : MonoBehaviour {
    [Tooltip("Needs a Layout Group (e.g. Vertical Layout Group) + Content Size Fitter for the list to auto-stack.")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private AchievementRowUI rowPrefab;

    private readonly List<AchievementRowUI> _spawnedRows = new();

    void Start() {
        if (AuthManager.Instance != null) AuthManager.Instance.OnPlayerStatsLoaded += HandleProfileChanged;
        else Debug.LogWarning("[AchievementListUI] AuthManager.Instance not ready in Start — check script execution order / GameObject setup.");

        RemoteAchievementSyncService.OnRemoteAchievementsUpdated += HandleRemoteAchievementsUpdated;

        Refresh();
    }

    void OnDestroy() {
        if (AuthManager.Instance != null) AuthManager.Instance.OnPlayerStatsLoaded -= HandleProfileChanged;
        RemoteAchievementSyncService.OnRemoteAchievementsUpdated -= HandleRemoteAchievementsUpdated;
    }

    private void HandleProfileChanged(PlayerProfile profile) => Refresh();
    private void HandleRemoteAchievementsUpdated(List<RemoteAchievementDefinition> _) => Refresh();

    public void Refresh() {
        if (AchievementManager.Instance == null) return;

        var profile = AuthManager.Instance?.CurrentProfile;

        // Unlocked-first (trophy-case convention), alphabetical within each
        // group. Swap this ordering out here if you'd rather group by subject
        // or trigger type instead — nothing else depends on list order.
        var defs = AchievementManager.Instance.AllDefinitions()
            .OrderByDescending(d => profile != null && profile.HasUnlockedAchievement(d.AchievementId))
            .ThenBy(d => d.DisplayName)
            .ToList();

        EnsureRowCount(defs.Count);

        for (int i = 0; i < defs.Count; i++) {
            var def = defs[i];
            var progress = AchievementManager.Instance.GetProgress(def, profile);
            _spawnedRows[i].gameObject.SetActive(true);
            _spawnedRows[i].Show(def, progress);
        }

        // Extra pooled rows from a previous, larger list — hide rather than
        // destroy, so growing the list back doesn't re-instantiate.
        for (int i = defs.Count; i < _spawnedRows.Count; i++) {
            _spawnedRows[i].gameObject.SetActive(false);
        }
    }

    private void EnsureRowCount(int count) {
        while (_spawnedRows.Count < count) {
            var row = Instantiate(rowPrefab, contentParent);
            _spawnedRows.Add(row);
        }
    }
}