using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// One-time, server-only spawner. At each assigned spawn point, spawns a
// WorldItem carrying a randomly chosen item from itemPool.
//
// This GameObject itself is NOT a NetworkObject — it just calls Spawn() on
// networked WorldItem instances, which then sync to every client on their own.
// Not a respawner: once placed, items are picked up and gone for good.
public class WorldItemSpawner : MonoBehaviour {
    [Header("Spawn Points")]
    [Tooltip("Empty Transform GameObjects placed in the scene — one item spawns at each.")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Item Pool")]
    [Tooltip("Each item in this pool spawns exactly once, at a randomly assigned point. " +
             "No duplicates — if there are more points than items, extra points are skipped; " +
             "if there are more items than points, extra items are left unplaced.")]
    [SerializeField] private List<InventoryItem> itemPool = new();

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
        if (itemPool.Count == 0) {
            Debug.LogError("[WorldItemSpawner] Item pool is empty.");
            return;
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

            var item = shuffledItems[i];

            var go = Instantiate(worldItemPrefab, point.position, point.rotation);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null) {
                Debug.LogError($"[WorldItemSpawner] '{worldItemPrefab.name}' has no NetworkObject.");
                Destroy(go);
                continue;
            }

            netObj.Spawn();
            go.GetComponent<WorldItem>().Setup(item.itemID, 1);
            spawned++;
        }

        Debug.Log($"[WorldItemSpawner] Spawned {spawned} unique items.");
    }

    // Fisher-Yates shuffle
    private static void Shuffle(List<InventoryItem> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}