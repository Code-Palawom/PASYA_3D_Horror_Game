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
    [Tooltip("Physics collider on this WorldItem root. Auto-swapped each time the visual refreshes " +
             "to match whatever Collider type (Box/Sphere/Capsule) is found on the item's " +
             "worldModelPrefab, with that collider's shape values copied over — so a key's small " +
             "SphereCollider and a crate's big BoxCollider each end up as the actual physics shape, " +
             "instead of one fixed size for every item. If the model has no Collider at all, falls " +
             "back to a BoxCollider sized from InventoryItem.worldColliderSize/worldColliderCenter. " +
             "Assign any Collider here to start (e.g. a BoxCollider) — it'll be replaced as needed.")]
    [SerializeField] private Collider physicsCollider;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    private GameObject _currentModelInstance;
    private NetworkedQuizGate _gate;
    private InteractionRequirements _requirements; // optional — e.g. "need a lockpick to even attempt this"
    private Rigidbody _rb;

    void Awake() {
        _gate = GetComponent<NetworkedQuizGate>();
        _requirements = GetComponent<InteractionRequirements>();

        _rb = GetComponent<Rigidbody>();
        if (_rb != null) {
            // Discrete (the default) only checks for overlap at the start/end of
            // each physics step — a small/fast Rigidbody can move further than
            // its own size in one step and pass straight through a thin floor
            // collider without ever registering the hit. This got noticeably
            // worse once items got per-item collider sizes (a key's small
            // SphereCollider tunnels far more easily than the old fixed generic
            // box did). Continuous checks the whole swept path instead. Forced
            // here in code rather than left to prefab setup so it can't get
            // reset/forgotten when the prefab is touched later.
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; // smooths the visual fall too
        }
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
            ApplyColliderForItem(item, null); // still size the fallback collider even with no visual
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

        ApplyColliderForItem(item, _currentModelInstance);
    }

    // Sizes/shapes physicsCollider to match this item — copied from a Collider
    // found on the model instance if it has one, otherwise a BoxCollider
    // fallback sized from the InventoryItem asset.
    private void ApplyColliderForItem(InventoryItem item, GameObject modelInstance) {
        var source = modelInstance != null
            ? modelInstance.GetComponentInChildren<Collider>(includeInactive: true)
            : null;

        if (source != null) {
            EnsureColliderType(source.GetType());
            CopyColliderShape(source, physicsCollider);
            // The model's collider was only a shape template — disable it so it
            // doesn't ALSO count as a compound collider under this Rigidbody
            // (the model sits under modelAnchor, a child of this GameObject).
            source.enabled = false;
        } else {
            EnsureColliderType(typeof(BoxCollider));
            if (physicsCollider is BoxCollider box) {
                box.size = item.worldColliderSize;
                box.center = item.worldColliderCenter;
            }
        }
    }

    // Swaps physicsCollider to a fresh component of colliderType if it isn't
    // already one — e.g. going from a previous item's BoxCollider to this
    // item's SphereCollider.
    private void EnsureColliderType(System.Type colliderType) {
        if (physicsCollider != null && physicsCollider.GetType() != colliderType) {
            Destroy(physicsCollider);
            physicsCollider = null;
        }
        if (physicsCollider == null)
            physicsCollider = (Collider)gameObject.AddComponent(colliderType);
    }

    // Copies shape values for the collider types WorldItem supports. Mesh
    // colliders aren't included — they need convex=true to work with a
    // non-kinematic Rigidbody, which usually isn't what you want for a small
    // pickup anyway; use Box/Sphere/Capsule on the model instead.
    private static void CopyColliderShape(Collider source, Collider dest) {
        switch (source) {
            case BoxCollider srcBox when dest is BoxCollider dstBox:
                dstBox.size = srcBox.size;
                dstBox.center = srcBox.center;
                break;
            case SphereCollider srcSphere when dest is SphereCollider dstSphere:
                dstSphere.radius = srcSphere.radius;
                dstSphere.center = srcSphere.center;
                break;
            case CapsuleCollider srcCapsule when dest is CapsuleCollider dstCapsule:
                dstCapsule.radius = srcCapsule.radius;
                dstCapsule.height = srcCapsule.height;
                dstCapsule.center = srcCapsule.center;
                dstCapsule.direction = srcCapsule.direction;
                break;
            default:
                Debug.LogWarning($"[WorldItem] Unsupported collider type '{source.GetType().Name}' " +
                                  "on model — use Box/Sphere/Capsule Collider instead.");
                break;
        }
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