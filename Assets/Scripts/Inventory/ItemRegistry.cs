using System.Collections.Generic;
using UnityEngine;

// ScriptableObject that holds every InventoryItem for the CURRENT scene.
// Call Initialize() once at scene boot (see GameBootstrap) — but Get() also
// self-heals if something calls it before Initialize() ran, so ordering
// mistakes fail safe instead of silently returning null forever.
[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Inventory/Item Registry")]
public class ItemRegistry : ScriptableObject {
    // Whichever ItemRegistry last called Initialize() — i.e. the current
    // scene's registry. Runtime components that need to work across scenes
    // off a single shared prefab (like PlayerInventory) resolve through this
    // instead of a hardcoded serialized reference.
    public static ItemRegistry Instance { get; private set; }

    [SerializeField] private List<InventoryItem> items = new();

    private Dictionary<string, InventoryItem> _lookup;

    // Build the lookup dictionary. Call once per scene load, before anything
    // spawns that might need item lookups (see GameBootstrap).
    public void Initialize() {
        Instance = this;
        _lookup = new Dictionary<string, InventoryItem>();
        foreach (var item in items) {
            if (!string.IsNullOrEmpty(item.itemID))
                _lookup[item.itemID] = item;
            else
                Debug.LogWarning($"[ItemRegistry] Item '{item.name}' has no itemID — skipped.");
        }
        Debug.Log($"[ItemRegistry] '{name}' loaded {_lookup.Count} items.");
    }

    // Returns null if not found. Self-heals if Initialize() hasn't run yet
    // (e.g. a script execution order slip) rather than staying broken.
    public InventoryItem Get(string itemID) {
        Debug.Log($"ItemRegistry.Get('{itemID}') called.");
        if (string.IsNullOrEmpty(itemID)) return null;
        if (_lookup == null) {
            Debug.LogWarning($"[ItemRegistry] '{name}' queried before Initialize() ran — " +
                              "initializing now. Check your GameBootstrap execution order.");
            Initialize();
        }
        _lookup.TryGetValue(itemID, out var result);
        return result;
    }
}