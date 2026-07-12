using Unity.Cinemachine;
using UnityEngine;

// Freezes or restores player input while the quiz canvas is open.
// Hook into your actual input/movement system here.
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }

    private MonoBehaviour[] _cameraComponents;
    private bool[] _wasEnabled;

    public bool IsControlFrozen { get; private set; }

    void Awake() => Instance = this;

    public void SetPlayerInputEnabled(bool enabled) {
        var localPlayer = FindLocalPlayer();
        if (localPlayer == null) return;

        if (localPlayer.TryGetComponent<InteractionController>(out var ic))
            ic.enabled = enabled;

        localPlayer.GetComponent<Player>().enabled = enabled;

        if (!enabled) {
            var list = new System.Collections.Generic.List<MonoBehaviour>();
            list.AddRange(localPlayer.GetComponentsInChildren<CinemachineInputAxisController>(true));
            list.AddRange(localPlayer.GetComponentsInChildren<FirstPersonCameraLook>(true));
            list.AddRange(localPlayer.GetComponentsInChildren<ThirdPersonCameraLook>(true));
            list.AddRange(localPlayer.GetComponentsInChildren<CameraControls>(true));

            _cameraComponents = list.ToArray();
            _wasEnabled = new bool[_cameraComponents.Length];
            for (int i = 0; i < _cameraComponents.Length; i++) {
                _wasEnabled[i] = _cameraComponents[i].enabled;
                _cameraComponents[i].enabled = false;
            }
        } else {
            if (_cameraComponents == null) return;
            for (int i = 0; i < _cameraComponents.Length; i++) {
                if (_cameraComponents[i] != null) _cameraComponents[i].enabled = _wasEnabled[i];
            }
            _cameraComponents = null;
        }

        IsControlFrozen = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    GameObject FindLocalPlayer() {
        return GameObject.FindWithTag("LocalPlayer");
    }
}