using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Scrollable list of every achievement (local AchievementDefinitionSO +
// remote config/achievements entries), one AchievementRowUI per entry.
// Refreshes on every profile change (covers guest <-> signed-in switches,
// new unlocks, guest-to-account merges) and whenever config/achievements
// updates (so a newly-added remote achievement appears without needing an
// unrelated profile change to trigger a rebuild). Also includes a minimal
// "(Legacy)" row for any id already in the player's UnlockedAchievements
// map that's no longer an active definition (removed locally or deleted
// from config/achievements) — see Refresh's def == null handling.
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
        var manager = AchievementManager.Instance;

        // def == null marks a legacy entry — an id the player already has in
        // UnlockedAchievements map, but that's no longer in AllDefinitions()
        // (local one removed from the build, or remote one deleted from
        // config/achievements). Nothing is preserved about what it used to
        // look like — see RemoteAchievementSyncService — so these render with
        // just the bare id (AchievementRowUI.Show handles def == null).
        var entries = new List<(IAchievementDefinition def, string legacyId)>();
        var seenIds = new HashSet<string>();

        foreach (var def in manager.AllDefinitions()) {
            entries.Add((def, null));
            seenIds.Add(def.AchievementId);
        }

        if (profile?.UnlockedAchievements != null) {
            foreach (var id in profile.UnlockedAchievements.Keys) {
                if (seenIds.Contains(id)) continue;
                entries.Add((null, id));
                seenIds.Add(id);
            }
        }

        // Unlocked-first (trophy-case convention), alphabetical within each
        // group. Swap this ordering out here if you'd rather group by subject
        // or trigger type instead — nothing else depends on list order.
        entries = entries
            .OrderByDescending(e => e.def == null || (profile != null && profile.HasUnlockedAchievement(e.def.AchievementId)))
            .ThenBy(e => e.def?.DisplayName ?? e.legacyId)
            .ToList();

        EnsureRowCount(entries.Count);

        for (int i = 0; i < entries.Count; i++) {
            var (def, legacyId) = entries[i];
            _spawnedRows[i].gameObject.SetActive(true);

            if (def == null) {
                _spawnedRows[i].ShowLegacy(legacyId);
            } else {
                var progress = manager.GetProgress(def, profile);
                _spawnedRows[i].Show(def, progress);
            }
        }

        // Extra pooled rows from a previous, larger list — hide rather than
        // destroy, so growing the list back doesn't re-instantiate.
        for (int i = entries.Count; i < _spawnedRows.Count; i++) {
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