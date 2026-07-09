//using Unity.Collections;
//using Unity.Netcode;
//using UnityEditor.PackageManager;
//using UnityEngine;

//// A physical item lying in the world that players can pick up.

//// Spawn workflow (server-side):
////   var go = Instantiate(worldItemPrefab, position, rotation);
////   go.GetComponent<NetworkObject>().Spawn();
////   go.GetComponent<WorldItem>().Setup("key_red_door", 1);
//[RequireComponent(typeof(NetworkObject))]
//public class WorldItem : NetworkBehaviour, IInteractable {
//    [SerializeField] private ItemRegistry itemRegistry;
//    [SerializeField] private SpriteRenderer spriteRenderer; // swap for MeshRenderer in 3D

//    private NetworkVariable<FixedString64Bytes> _itemID = new(
//        default,
//        NetworkVariableReadPermission.Everyone,
//        NetworkVariableWritePermission.Server
//    );

//    private NetworkVariable<int> _quantity = new(
//        1,
//        NetworkVariableReadPermission.Everyone,
//        NetworkVariableWritePermission.Server
//    );

//    // ── Setup (called server-side after Spawn) ────────────────────────────────

//    public void Setup(string itemID, int quantity) {
//        if (!IsServer) return;
//        _itemID.Value = itemID;
//        _quantity.Value = quantity;
//        RefreshVisualClientRpc(itemID);
//    }

//    public override void OnNetworkSpawn() {
//        // Late-joining clients: sync icon from current NetworkVariable value
//        if (!_itemID.Value.IsEmpty)
//            RefreshVisualClientRpc(_itemID.Value.ToString());
//    }

//    [ClientRpc]
//    private void RefreshVisualClientRpc(string itemID) {
//        var item = itemRegistry.Get(itemID);
//        if (item != null && spriteRenderer != null)
//            spriteRenderer.sprite = item.icon;
//    }

//    // ── IInteractable ─────────────────────────────────────────────────────────

//    public void Interact(ulong interactingClientId) {
//        // Called locally on the interacting client → fire ServerRpc
//        RequestPickupServerRpc();
//    }

//    [ServerRpc(RequireOwnership = false)]
//    private void RequestPickupServerRpc(ServerRpcParams rpcParams = default) {
//        if (!IsSpawned) return; // Already picked up by someone else

//        ulong clientId = rpcParams.Receive.SenderClientId;

//        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
//            return;

//        var inventory = client.PlayerObject?.GetComponent<PlayerInventory>();
//        if (inventory == null) return;

//        bool added = inventory.AddItem(_itemID.Value.ToString(), _quantity.Value);
//        if (added)
//            NetworkObject.Despawn(destroy: true);
//        // If inventory is full, do nothing (item stays in world)
//    }
//}