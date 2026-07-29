using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// One-time, server-only spawner. At each assigned spawn point, spawns a
// WorldItem carrying a randomly chosen item from itemPool. Also supports
// a list of fixed spawns — specific items tied to specific points, always
// placed, never shuffled.
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

    [Header("Random Spawn Points")]
    [Tooltip("Empty Transform GameObjects placed in the scene — one item spawns at each, " +
             "randomly chosen from itemPool.")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Item Pool")]
    [Tooltip("Each item in this pool spawns exactly once, at a randomly assigned point. " +
             "No duplicates — if there are more points than items, extra points are skipped; " +
             "if there are more items than points, extra items are left unplaced.")]
    [SerializeField] private List<InventoryItem> itemPool = new();

    [Header("Fixed Spawns")]
    [Tooltip("Items that always spawn at their assigned point — not part of the random shuffle.")]
    [SerializeField] private List<FixedSpawn> fixedSpawns = new();

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
        spawned += SpawnRandomPool();

        Debug.Log($"[WorldItemSpawner] Spawned {spawned} items total.");
    }

    private int SpawnFixed() {
        int spawned = 0;
        foreach (var fixedSpawn in fixedSpawns) {
            if (fixedSpawn.point == null) {
                Debug.LogWarning("[WorldItemSpawner] Fixed spawn has no point assigned — skipping.");
                continue;
            }
            if (fixedSpawn.item == null) {
                Debug.LogWarning($"[WorldItemSpawner] Fixed spawn at '{fixedSpawn.point.name}' has no item assigned — skipping.");
                continue;
            }

            if (SpawnItemAt(fixedSpawn.point, fixedSpawn.item)) spawned++;
        }
        return spawned;
    }

    private int SpawnRandomPool() {
        if (itemPool.Count == 0) {
            Debug.LogWarning("[WorldItemSpawner] Item pool is empty — skipping random spawns.");
            return 0;
        }

        if (spawnPoints.Count != itemPool.Count) {
            Debug.LogWarning($"[WorldItemSpawner] {spawnPoints.Count} spawn points but " +
                              $"{itemPool.Count} items in pool — " +
                              (spawnPoints.Count > itemPool.Count
                                  ? "some points will be left empty."
                                  : "some items won't be placed."));
        }

        var shuffledItems = new List<InventoryItem>(itemPool);
        Shuffle(shuffledItems);

        int spawned = 0;
        int count = Mathf.Min(spawnPoints.Count, shuffledItems.Count);
        for (int i = 0; i < count; i++) {
            var point = spawnPoints[i];
            if (point == null) continue;

            if (SpawnItemAt(point, shuffledItems[i])) spawned++;
        }
        return spawned;
    }

    // Shared spawn path used by both fixed and random spawns.
    private bool SpawnItemAt(Transform point, InventoryItem item) {
        var go = Instantiate(worldItemPrefab, point.position, point.rotation);
        if (!go.TryGetComponent<NetworkObject>(out var netObj)) {
            Debug.LogError($"[WorldItemSpawner] '{worldItemPrefab.name}' has no NetworkObject.");
            Destroy(go);
            return false;
        }

        // Must happen BEFORE Spawn() — NetworkedQuizGate claims its question
        // inside OnNetworkSpawn using whatever difficulty is set at that moment.
        var gate = go.GetComponent<NetworkedQuizGate>();
        gate.SetDifficulty(item.pickupDifficulty);

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
}