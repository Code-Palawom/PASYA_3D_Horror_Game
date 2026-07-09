using System.Collections.Generic;
using UnityEngine;

// ScriptableObject that holds every InventoryItem in the game.
// Call Initialize() once at boot (see GameBootstrap).
[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Inventory/Item Registry")]
public class ItemRegistry : ScriptableObject {
    public static ItemRegistry Instance { get; private set; }

    [SerializeField] private List<InventoryItem> items = new();

    private Dictionary<string, InventoryItem> _lookup;

    // Build the lookup dictionary. Call once at game start.
    public void Initialize() {
        Instance = this;
        _lookup = new Dictionary<string, InventoryItem>();
        foreach (var item in items) {
            if (!string.IsNullOrEmpty(item.itemID))
                _lookup[item.itemID] = item;
            else
                Debug.LogWarning($"[ItemRegistry] Item '{item.name}' has no itemID — skipped.");
        }
        Debug.Log($"[ItemRegistry] Loaded {_lookup.Count} items.");
    }

    // Returns null if not found.
    public InventoryItem Get(string itemID) {
        if (string.IsNullOrEmpty(itemID)) return null;
        _lookup.TryGetValue(itemID, out var result);
        return result;
    }
}