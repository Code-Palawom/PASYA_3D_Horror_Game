using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class QuizAssignmentCoordinator : MonoBehaviour {
    public static QuizAssignmentCoordinator Instance { get; private set; }

    // Per-difficulty shuffled queues
    private Dictionary<QuestionDifficulty, Queue<QuestionRuntime>> _pools = new();

    void Awake() {
        Instance = this;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (GameSessionManager.Instance == null) {
            Debug.LogError("[QuizAssignmentCoordinator] GameSessionManager not ready in Awake. " +
                           "Ensure it is spawned before the level scene loads.");
            return;
        }

        string setName = GameSessionManager.Instance.SelectedQuizSetName.Value.ToString();
        if (!string.IsNullOrEmpty(setName))
            Initialize(setName);
    }

    // Call this on the server before any gate spawns.
    public void Initialize(string setName) {
        _pools.Clear();

        var set = QuizRepository.Instance.GetSetByName(setName);
        if (set == null) {
            var availableNames = QuizRepository.Instance
                .LoadMetaCache()
                .Select(e => e.name)
                .ToList();

            Debug.LogError($"[QuizAssignmentCoordinator] Set '{setName}' not found. " +
                           $"Available sets: {string.Join(", ", availableNames)}");
            return;
        }

        Debug.Log($"[QuizAssignmentCoordinator] Loading set '{setName}' " +
                  $"— {set.questions.Count} total questions.");

        var diffBreakdown = set.questions
            .GroupBy(q => q.difficulty)
            .Select(g => $"{g.Key}: {g.Count()}");
        Debug.Log($"[QuizAssignmentCoordinator] Difficulty breakdown: " +
                  string.Join(", ", diffBreakdown));

        var byDifficulty = set.questions.GroupBy(q => q.difficulty);

        foreach (var group in byDifficulty) {
            var shuffled = group.OrderBy(_ => Random.value);
            _pools[group.Key] = new Queue<QuestionRuntime>(shuffled);
            Debug.Log($"[QuizAssignmentCoordinator] Pool '{group.Key}' → {_pools[group.Key].Count} questions queued.");
        }

        Debug.Log($"[QuizAssignmentCoordinator] Initialized. " +
                  string.Join(", ", _pools.Select(p => $"{p.Key}:{p.Value.Count}")));
    }

    // Claims a unique question for a gate.
    // Returns false if the pool for this difficulty is exhausted.
    public bool ClaimQuestion(QuestionDifficulty difficulty, out QuestionRuntime runtime) {
        runtime = null;

        if (!_pools.TryGetValue(difficulty, out var pool)) {
            Debug.LogError($"[QuizAssignmentCoordinator] No pool found for difficulty '{difficulty}'. " +
                           $"Available pools: {string.Join(", ", _pools.Keys)}. " +
                           $"Is Initialize() called? _pools.Count = {_pools.Count}");
            return false;
        }

        if (pool.Count == 0) {
            Debug.LogError($"[QuizAssignmentCoordinator] Pool exhausted for '{difficulty}'. " +
                           $"You have more gates than questions in this difficulty tier.");
            return false;
        }

        runtime = pool.Dequeue();
        return true;
    }
}