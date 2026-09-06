using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.Cinemachine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

[RequireComponent(typeof(CinemachineInputAxisController))]
public class FirstPersonCameraLook : MonoBehaviour {
    [SerializeField] private Player player; // used to gate input to the true owner only
    [SerializeField] private RectTransform[] moveBoundaryRects;
    [SerializeField] private ChatUI chatUi;
    [SerializeField] private RectTransform chatBoundaryRect;
    [SerializeField] private InventoryUI inventoryUi;
    [SerializeField] private RectTransform inventoryUiRect;
    [SerializeField] private CinemachinePanTilt panTilt;
    [SerializeField] private Transform fpYawTarget;

    [Header("Look Sensitivity")]
    [SerializeField] private float horizontalSensitivity = 0.25f;
    [SerializeField] private float verticalSensitivity = 0.18f;
    [SerializeField] private bool invertVertical = true;

    private bool showOverlay;
    private Finger lookFinger;
    private CinemachineInputAxisController inputAxisController;

    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() {
        EnhancedTouchSupport.Disable();
        lookFinger = null;
    }

    void Start() {
        inputAxisController = GetComponent<CinemachineInputAxisController>();

        // Only the true owner should ever drive this player's look axes.
        // Without this, CinemachineInputAxisController reads raw hardware
        // input for whatever object it's attached to regardless of whose
        // player it is — on desktop every instance in the scene was
        // enabling its own input axis controller, so one client's mouse
        // was silently steering every player's first-person look.
        bool isOwner = player != null && player.IsOwner;

        if (!Application.isMobilePlatform) {
            inputAxisController.enabled = isOwner;
            this.enabled = isOwner;
        } else {
            this.enabled = isOwner;
        }

        RefreshDebugMode();
    }

    private void Update() {
        foreach (Touch touch in Touch.activeTouches) {
            switch (touch.phase) {
                case TouchPhase.Began:
                    TryClaimLookFinger(touch);
                    break;
                case TouchPhase.Moved:
                    if (lookFinger != null && touch.finger == lookFinger) ApplyLookDelta(touch.delta);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (lookFinger != null && touch.finger == lookFinger) lookFinger = null;
                    break;
            }
        }
    }

    private void TryClaimLookFinger(Touch touch) {
        if (lookFinger != null) return;
        if (!IsBlocked(touch.screenPosition)) lookFinger = touch.finger;
    }

    private void ApplyLookDelta(Vector2 delta) {
        if (panTilt == null) return;
        float h = delta.x * horizontalSensitivity;
        float v = delta.y * verticalSensitivity * (invertVertical ? -1f : 1f);
        panTilt.PanAxis.Value += h;
        panTilt.TiltAxis.Value += v;
        if (fpYawTarget != null) fpYawTarget.Rotate(Vector3.up, h, Space.World);
        float min = panTilt.TiltAxis.Range.x;
        float max = panTilt.TiltAxis.Range.y;
        float newTilt = panTilt.TiltAxis.Value + v;
        panTilt.TiltAxis.Value = Mathf.Clamp(newTilt, min, max);
    }

    private bool IsBlocked(Vector2 screenPos) {
        if (moveBoundaryRects == null) return false;
        if (chatUi.isChatActive && RectTransformUtility.RectangleContainsScreenPoint(chatBoundaryRect, screenPos, null)) return true;
        if (inventoryUi.IsOpen && RectTransformUtility.RectangleContainsScreenPoint(inventoryUiRect, screenPos, null)) return true;

        foreach (RectTransform r in moveBoundaryRects) {
            if (r != null && RectTransformUtility.RectangleContainsScreenPoint(r, screenPos, null)) return true;
        }
        return false;
    }

    public void RefreshDebugMode() {
        if (SettingsManager.Instance == null) {
            Debug.LogWarning("[FirstPersonCameraLook] SettingsManager not ready yet.");
            return;
        }
        showOverlay = SettingsManager.Instance.Current.showDebugOverlay;
    }

    //#if UNITY_EDITOR
    private void OnGUI() {
        if (!showOverlay) return;
        GUI.Label(new Rect(10, 10, 420, 20), $"[1P] Look finger : {(lookFinger != null ? lookFinger.index.ToString() : "none")}");
        GUI.Label(new Rect(10, 30, 420, 20), $"[1P] Pan  : {(panTilt != null ? panTilt.PanAxis.Value.ToString("F1") + "°" : "—")}");
        GUI.Label(new Rect(10, 50, 420, 20), $"[1P] Tilt : {(panTilt != null ? panTilt.TiltAxis.Value.ToString("F1") + "°" : "—")}");
        GUI.Label(new Rect(10, 70, 420, 20), $"Active touches: {Touch.activeTouches.Count}");
    }
    //#endif
}