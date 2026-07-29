using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

// Session-only task progress. One TaskSet asset is assigned per map/scene,
// so different maps can have completely different tasks. Server owns the
// NetworkList/NetworkVariable state; clients read it for UI.
//
// Tasks are organized into sequential groups: only one group is "active"
// at a time (i.e. shown in UI), and the next group only becomes active
// once every task in the current group is completed. Correct answers are
// matched against ALL tasks though, not just the active group's — so a
// gate tied to a future group's task banks that progress immediately,
// and the task can already be done (or partway there) once its group
// becomes active. Within the active group, at most
// maxVisibleTasks incomplete tasks are shown at once (already-completed
// tasks in that group stay visible, struck through) — completing one
// reveals the next queued task in the group.
public struct TaskProgressNetworked : INetworkSerializable, IEquatable<TaskProgressNetworked> {
    public FixedString64Bytes taskId;
    public TaskType taskType;
    public int currentCount;
    public int requiredCount;
    public bool completed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref taskId);
        serializer.SerializeValue(ref taskType);
        serializer.SerializeValue(ref currentCount);
        serializer.SerializeValue(ref requiredCount);
        serializer.SerializeValue(ref completed);
    }

    public bool Equals(TaskProgressNetworked other) =>
        taskId.Equals(other.taskId) &&
        taskType == other.taskType &&
        currentCount == other.currentCount &&
        requiredCount == other.requiredCount &&
        completed == other.completed;
}

public class TaskManager : NetworkBehaviour {
    public static TaskManager Instance { get; private set; }

    [Tooltip("Assign the TaskSet asset for THIS map. Different maps use different assets.")]
    [SerializeField] TaskSet taskSet;

    [Tooltip("Max incomplete tasks shown at once from the active group. Completed tasks in " +
             "the group still show (struck through); this only caps how many are queued up.")]
    [SerializeField] int maxVisibleTasks = 3;

    private NetworkList<TaskProgressNetworked> _progress;
    private NetworkVariable<int> _activeGroupIndex = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Dictionary<string, TaskDefinition> _defsById;

    // UI subscribes to this instead of polling.
    public event Action OnTasksChanged;

    public string CurrentGroupName {
        get {
            int idx = _activeGroupIndex.Value;
            if (taskSet == null || idx < 0 || idx >= taskSet.groups.Count) return "";
            return taskSet.groups[idx].groupName;
        }
    }

    public bool AllGroupsCompleted =>
        taskSet == null || _activeGroupIndex.Value >= taskSet.groups.Count;

    void Awake() {
        Instance = this;
        _progress = new NetworkList<TaskProgressNetworked>(
            new List<TaskProgressNetworked>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn() {
        BuildDefinitionLookup(); // both server and clients need this for titles/descriptions
        _progress.OnListChanged += _ => OnTasksChanged?.Invoke();
        _activeGroupIndex.OnValueChanged += (_, _) => OnTasksChanged?.Invoke();

        if (IsServer) SeedProgressFromTaskSet();
    }

    public override void OnNetworkDespawn() {
        _progress.OnListChanged -= _ => OnTasksChanged?.Invoke();
        _activeGroupIndex.OnValueChanged -= (_, _) => OnTasksChanged?.Invoke();
    }

    void BuildDefinitionLookup() {
        _defsById = new Dictionary<string, TaskDefinition>();
        if (taskSet?.groups == null) return;

        foreach (var group in taskSet.groups)
            foreach (var def in group.tasks) {
                if (string.IsNullOrEmpty(def.taskId)) {
                    Debug.LogWarning($"[TaskManager] Task '{def.name}' has no taskId set, skipping.");
                    continue;
                }
                _defsById[def.taskId] = def;
            }
    }

    void SeedProgressFromTaskSet() {
        foreach (var group in taskSet.groups)
            foreach (var def in group.tasks) {
                if (string.IsNullOrEmpty(def.taskId)) continue;

                _progress.Add(new TaskProgressNetworked {
                    taskId = def.taskId,
                    taskType = def.type,
                    currentCount = 0,
                    requiredCount = def.type == TaskType.GenericCount ? Mathf.Max(1, def.requiredCount) : 1,
                    completed = false
                });
            }
    }

    // Call from server code whenever a gate is answered correctly
    // (NetworkedQuizGate.RequestUnlockRpc calls this already). Matches
    // against ALL tasks, not just the active group's — answering a gate
    // that belongs to a future group's task banks that progress now, so
    // the task can already be complete (or partially done) the moment
    // its group becomes active.
    public void NotifyGateSolved(string gateId, QuestionDifficulty difficulty, IEnumerable<string> tags = null) {
        if (!IsServer || _defsById == null) return;

        for (int i = 0; i < _progress.Count; i++) {
            var p = _progress[i];
            if (p.completed) continue;
            if (!_defsById.TryGetValue(p.taskId.ToString(), out var def)) continue;

            bool matches = def.type switch {
                TaskType.SpecificGate => !string.IsNullOrEmpty(gateId) && def.targetGateId == gateId,
                TaskType.GenericCount => true,
                TaskType.DifficultyOrTag => def.targetDifficulty == difficulty &&
                    (string.IsNullOrEmpty(def.targetTag) || (tags != null && tags.Contains(def.targetTag))),
                _ => false
            };
            if (!matches) continue;

            p.currentCount++;
            if (p.currentCount >= p.requiredCount) p.completed = true;
            _progress[i] = p; // NetworkList requires reassigning the struct to sync it
        }

        // Chain-advance in case banked progress already finished the new
        // active group too (e.g. everything was answered out of order).
        while (!AllGroupsCompleted && GroupIsFullyComplete(taskSet.groups[_activeGroupIndex.Value])) {
            _activeGroupIndex.Value++;
        }
    }

    bool GroupIsFullyComplete(TaskGroup group) {
        foreach (var def in group.tasks) {
            if (!TryGetProgress(def.taskId, out var p) || !p.completed) return false;
        }
        return true;
    }

    bool TryGetProgress(string taskId, out TaskProgressNetworked progress) {
        foreach (var p in _progress) {
            if (p.taskId.ToString() == taskId) { progress = p; return true; }
        }
        progress = default;
        return false;
    }

    // For UI: definition (title/description) paired with live progress,
    // already filtered to the active group and capped at maxVisibleTasks
    // incomplete entries (completed entries in the group are unlimited).
    public IReadOnlyList<(TaskDefinition def, TaskProgressNetworked progress)> GetTasksForUI() {
        var result = new List<(TaskDefinition, TaskProgressNetworked)>();
        if (_defsById == null || AllGroupsCompleted) return result;

        var group = taskSet.groups[_activeGroupIndex.Value];
        int shownIncomplete = 0;

        foreach (var def in group.tasks) {
            if (!TryGetProgress(def.taskId, out var progress)) continue;

            if (progress.completed) {
                result.Add((def, progress));
            } else if (shownIncomplete < maxVisibleTasks) {
                result.Add((def, progress));
                shownIncomplete++;
            }
        }
        return result;
    }
}