using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Server-side. Spawns enemy NetworkObjects at points defined in a
// SceneEnemySpawnData asset, using a matching ScenePatrolData asset
// (paired by groupId) to assign each enemy's patrol route. Attach to
// an empty GameObject with a NetworkObject, place in your scene, and
// assign enemyPrefab + the two data assets for that scene.
public class EnemySpawnManager : NetworkBehaviour {
    [SerializeField] private NetworkObject enemyPrefab;
    [SerializeField] private SceneEnemySpawnData spawnData;
    [SerializeField] private ScenePatrolData patrolData;

    [Header("Spawn Rules")]
    [SerializeField] private int maxActiveEnemies = 1;
    [SerializeField] private float minSpawnPointSeparation = 5f; // avoid spawning right on top of a player-visible point twice in a row

    [Header("Time-of-day schedule")]
    [Tooltip("Hour (0-23) enemies start spawning, e.g. 22 for 10 PM.")]
    [SerializeField] private int nightStartHour = 22;
    [Tooltip("Hour (0-23) enemies despawn, e.g. 6 for 6 AM. Can be earlier than start (wraps past midnight).")]
    [SerializeField] private int nightEndHour = 6;

    private readonly List<NetworkObject> activeEnemies = new List<NetworkObject>();
    private EnemySpawnPointData lastSpawnPoint;
    private bool isNight;

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

        // catch up in case we hook mid-scene-load during an active night
        EvaluateHour(tm.Hours, forceEvaluate: true);
    }

    private void OnTimeUpdated() {
        if (subscribedTimeManager == null) return;
        EvaluateHour(subscribedTimeManager.Hours, forceEvaluate: false);
    }

    private void EvaluateHour(int hour, bool forceEvaluate) {
        bool shouldBeNight = nightStartHour <= nightEndHour
            ? hour >= nightStartHour && hour < nightEndHour          // e.g. 8 -> 18, same-day window
            : hour >= nightStartHour || hour < nightEndHour;         // e.g. 22 -> 6, wraps past midnight

        if (shouldBeNight == isNight && !forceEvaluate) return;

        isNight = shouldBeNight;

        if (isNight)
            SpawnEnemy();
        else
            DespawnAllEnemies();
    }

    private void DespawnAllEnemies() {
        foreach (var enemy in activeEnemies) {
            if (enemy == null) continue;
            enemy.Despawn(true); // true = destroy the instance too
        }
        activeEnemies.Clear();
    }

    // Call this to spawn one enemy (e.g. from a game-start event,
    // a director/wave system, or after the previous enemy dies).
    public void SpawnEnemy() {
        if (!IsServer) return;
        if (enemyPrefab == null || spawnData == null || spawnData.spawnPoints.Count == 0) return;

        activeEnemies.RemoveAll(e => e == null);
        if (activeEnemies.Count >= maxActiveEnemies) return;

        EnemySpawnPointData point = PickSpawnPoint();
        var instance = Instantiate(enemyPrefab, point.position, point.rotation);
        instance.Spawn();

        var controller = instance.GetComponent<EnemyController>();
        if (controller != null)
            controller.SetPatrolPoints(GetPatrolPointsFor(point.groupId));

        activeEnemies.Add(instance);
        lastSpawnPoint = point;
    }

    private EnemySpawnPointData PickSpawnPoint() {
        var points = spawnData.spawnPoints;
        if (points.Count == 1) return points[0];

        // Try to avoid repeating the same point (or one too close to it) back-to-back.
        for (int attempt = 0; attempt < 10; attempt++) {
            EnemySpawnPointData candidate = points[Random.Range(0, points.Count)];
            if (lastSpawnPoint == null) return candidate;

            float dist = Vector3.Distance(candidate.position, lastSpawnPoint.position);
            if (dist >= minSpawnPointSeparation) return candidate;
        }

        // fallback: just take any random point
        return points[Random.Range(0, points.Count)];
    }

    private List<Vector3> GetPatrolPointsFor(string groupId) {
        if (string.IsNullOrEmpty(groupId) || patrolData == null)
            return new List<Vector3>();

        PatrolGroupData group = patrolData.patrolGroups.FirstOrDefault(g => g.groupId == groupId);
        if (group == null) return new List<Vector3>();

        return group.points.Select(p => p.position).ToList();
    }

    // Call when an enemy dies/despawns if you want the manager to
    // track counts and potentially respawn.
    public void NotifyEnemyRemoved(NetworkObject enemy) {
        activeEnemies.Remove(enemy);
    }
}