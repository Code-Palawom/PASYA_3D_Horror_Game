using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// A physical item lying in the world. Requires a correct quiz answer
// (via NetworkedQuizGate) before the item is actually added to inventory —
// same flow as your doors, just gating a pickup instead of an open/unlock.
//
// Spawn workflow (server-side):
//   var go = Instantiate(worldItemPrefab, position, rotation);
//   go.GetComponent<NetworkObject>().Spawn();
//   go.GetComponent<WorldItem>().Setup("key_red_door", 1);
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkedQuizGate))]
public class WorldItem : NetworkBehaviour, IInteractable {
    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    [Tooltip("Empty child transform where the item's 3D worldModelPrefab gets instantiated.")]
    [SerializeField] private Transform modelAnchor;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    private GameObject _currentModelInstance;
    private NetworkedQuizGate _gate;
    private InteractionRequirements _requirements; // optional — e.g. "need a lockpick to even attempt this"

    void Awake() {
        _gate = GetComponent<NetworkedQuizGate>();
        _requirements = GetComponent<InteractionRequirements>();
    }

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
        if (Registry == null) {
            Debug.LogError("[WorldItem] No ItemRegistry available — ItemRegistry.Instance is null " +
                            "and no explicit itemRegistry was assigned. Check that GameBootstrap is " +
                            "present in this scene and ran before this WorldItem spawned.");
            return;
        }

        var item = Registry.Get(itemID);
        if (item == null) return;

        // Clear any previous model (e.g. re-Setup on a pooled/reused WorldItem)
        if (_currentModelInstance != null) {
            Destroy(_currentModelInstance);
            _currentModelInstance = null;
        }

        if (item.worldModelPrefab == null) {
            Debug.LogWarning($"[WorldItem] '{item.itemID}' has no worldModelPrefab assigned.");
            return;
        }

        if (item.worldModelPrefab.GetComponent<NetworkObject>() != null) {
            Debug.LogError($"[WorldItem] '{item.itemID}'.worldModelPrefab is itself a NetworkObject " +
                            "(likely the WorldItem prefab assigned by mistake). worldModelPrefab must " +
                            "be a plain visual-only prefab — mesh/renderer only, no NetworkObject or " +
                            "scripts. Fix the assignment on the InventoryItem asset.");
            return;
        }

        Transform parent = modelAnchor != null ? modelAnchor : transform;
        _currentModelInstance = Instantiate(item.worldModelPrefab, parent);
        _currentModelInstance.transform.localPosition = Vector3.zero;
        _currentModelInstance.transform.localRotation = Quaternion.identity;
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractPrompt {
        get {
            if (_gate.IsCooldownActive) return "Locked";
            if (_gate.HasInteractingPlayer && !_gate.AllowOthers) return "Someone is answering...";

            if (_requirements != null && _requirements.HasRequirements) {
                var local = NetworkManager?.LocalClient?.PlayerObject?.gameObject;
                if (local != null && !_requirements.CheckAll(local, out string failMsg))
                    return failMsg;
            }

            var item = Registry?.Get(_itemID.Value.ToString());
            string name = item != null ? item.displayName : "Item";
            return $"Pick up {name}";
        }
    }

    public bool IsLocked => !_gate.IsUnlocked;

    public void OnFocus(PlayerInteractionUI ui) {
        if (_gate.IsCooldownActive)
            ui.ShowWithCooldown(_gate.CooldownRemaining, _gate.WrongAnswerCooldown);
        else
            ui.Show(InteractPrompt);
    }

    public void OnInteract(GameObject interactor) {
        // Guards against a race where this item was already picked up/despawned
        // (by another player, or a duplicate input event) between the raycast
        // that found it and this interaction actually firing. Without this,
        // _gate.Attempt below tries to send an RPC on a despawned NetworkObject,
        // which throws inside Netcode's internal RPC machinery.
        if (!IsSpawned) return;

        if (_requirements != null && _requirements.HasRequirements
            && !_requirements.CheckAll(interactor, out string failMsg)) {
            PlayerInteractionUI.ShowMessageForPlayer(interactor, failMsg);
            return;
        }

        _gate.Attempt(
            interactor,
            onSuccess: () => {
                _requirements?.NotifyConsumed(interactor);
                RequestPickupServerRpc();
            },
            onFail: () => { }
        );
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