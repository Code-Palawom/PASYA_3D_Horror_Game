using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Owner-only local input for switching the active hotbar slot, built on
// InputAction callbacks rather than per-frame polling.
//
// This version builds its InputActions directly in code (no .inputactions
// asset required). If your project already has a generated Input Actions
// class (e.g. "PlayerControls"), swap the actions below for references to
// that asset instead — the OnXxxPerformed callback logic stays the same.
[RequireComponent(typeof(PlayerInventory))]
public class HotbarInput : NetworkBehaviour {
    [SerializeField] private float scrollThreshold = 0.05f; // dead zone to avoid trackpad jitter

    private PlayerInventory _inventory;

    private InputAction[] _digitActions; // 1–9 keys, one action per digit
    private InputAction _scrollAction;

    private void Awake() {
        _inventory = GetComponent<PlayerInventory>();
        BuildActions();
    }

    private void BuildActions() {
        // One button-per-digit action. Each binding stores its slot index in
        // the interaction/processor-free path, so we can't get the "which
        // key" info off InputAction.CallbackContext directly for a single
        // shared action — instead, wire 9 individual actions so each
        // callback closure already knows its own index. Cheap, and it's
        // the more idiomatic pattern for this Input System since actions
        // aren't easily parameterized by which binding fired.
        _digitActions = new InputAction[PlayerInventory.HotbarSize];
        for (int i = 0; i < PlayerInventory.HotbarSize; i++) {
            var action = new InputAction($"SelectHotbar{i + 1}", InputActionType.Button,
                $"<Keyboard>/{i + 1}");
            int captured = i; // avoid closure-over-loop-variable bug
            action.performed += _ => RequestSlot(captured);
            _digitActions[i] = action;
        }

        _scrollAction = new InputAction("HotbarScroll", InputActionType.Value,
            "<Mouse>/scroll/y");
        _scrollAction.performed += OnScrollPerformed;
    }

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            // Non-owners never enable input — no polling, no callbacks, nothing.
            return;
        }
        EnableActions();
    }

    public override void OnNetworkDespawn() => DisableActions();

    private void EnableActions() {
        foreach (var action in _digitActions) action.Enable();
        _scrollAction.Enable();
    }

    private void DisableActions() {
        if (_digitActions != null)
            foreach (var action in _digitActions) action?.Disable();
        _scrollAction?.Disable();
    }

    private void OnScrollPerformed(InputAction.CallbackContext ctx) {
        float scroll = ctx.ReadValue<float>();
        if (Mathf.Abs(scroll) <= scrollThreshold) return;

        int dir = scroll > 0 ? -1 : 1; // scroll up = previous slot, flip if you want the opposite feel
        int next = (_inventory.ActiveHotbarIndex + dir + PlayerInventory.HotbarSize)
                   % PlayerInventory.HotbarSize;
        RequestSlot(next);
    }

    private void RequestSlot(int index) {
        if (index == _inventory.ActiveHotbarIndex) return; // avoid redundant RPC spam
        _inventory.SetActiveSlotServerRpc(index);
    }
}