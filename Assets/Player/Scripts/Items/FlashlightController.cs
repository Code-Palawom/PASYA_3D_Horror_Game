using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class FlashlightController : NetworkBehaviour {
    [SerializeField] private Light flashlightLight;
    [SerializeField] private InputAction action;

    private NetworkVariable<bool> isOn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner // owner can write directly, no RPC needed
    );

    public override void OnNetworkSpawn() {
        isOn.OnValueChanged += OnFlashlightStateChanged;
        flashlightLight.enabled = isOn.Value;

        if (IsOwner) {
            action.performed += _ => OnTogglePerformed();
            action.Enable();
        }
    }

    public override void OnNetworkDespawn() {
        isOn.OnValueChanged -= OnFlashlightStateChanged;

        if (IsOwner) {
            action.Disable();
            action.Dispose();
        }
    }

    private void OnTogglePerformed() {
        isOn.Value = !isOn.Value;
    }

    private void OnFlashlightStateChanged(bool previous, bool current) {
        flashlightLight.enabled = current;
    }
}