using Unity.Netcode;
using UnityEngine;

public class DevTools : NetworkBehaviour {
    public static DevTools Instance { get; private set; }

    [SerializeField] private Camera playerCamera;

    private bool cullingFrozen = false;

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            enabled = false;
            return;
        }

        if (AuthManager.Instance != null && AuthManager.Instance.CurrentProfile.Role == "Developer") {
            Instance = this;
        }
    }

    public void ToggleCulling() {
        if (!IsOwner) return;

        cullingFrozen = !cullingFrozen;

        if (cullingFrozen) {
            playerCamera.cullingMatrix = playerCamera.projectionMatrix * playerCamera.worldToCameraMatrix;
        } else {
            playerCamera.ResetCullingMatrix();
        }
    }
}