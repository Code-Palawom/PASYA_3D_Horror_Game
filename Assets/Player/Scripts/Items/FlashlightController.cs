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
// under the camera). Also assign/auto-resolve a PlayerNoiseEmitter on the
// same prefab — toggling the flashlight emits a noise burst (the click),
// and Player.cs separately amplifies ongoing footstep noise while it's on.
public class FlashlightController : NetworkBehaviour, IItemAction {
    [SerializeField] private Light flashlightLight;

    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    [Header("Noise")]
    [Tooltip("Leave empty to auto-resolve via GetComponent in OnNetworkSpawn.")]
    [SerializeField] private PlayerNoiseEmitter noiseEmitter;
    [SerializeField] private float toggleOnNoiseLoudness = 2f;
    [SerializeField] private float toggleOffNoiseLoudness = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource clickAudioSource;
    [SerializeField] private AudioClip toggleOnClip;
    [SerializeField] private AudioClip toggleOffClip;

    // Owner writes this directly — no ServerRpc round-trip — so the local
    // player's own toggle has zero input delay. Everyone else still reads it
    // fine (Everyone read permission), it just arrives via the normal
    // NetworkVariable sync instead of an RPC.
    private readonly NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private PlayerInventory _inventory;
    private bool _isEquipped;

    public ItemActionType ActionType => ItemActionType.Flashlight;

    // Drives which of InventoryItem.actionIconOn/actionIconOff the HUD shows.
    public bool IsActive => _isOn.Value;
    public event Action OnStateChanged;

    public override void OnNetworkSpawn() {
        _inventory = GetComponent<PlayerInventory>();
        if (noiseEmitter == null) noiseEmitter = GetComponent<PlayerNoiseEmitter>();

        _isOn.OnValueChanged += (_, current) => {
            ApplyState();
            PlayToggleClick(current);
            OnStateChanged?.Invoke();
        };
        _inventory.OnActiveSlotChanged += _ => RefreshEquipped();
        _inventory.OnSlotChanged += index => {
            if (index == _inventory.ActiveHotbarIndex) RefreshEquipped();
        };

        RefreshEquipped();
    }

    private void PlayToggleClick(bool isOn) {
        if (clickAudioSource == null) return;
        var clip = isOn ? toggleOnClip : toggleOffClip;
        if (clip != null) clickAudioSource.PlayOneShot(clip);
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

        // The click of the switch itself is audible to enemies — separate
        // from the ongoing amplification while the beam stays on.
        if (noiseEmitter != null) {
            float loudness = _isOn.Value ? toggleOnNoiseLoudness : toggleOffNoiseLoudness;
            noiseEmitter.EmitNoise(transform.position, loudness);
        }
    }

    private void ApplyState() {
        if (flashlightLight != null) flashlightLight.enabled = _isOn.Value && _isEquipped;
    }
}