using System.Collections.Generic;
using System.Linq;
using Firebase.Firestore;
using UnityEngine;

// Scrollable list of every quiz set in PlayerProfile.CompletedQuizSets,
// grouped by subject with a CompletedQuizSetSectionHeaderUI per group and a
// CompletedQuizSetRowUI per set. Groups are ordered by their own most-recent
// completion (swap the OrderByDescending below for .OrderBy(g => g.Key) if
// you'd rather go alphabetical); sets within a group are most-recent-first.
// Refreshes on every profile change (covers guest <-> signed-in switches,
// new completions, guest-to-account merges).
public class CompletedQuizSetListUI : MonoBehaviour {
    [Tooltip("Needs a Layout Group (e.g. Vertical Layout Group) + Content Size Fitter for the list to auto-stack.")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private CompletedQuizSetSectionHeaderUI headerPrefab;
    [SerializeField] private CompletedQuizSetRowUI rowPrefab;

    // Sets with no resolvable QuizRepository metadata (subject unknown) fall
    // into this catch-all group rather than being dropped from the list.
    private const string UnknownSubjectGroup = "Unknown Sets";

    private readonly List<CompletedQuizSetSectionHeaderUI> _spawnedHeaders = new();
    private readonly List<CompletedQuizSetRowUI> _spawnedRows = new();

    void Start() {
        if (AuthManager.Instance != null) AuthManager.Instance.OnPlayerStatsLoaded += HandleProfileChanged;
        else Debug.LogWarning("[CompletedQuizSetListUI] AuthManager.Instance not ready in Start — check script execution order / GameObject setup.");

        Refresh();
    }

    void OnDestroy() {
        if (AuthManager.Instance != null) AuthManager.Instance.OnPlayerStatsLoaded -= HandleProfileChanged;
    }

    private void HandleProfileChanged(PlayerProfile profile) => Refresh();

    public void Refresh() {
        var profile = AuthManager.Instance?.CurrentProfile;
        var completed = profile?.CompletedQuizSets;

        if (completed == null || completed.Count == 0) {
            SetActiveCounts(0, 0);
            return;
        }

        // Resolve metadata once per set up front, rather than repeatedly
        // during grouping/sorting below.
        var entries = completed.Select(kvp => {
            var meta = QuizRepository.Instance != null ? QuizRepository.Instance.GetMetaById(kvp.Key) : null;
            return (setId: kvp.Key, info: kvp.Value, meta);
        }).ToList();

        var groups = entries
            .GroupBy(e => string.IsNullOrEmpty(e.meta?.subject) ? UnknownSubjectGroup : e.meta.subject)
            .OrderByDescending(g => g.Max(e => e.info.CompletedAt.ToDateTime()))
            .ToList();

        int headerIndex = 0;
        int rowIndex = 0;
        int siblingIndex = 0;

        foreach (var group in groups) {
            var groupEntries = group.OrderByDescending(e => e.info.CompletedAt.ToDateTime()).ToList();

            EnsureHeaderCount(headerIndex + 1);
            var header = _spawnedHeaders[headerIndex++];
            header.gameObject.SetActive(true);
            header.Show(group.Key, groupEntries.Count);
            header.transform.SetSiblingIndex(siblingIndex++);

            foreach (var entry in groupEntries) {
                EnsureRowCount(rowIndex + 1);
                var row = _spawnedRows[rowIndex++];
                row.gameObject.SetActive(true);

                if (entry.meta != null) row.Show(entry.meta, entry.info.CompletedAt, entry.info.Correct, entry.info.Incorrect);
                else row.ShowUnknown(entry.setId, entry.info.CompletedAt, entry.info.Correct, entry.info.Incorrect);

                row.transform.SetSiblingIndex(siblingIndex++);
            }
        }

        SetActiveCounts(headerIndex, rowIndex);
    }

    private void EnsureHeaderCount(int count) {
        while (_spawnedHeaders.Count < count) _spawnedHeaders.Add(Instantiate(headerPrefab, contentParent));
    }

    private void EnsureRowCount(int count) {
        while (_spawnedRows.Count < count) _spawnedRows.Add(Instantiate(rowPrefab, contentParent));
    }

    // Extra pooled headers/rows from a previous, larger list — hidden rather
    // than destroyed, so growing the list back doesn't re-instantiate.
    private void SetActiveCounts(int activeHeaders, int activeRows) {
        for (int i = activeHeaders; i < _spawnedHeaders.Count; i++) _spawnedHeaders[i].gameObject.SetActive(false);
        for (int i = activeRows; i < _spawnedRows.Count; i++) _spawnedRows[i].gameObject.SetActive(false);
    }
}