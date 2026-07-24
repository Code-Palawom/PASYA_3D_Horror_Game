using System;
using System.Collections.Generic;
using UnityEngine;

// Bridges "what's currently equipped" (PlayerInventory) to "which gameplay
// component should react" (any component on this GameObject implementing
// IItemAction) and to the HUD button that lets the player trigger it.
//
// Setup:
//   - Add one component per special item behavior to the player prefab
//     (e.g. FlashlightController for ItemActionType.Flashlight). This class
//     finds them all automatically via GetComponents<IItemAction>() — no
//     manual list to fill in, same pattern as InteractionRequirements.
//   - Call Init(inventory) from wherever you already call
//     InventoryUI.Init(inventory) after network spawn.
//   - Wire ItemActionButtonUI to this via ItemActionButtonUI.Init(this).
public class PlayerItemActions : MonoBehaviour {
    [Tooltip("Leave empty to auto-resolve the current scene's registry via " +
             "ItemRegistry.Instance (set by that scene's GameBootstrap).")]
    [SerializeField] private ItemRegistry itemRegistry;
    private ItemRegistry Registry => itemRegistry != null ? itemRegistry : ItemRegistry.Instance;

    private PlayerInventory _inventory;
    private Dictionary<ItemActionType, IItemAction> _actions;

    public ItemActionType CurrentActionType { get; private set; } = ItemActionType.None;
    public Sprite CurrentActionIcon { get; private set; }

    // Fired whenever the equipped action changes (including to/from None) —
    // ItemActionButtonUI uses this to show/hide and set its icon.
    public event Action<ItemActionType, Sprite> OnActiveActionChanged;

    public void Init(PlayerInventory inventory) {
        _inventory = inventory;

        _actions = new Dictionary<ItemActionType, IItemAction>();
        foreach (var action in GetComponents<IItemAction>()) {
            if (action.ActionType == ItemActionType.None) continue; // None isn't a real button
            if (!_actions.TryAdd(action.ActionType, action))
                Debug.LogWarning($"[PlayerItemActions] Duplicate IItemAction for " +
                                  $"{action.ActionType} on '{name}' — only the first is used.");
        }

        _inventory.OnActiveSlotChanged += _ => Refresh();
        // Also refresh if the active slot's CONTENTS change without the active
        // index itself changing — e.g. the equipped item gets consumed down to
        // zero and the slot goes empty (button should disappear).
        _inventory.OnSlotChanged += index => {
            if (index == _inventory.ActiveHotbarIndex) Refresh();
        };

        Refresh();
    }

    private void Refresh() {
        var slot = _inventory.GetSlot(_inventory.ActiveHotbarIndex);
        var item = slot.IsEmpty ? null : Registry?.Get(slot.ItemID.ToString());

        var type = item != null ? item.actionType : ItemActionType.None;
        var icon = item != null ? (item.actionIcon != null ? item.actionIcon : item.icon) : null;

        if (type == CurrentActionType && icon == CurrentActionIcon) return;
        CurrentActionType = type;
        CurrentActionIcon = icon;
        OnActiveActionChanged?.Invoke(CurrentActionType, CurrentActionIcon);
    }

    // Called by ItemActionButtonUI when the player presses the HUD action button.
    public void TriggerCurrentAction() {
        if (CurrentActionType == ItemActionType.None) return;

        if (_actions != null && _actions.TryGetValue(CurrentActionType, out var action)) {
            action.Activate();
        } else {
            Debug.LogWarning($"[PlayerItemActions] No IItemAction registered for " +
                              $"{CurrentActionType} on '{name}' — button pressed but nothing to trigger.");
        }
    }
}