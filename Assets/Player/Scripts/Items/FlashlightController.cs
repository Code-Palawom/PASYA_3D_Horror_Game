using System;
using Unity.Netcode;
using UnityEngine;

// First concrete IItemAction — toggles a Light on/off when the player
// presses the HUD action button while a flashlight item is equipped (see
// PlayerItemActions, ItemActionButtonUI, InventoryItem.actionType).
//
// Two separate bits of state:
//   - _isOn:       the player's toggle INTENT ("do they want it on"), owner-
//                   written, persists across switching hotbar slots.
//   - _isEquipped: whether Flashlight is the CURRENTLY active hotbar item,
//                   recomputed from PlayerInventory on every client.
// The light is only actually lit when both are true — so switching away
// from the flashlight turns it off visually without losing whether it was
// on, and switching back re-lights it automatically with no extra button
// press.
//
// Networked (not just local) so other players actually see your flashlight,
// and see it turn off the moment you switch away — RefreshEquipped runs on
// every client, not just the owner, since PlayerInventory's active-slot
// state is Everyone-readable.
//
// Setup: attach to the player prefab alongside PlayerInventory and
// PlayerItemActions, and assign flashlightLight (e.g. a spotlight parented
// under the camera).
public class FlashlightController : NetworkBehaviour, IItemAction {
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] private Light flashlightLight;

    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    // Owner writes this directly — no ServerRpc round-trip — so the local
    // player's own toggle has zero input delay. Everyone else still reads it
    // fine (Everyone read permission), it just arrives via the normal
    // NetworkVariable sync instead of an RPC.
    private readonly NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private bool _isEquipped;

    public event Action OnStateChanged;

    public ItemActionType ActionType => ItemActionType.Flashlight;

    public bool IsActive => throw new NotImplementedException();

    public override void OnNetworkSpawn() {
        _isOn.OnValueChanged += (_, _) => ApplyState();
        _inventory.OnActiveSlotChanged += _ => RefreshEquipped();
        _inventory.OnSlotChanged += index => {
            if (index == _inventory.ActiveHotbarIndex) RefreshEquipped();
        };

        RefreshEquipped(); // covers late-joining clients too
    }

    // Recomputes whether Flashlight is the currently active/equipped item.
    // Runs on every client identically (not owner-only), so a remote player
    // watching you sees the light cut off the instant you switch slots.
    private void RefreshEquipped() {
        var slot = _inventory.GetSlot(_inventory.ActiveHotbarIndex);
        var item = slot.IsEmpty ? null : Registry?.Get(slot.ItemID.ToString());
        bool nowEquipped = item != null && item.actionType == ActionType;

        if (nowEquipped == _isEquipped) return;
        _isEquipped = nowEquipped;
        ApplyState();
    }

    // Called locally by PlayerItemActions.TriggerCurrentAction() when the
    // owner presses the HUD button. PlayerItemActions only runs for the
    // local player, so this is already only ever called by the owner — the
    // IsOwner check is just a guard, since an Owner-write NetworkVariable
    // throws if written by anyone else.
    public void Activate() {
        if (!IsOwner) return;
        _isOn.Value = !_isOn.Value;
    }

    private void ApplyState() {
        if (flashlightLight != null) flashlightLight.enabled = _isOn.Value && _isEquipped;
    }
}