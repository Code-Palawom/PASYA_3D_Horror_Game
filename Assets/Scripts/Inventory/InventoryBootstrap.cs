using UnityEngine;

// Initializes global item lookup before anything tries to use it.
//
// IMPORTANT — execution order:
// This MUST run before any PlayerInventory.OnNetworkSpawn, WorldItem.Setup,
// or KeyRequirement/ItemRequirement check does an ItemRegistry.Get() lookup.
//
// Setup:
//   1. Put this on a persistent GameObject in your bootstrap/first-loaded
//      scene (e.g. wherever your NetworkManager lives).
//   2. Assign your ItemRegistry asset in the Inspector.
//   3. In Project Settings > Script Execution Order, put GameBootstrap
//      BEFORE Default Time, or simply ensure its GameObject is higher in
//      the hierarchy / loads in an earlier-loaded scene than any player
//      or networked object that reads from ItemRegistry.
public class InventoryBootstrap : MonoBehaviour {
    [SerializeField] private ItemRegistry itemRegistry;

    void Awake() {
        if (itemRegistry == null) {
            Debug.LogError("[GameBootstrap] No ItemRegistry assigned — item lookups will fail.");
            return;
        }

        itemRegistry.Initialize();
    }
}