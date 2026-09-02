using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Server-side. Spawns enemy NetworkObjects at points defined in a
// SceneEnemySpawnData asset, using a matching ScenePatrolData asset
// (paired by groupId) to assign each enemy's patrol route. Supports
// multiple enemy prefabs/types — a spawn point can pin a specific type
// via EnemySpawnPointData.enemyType, or leave it empty for a random pick.
// Attach to an empty GameObject with a NetworkObject, place in your
// scene, and assign enemyTypes + the two data assets for that scene.
public class EnemySpawnManager : NetworkBehaviour {
    [System.Serializable]
    public class EnemyTypeEntry {
        [Tooltip("Matches EnemySpawnPointData.enemyType. Leave any spawn point's enemyType empty to allow any of these to be picked at random for it.")]
        public string typeId;
        public NetworkObject prefab;
    }

    [SerializeField] private List<EnemyTypeEntry> enemyTypes = new();
    [SerializeField] private SceneEnemySpawnData spawnData;
    [SerializeField] private ScenePatrolData patrolData;

    [Header("Spawn Rules")]
    [SerializeField] private int maxActiveEnemies = 1;
    [SerializeField] private float minSpawnPointSeparation = 5f;

    [Header("Schedule — Night Window")]
    [Tooltip("Hour enemies spawn, e.g. 22 = 10 PM. Enemies spawn once when the clock enters the window (> this hour).")]
    [SerializeField] private int spawnHour = 22;
    [Tooltip("Minute threshold. Enemies spawn once when clock is past this minute on the spawn hour, e.g. 30 = spawns after 10:30 PM.")]
    [SerializeField] private int spawnMinute = 30;
    [Tooltip("Hour enemies despawn, e.g. 6 = 6 AM. Enemies despawn when clock exits the window (< this hour).")]
    [SerializeField] private int despawnHour = 6;
    [Tooltip("Minute threshold. Enemies despawn once when clock is before this minute on the despawn hour, e.g. 30 = despawns before 6:30 AM.")]
    [SerializeField] private int despawnMinute = 30;

    private readonly List<NetworkObject> activeEnemies = new List<NetworkObject>();
    private EnemySpawnPointData lastSpawnPoint;
    private bool spawnedTonight;

    // Tracks the previous clock state to detect window crossings without
    // needing TimeManager to tell us about admin skips.
    private int prevHour = -1;
    private int prevMinute = -1;

    private TimeManager subscribedTimeManager;

    public override void OnNetworkSpawn() {
        if (!IsServer) { enabled = false; return; }

        string activeScene = SceneManager.GetActiveScene().name;
        if (spawnData != null && spawnData.sceneName != activeScene)
            Debug.LogWarning($"EnemySpawnManager: spawnData.sceneName '{spawnData.sceneName}' does not match active scene '{activeScene}'.");

        if (TimeManager.Instance != null) {
            // TimeManager already spawned before us this scene load — hook up now.
            HookTimeManager(TimeManager.Instance);
        } else {
            // Scene-placed NetworkObjects don't guarantee spawn order, so
            // TimeManager might not exist yet. Wait for it to announce itself.
            TimeManager.OnAnyTimeManagerReady += HookTimeManager;
        }
    }

    public override void OnNetworkDespawn() {
        TimeManager.OnAnyTimeManagerReady -= HookTimeManager;
        if (subscribedTimeManager != null)
            subscribedTimeManager.OnTimeUpdated -= OnTimeUpdated;
    }

    private void HookTimeManager(TimeManager tm) {
        TimeManager.OnAnyTimeManagerReady -= HookTimeManager;
        subscribedTimeManager = tm;
        tm.OnTimeUpdated += OnTimeUpdated;

        // Initialize prev state to the current time so we don't get a
        // false window-crossing on the very first EvaluateTime call.
        prevHour = tm.Hours;
        prevMinute = tm.Minutes;
    }

    private void OnTimeUpdated() {
        if (subscribedTimeManager == null) return;
        EvaluateTime(subscribedTimeManager.Hours, subscribedTimeManager.Minutes);
    }

    private void EvaluateTime(int hour, int minute) {
        bool wasInWindow = IsInNightWindow(prevHour, prevMinute);
        bool isInWindow = IsInNightWindow(hour, minute);

        Debug.Log($"EnemySpawnManager: EvaluateTime {hour:00}:{minute:00} (wasInWindow={wasInWindow}, isInWindow={isInWindow}, spawnedTonight={spawnedTonight})");
        // ---- Entered the night window ----
        if (isInWindow && !wasInWindow && !spawnedTonight) {
            SpawnWave();
            spawnedTonight = true;
        }

        // ---- Exited the night window ----
        if (wasInWindow && !isInWindow) {
            DespawnAllEnemies();
            spawnedTonight = false;
        }

        prevHour = hour;
        prevMinute = minute;
    }

    // True when the clock is inside the night window: >=spawnHour:spawnMinute
    // up to but not including despawnHour:despawnMinute, wrapping past midnight.
    // E.g. with spawn=22:30, despawn=6:30: true at 22:30, false at 06:30+.
    private bool IsInNightWindow(int hour, int minute) {
        int t = hour * 60 + minute;
        int spawnMark = spawnHour * 60 + spawnMinute;       // 22:30 = 1350
        int despawnMark = despawnHour * 60 + despawnMinute; // 06:30 = 390

        Debug.Log($"{hour} {minute} {t}");
        // The window always wraps midnight in this design (22:30 → 6:30).
        // t >= spawnMark: inside the window on the "late" side of midnight (>=22:30)
        // t < despawnMark: before the despawn mark on the "early" side (<6:30)
        return t >= spawnMark || t < despawnMark;
    }

    private void DespawnAllEnemies() {
        foreach (var enemy in activeEnemies) {
            if (enemy == null) continue;
            enemy.Despawn(true); // true = destroy the instance too
        }
        activeEnemies.Clear();
    }

    // Fills up to maxActiveEnemies, each at a distinct spawn point for
    // this wave (so a single night doesn't stack two enemies on top of
    // each other), each with its own resolved type + patrol route.
    private void SpawnWave() {
        activeEnemies.RemoveAll(e => e == null);
        int toSpawn = maxActiveEnemies - activeEnemies.Count;
        if (toSpawn <= 0) return;

        var usedThisWave = new HashSet<EnemySpawnPointData>();
        for (int i = 0; i < toSpawn; i++) {
            if (!TrySpawnOne(usedThisWave)) break; // ran out of valid spawn points
        }
    }

    // Call this to spawn one additional enemy on demand (e.g. after a
    // previous enemy dies mid-night and you want to backfill it).
    public void SpawnEnemy() {
        if (!IsServer) return;

        activeEnemies.RemoveAll(e => e == null);
        if (activeEnemies.Count >= maxActiveEnemies) return;

        TrySpawnOne(new HashSet<EnemySpawnPointData>());
    }

    private bool TrySpawnOne(HashSet<EnemySpawnPointData> usedThisWave) {
        if (enemyTypes.Count == 0 || spawnData == null || spawnData.spawnPoints.Count == 0) return false;

        EnemySpawnPointData point = PickSpawnPoint(usedThisWave);
        if (point == null) return false;

        NetworkObject prefab = ResolvePrefab(point.enemyType);
        if (prefab == null) return false;

        usedThisWave.Add(point);

        var instance = Instantiate(prefab, point.position, point.rotation);
        instance.Spawn();

        var controller = instance.GetComponent<EnemyController>();
        if (controller != null)
            controller.SetPatrolPoints(GetPatrolPointsFor(point.groupId));

        activeEnemies.Add(instance);
        lastSpawnPoint = point;
        return true;
    }

    private NetworkObject ResolvePrefab(string typeId) {
        if (!string.IsNullOrEmpty(typeId)) {
            var match = enemyTypes.FirstOrDefault(t => t.typeId == typeId);
            if (match != null && match.prefab != null) return match.prefab;
            Debug.LogWarning($"EnemySpawnManager: no enemyType entry matches '{typeId}' — picking a random type for this spawn instead.");
        }
        return enemyTypes[Random.Range(0, enemyTypes.Count)].prefab;
    }

    private EnemySpawnPointData PickSpawnPoint(HashSet<EnemySpawnPointData> usedThisWave) {
        var available = spawnData.spawnPoints.Where(p => !usedThisWave.Contains(p)).ToList();
        if (available.Count == 0) return null;
        if (available.Count == 1) return available[0];

        // Try to avoid repeating the same point (or one too close to it) back-to-back.
        for (int attempt = 0; attempt < 10; attempt++) {
            EnemySpawnPointData candidate = available[Random.Range(0, available.Count)];
            if (lastSpawnPoint == null) return candidate;

            float dist = Vector3.Distance(candidate.position, lastSpawnPoint.position);
            if (dist >= minSpawnPointSeparation) return candidate;
        }

        // fallback: just take any random available point
        return available[Random.Range(0, available.Count)];
    }

    private List<Vector3> GetPatrolPointsFor(string groupId) {
        if (string.IsNullOrEmpty(groupId) || patrolData == null)
            return new List<Vector3>();

        PatrolGroupData group = patrolData.patrolGroups.FirstOrDefault(g => g.groupId == groupId);
        if (group == null) return new List<Vector3>();

        return group.points.Select(p => p.position).ToList();
    }

    // Call when an enemy dies/despawns if you want the manager to
    // track counts and potentially backfill via SpawnEnemy().
    public void NotifyEnemyRemoved(NetworkObject enemy) {
        activeEnemies.Remove(enemy);
    }
}