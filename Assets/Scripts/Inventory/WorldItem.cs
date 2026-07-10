using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// A physical item lying in the world that players can pick up directly
// (no quiz gate — for a quiz-gated pickup, use PickupInteractable instead).
//
// Spawn workflow (server-side):
//   var go = Instantiate(worldItemPrefab, position, rotation);
//   go.GetComponent<NetworkObject>().Spawn();
//   go.GetComponent<WorldItem>().Setup("key_red_door", 1);
[RequireComponent(typeof(NetworkObject))]
public class WorldItem : NetworkBehaviour, IInteractable {
    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    [SerializeField] private MeshRenderer meshRenderer; // swap for MeshRenderer in 3D
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    private NetworkVariable<FixedString64Bytes> _itemID = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _quantity = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Setup (called server-side after Spawn) ────────────────────────────────

    public void Setup(string itemID, int quantity) {
        if (!IsServer) return;
        _itemID.Value = itemID;
        _quantity.Value = quantity;
        RefreshVisualClientRpc(itemID);
    }

    public override void OnNetworkSpawn() {
        // Late-joining clients: sync icon from current NetworkVariable value
        if (!_itemID.Value.IsEmpty)
            RefreshVisualClientRpc(_itemID.Value.ToString());
    }

    [ClientRpc]
    private void RefreshVisualClientRpc(string itemID) {
        var item = Registry.Get(itemID);
        if (item != null && meshRenderer != null) Debug.Log($"WorldItem: would set meshRenderer for {itemID} to {item.displayName}");
        //meshRenderer.sprite = item.icon;
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractPrompt {
        get {
            var item = Registry.Get(_itemID.Value.ToString());
            string name = item != null ? item.displayName : "Item";
            return $"Press E to pick up {name}";
        }
    }

    // A world pickup is never "locked" in the requirement sense — it's either
    // there to grab or it's already been despawned.
    public bool IsLocked => false;

    public void OnFocus(PlayerInteractionUI ui) => ui.Show(InteractPrompt);

    public void OnInteract(GameObject interactor) {
        // Called locally on the interacting client → fire ServerRpc
        RequestPickupServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPickupServerRpc(RpcParams rpcParams = default) {
        if (!IsSpawned) return; // Already picked up by someone else

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return;

        var inventory = client.PlayerObject?.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        bool added = inventory.AddItem(_itemID.Value.ToString(), _quantity.Value);
        if (added)
            NetworkObject.Despawn(destroy: true);
        // If inventory is full, do nothing (item stays in world)
    }
}