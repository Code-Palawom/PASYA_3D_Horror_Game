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

    private InventoryItem _currentItem;
    private IItemAction _currentAction;

    public ItemActionType CurrentActionType { get; private set; } = ItemActionType.None;
    public Sprite CurrentActionIcon { get; private set; }

    // Fired whenever the equipped action or its icon changes (including
    // to/from None, or a toggle flipping while still equipped) —
    // ItemActionButtonUI uses this to show/hide and set its icon.
    public event Action<ItemActionType, Sprite, bool> OnActiveActionChanged;

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

        // item is a ScriptableObject asset reference — Registry.Get returns
        // the same instance every time, so reference equality reliably
        // detects "the equipped item actually changed" even when two
        // different items share the same actionType (e.g. two flashlight
        // variants), which a type-only comparison would miss.
        if (type == CurrentActionType && item == _currentItem) return;

        if (_currentAction != null) _currentAction.OnStateChanged -= HandleActionStateChanged;

        CurrentActionType = type;
        _currentItem = item;
        _currentAction = null;
        if (type != ItemActionType.None && _actions.TryGetValue(type, out var action)) {
            _currentAction = action;
            _currentAction.OnStateChanged += HandleActionStateChanged;
        }

        UpdateIconAndNotify();
    }

    // The equipped action's own on/off state changed (e.g. flashlight
    // toggled) without the equipped item itself changing — just refresh the
    // icon, no need to re-resolve _currentAction.
    private void HandleActionStateChanged() => UpdateIconAndNotify();

    private void UpdateIconAndNotify() {
        CurrentActionIcon = ComputeIcon();
        OnActiveActionChanged?.Invoke(CurrentActionType, CurrentActionIcon, _currentAction != null && _currentAction.IsActive);
    }

    // actionIconOn/actionIconOff (state-specific) -> actionIcon (generic
    // fallback) -> item.icon (item's normal icon) -> null.
    private Sprite ComputeIcon() {
        if (_currentItem == null) return null;

        bool isActive = _currentAction != null && _currentAction.IsActive;
        var stateIcon = isActive ? _currentItem.actionIconOn : _currentItem.actionIconOff;
        if (stateIcon != null) return stateIcon;

        return _currentItem.actionIcon != null ? _currentItem.actionIcon : _currentItem.icon;
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