using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// One-time, server-only spawner. At each assigned spawn point, spawns a
// WorldItem carrying a randomly chosen item from itemPool. Also supports
// a list of fixed spawns — specific items tied to specific points, always
// placed, never shuffled — and spawn groups, where a subset of points draws
// only from its own item pool instead of the global one.
//
// This GameObject itself is NOT a NetworkObject — it just calls Spawn() on
// networked WorldItem instances, which then sync to every client on their own.
// Not a respawner: once placed, items are picked up and gone for good.
public class WorldItemSpawner : MonoBehaviour {
    [Serializable]
    private class FixedSpawn {
        [Tooltip("Where this specific item spawns.")]
        public Transform point;
        [Tooltip("The item that always spawns at this point.")]
        public InventoryItem item;
    }

    [Serializable]
    private class SpawnGroup {
        [Tooltip("Optional label for logging/debugging.")]
        public string groupName;
        [Tooltip("Points that only draw from this group's own item list.")]
        public List<Transform> points = new();
        [Tooltip("Items available to this group's points. Shuffled with no repeats, " +
                 "same rules as the main pool: extra points are skipped, extra items are left unplaced.")]
        public List<InventoryItem> items = new();
        [Tooltip("How many of this group's placed items spawn with their quiz gate skipped " +
                 "(SetSkipQuiz(true) before Spawn). Chosen randomly among the placed items. " +
                 "Clamped to the number actually placed.")]
        public int skipQuizCount = 0;
    }

    [Header("Random Spawn Points")]
    [Tooltip("Empty Transform GameObjects placed in the scene — one item spawns at each, " +
             "randomly chosen from itemPool.")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Item Pool")]
    [Tooltip("Each item in this pool spawns exactly once, at a randomly assigned point. " +
             "No duplicates — if there are more points than items, extra points are skipped; " +
             "if there are more items than points, extra items are left unplaced.")]
    [SerializeField] private List<InventoryItem> itemPool = new();
    [Tooltip("How many of the placed main-pool items spawn with their quiz gate skipped. " +
             "Chosen randomly among the placed items. Clamped to the number actually placed.")]
    [SerializeField] private int skipQuizCount = 0;

    [Header("Fixed Spawns")]
    [Tooltip("Items that always spawn at their assigned point — not part of the random shuffle.")]
    [SerializeField] private List<FixedSpawn> fixedSpawns = new();
    [Tooltip("How many fixed spawns spawn with their quiz gate skipped. Chosen randomly " +
             "among the valid fixed spawns (point+item both assigned). Clamped to that count.")]
    [SerializeField] private int fixedSpawnSkipQuizCount = 0;

    [Header("Spawn Groups")]
    [Tooltip("Each group's points only draw from that group's own items — isolated from " +
             "the main pool and from other groups.")]
    [SerializeField] private List<SpawnGroup> spawnGroups = new();

    [Header("Prefab")]
    [Tooltip("Must have NetworkObject + WorldItem components.")]
    [SerializeField] private GameObject worldItemPrefab;

    private void Start() {
        // Only the server spawns — Netcode syncs the result to every client.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        SpawnAll();
    }

    private void SpawnAll() {
        if (worldItemPrefab == null) {
            Debug.LogError("[WorldItemSpawner] No worldItemPrefab assigned.");
            return;
        }

        int spawned = 0;
        spawned += SpawnFixed();
        spawned += SpawnRandomPool(spawnPoints, itemPool, "main pool", skipQuizCount);
        spawned += SpawnGroups();

        Debug.Log($"[WorldItemSpawner] Spawned {spawned} items total.");
    }

    private int SpawnFixed() {
        // Filter to valid entries first so the skip count is picked among
        // what will actually be placed, not the raw (possibly broken) list.
        var valid = new List<FixedSpawn>();
        foreach (var fixedSpawn in fixedSpawns) {
            if (fixedSpawn.point == null) {
                Debug.LogWarning("[WorldItemSpawner] Fixed spawn has no point assigned — skipping.");
                continue;
            }
            if (fixedSpawn.item == null) {
                Debug.LogWarning($"[WorldItemSpawner] Fixed spawn at '{fixedSpawn.point.name}' has no item assigned — skipping.");
                continue;
            }
            valid.Add(fixedSpawn);
        }

        var skipIndices = PickRandomIndices(valid.Count, fixedSpawnSkipQuizCount);

        int spawned = 0;
        for (int i = 0; i < valid.Count; i++) {
            if (SpawnItemAt(valid[i].point, valid[i].item, skipIndices.Contains(i))) spawned++;
        }
        return spawned;
    }

    private int SpawnGroups() {
        int spawned = 0;
        foreach (var group in spawnGroups) {
            string label = string.IsNullOrEmpty(group.groupName) ? "unnamed group" : group.groupName;
            spawned += SpawnRandomPool(group.points, group.items, label, group.skipQuizCount);
        }
        return spawned;
    }

    // Shuffles `items` (no repeats) across `points`, one item per point, up to
    // whichever list is shorter. `skipQuizCount` items among those actually
    // placed are randomly chosen to spawn with skipQuiz = true.
    private int SpawnRandomPool(List<Transform> points, List<InventoryItem> items, string label, int skipQuizCount) {
        if (items.Count == 0) {
            Debug.LogWarning($"[WorldItemSpawner] Item pool for '{label}' is empty — skipping random spawns.");
            return 0;
        }

        if (points.Count != items.Count) {
            Debug.LogWarning($"[WorldItemSpawner] '{label}': {points.Count} spawn points but " +
                              $"{items.Count} items — " +
                              (points.Count > items.Count
                                  ? "some points will be left empty."
                                  : "some items won't be placed."));
        }

        var shuffledItems = new List<InventoryItem>(items);
        Shuffle(shuffledItems);

        int count = Mathf.Min(points.Count, shuffledItems.Count);
        var skipIndices = PickRandomIndices(count, skipQuizCount);

        int spawned = 0;
        for (int i = 0; i < count; i++) {
            var point = points[i];
            if (point == null) continue;

            if (SpawnItemAt(point, shuffledItems[i], skipIndices.Contains(i))) spawned++;
        }
        return spawned;
    }

    // Shared spawn path used by fixed spawns, the main pool, and every spawn group.
    private bool SpawnItemAt(Transform point, InventoryItem item, bool skipQuiz) {
        var go = Instantiate(worldItemPrefab, point.position, point.rotation);
        if (!go.TryGetComponent<NetworkObject>(out var netObj)) {
            Debug.LogError($"[WorldItemSpawner] '{worldItemPrefab.name}' has no NetworkObject.");
            Destroy(go);
            return false;
        }

        // Must happen BEFORE Spawn() — NetworkedQuizGate claims its question
        // (or skips claiming one) inside OnNetworkSpawn using whatever state
        // is set at that moment.
        var gate = go.GetComponent<NetworkedQuizGate>();
        gate.SetDifficulty(item.pickupDifficulty);
        gate.SetSkipQuiz(skipQuiz);

        // Gate id defaults to the item's own itemID, but InventoryItem.pickupGateId
        // can override it — needed if the same item is placed by more than one
        // spawner/point and each placement should count as a separate task
        // (see the SpecificGate task-matching caveat: same gate id = same task).
        string gateId = !string.IsNullOrEmpty(item.pickupGateId) ? item.pickupGateId : item.itemID;
        gate.SetGateId(gateId);
        gate.SetTags(item.pickupTags);

        netObj.Spawn();
        go.GetComponent<WorldItem>().Setup(item.itemID, 1);
        return true;
    }

    // Fisher-Yates shuffle
    private static void Shuffle(List<InventoryItem> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Returns `count` distinct indices in [0, total), chosen uniformly at
    // random, clamped to `total` if count exceeds it.
    private static HashSet<int> PickRandomIndices(int total, int count) {
        count = Mathf.Clamp(count, 0, total);
        var indices = new List<int>(total);
        for (int i = 0; i < total; i++) indices.Add(i);

        for (int i = 0; i < count; i++) {
            int j = UnityEngine.Random.Range(i, indices.Count);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return new HashSet<int>(indices.GetRange(0, count));
    }
}