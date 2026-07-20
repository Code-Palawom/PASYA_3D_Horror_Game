using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.Cinemachine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

[RequireComponent(typeof(CinemachineInputAxisController))]
[RequireComponent(typeof(CameraControls))]
public class ThirdPersonCameraLook : MonoBehaviour {
    [SerializeField] private RectTransform[] moveBoundaryRects;
    [SerializeField] private ChatUI chatUi;
    [SerializeField] private RectTransform chatBoundaryRect;
    [SerializeField] private InventoryUI inventoryUi;
    [SerializeField] private RectTransform inventoryUiRect;
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Header("Look Sensitivity")]
    [SerializeField] private float horizontalSensitivity = 0.25f;
    [SerializeField] private float verticalSensitivity = 0.18f;
    [SerializeField] private bool invertVertical = true;

    [Header("Pinch Zoom")]
    [SerializeField] private float zoomSensitivity = 0.02f;
    [SerializeField] private float minRadius = 1.5f;
    [SerializeField] private float maxRadius = 15f;
    [SerializeField] private float zoomSmoothSpeed = 10f;

    private bool showOverlay;
    private Finger lookFinger;
    private Finger pinchFinger1;
    private Finger pinchFinger2;
    private float pinchPrevDistance;
    private float targetRadius;

    private CinemachineInputAxisController inputAxisController;
    private CameraControls cameraController;

    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() {
        EnhancedTouchSupport.Disable();
        ReleaseLook();
        ReleasePinch();
    }

    private void Start() {
        inputAxisController = GetComponent<CinemachineInputAxisController>();
        cameraController = GetComponent<CameraControls>();

        if (!Application.isMobilePlatform) {
            inputAxisController.enabled = true;
            cameraController.enabled = true;
            this.enabled = false;
        }

        if (orbitalFollow != null) targetRadius = orbitalFollow.Radius;
        RefreshDebugMode();
    }

    private void Update() {
        foreach (Touch touch in Touch.activeTouches) {
            switch (touch.phase) {
                case TouchPhase.Began: OnTouchBegan(touch); break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary: OnTouchMoved(touch); break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled: OnTouchEnded(touch); break;
            }
        }
        ApplyZoomSmoothing();
    }

    private void OnTouchBegan(Touch touch) {
        if (IsBlocked(touch.screenPosition)) return;

        if (pinchFinger1 == null) {
            pinchFinger1 = touch.finger;
        } else if (pinchFinger2 == null && touch.finger != pinchFinger1) {
            pinchFinger2 = touch.finger;
            pinchPrevDistance = GetPinchDistance();
            ReleaseLook();
            return;
        }

        if (lookFinger == null && touch.finger != pinchFinger2) lookFinger = touch.finger;
    }

    private void OnTouchMoved(Touch touch) {
        if (lookFinger != null && touch.finger == lookFinger && pinchFinger2 == null) {
            float h = touch.delta.x * horizontalSensitivity;
            float v = touch.delta.y * verticalSensitivity * (invertVertical ? -1f : 1f);

            orbitalFollow.HorizontalAxis.Value += h;

            var vAxis = orbitalFollow.VerticalAxis;
            vAxis.Value = Mathf.Clamp(vAxis.Value + v, vAxis.Range.x, vAxis.Range.y);
            orbitalFollow.VerticalAxis = vAxis;
        }

        if (pinchFinger1 != null && pinchFinger2 != null &&
           (touch.finger == pinchFinger1 || touch.finger == pinchFinger2)) {
            float dist = GetPinchDistance();
            targetRadius -= (dist - pinchPrevDistance) * zoomSensitivity;
            targetRadius = Mathf.Clamp(targetRadius, minRadius, maxRadius);
            pinchPrevDistance = dist;
        }
    }

    private void OnTouchEnded(Touch touch) {
        if (lookFinger != null && touch.finger == lookFinger) ReleaseLook();
        if ((pinchFinger1 != null && touch.finger == pinchFinger1) ||
            (pinchFinger2 != null && touch.finger == pinchFinger2)) ReleasePinch();
    }

    private void ApplyZoomSmoothing() {
        if (orbitalFollow == null) return;
        orbitalFollow.Radius = zoomSmoothSpeed > 0f
            ? Mathf.Lerp(orbitalFollow.Radius, targetRadius, Time.deltaTime * zoomSmoothSpeed)
            : targetRadius;
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

    private float GetPinchDistance() {
        if (pinchFinger1 == null || pinchFinger2 == null) return 0f;
        return Vector2.Distance(
            pinchFinger1.currentTouch.screenPosition,
            pinchFinger2.currentTouch.screenPosition);
    }

    private void ReleaseLook() => lookFinger = null;

    private void ReleasePinch() {
        pinchFinger1 = pinchFinger2 = null;
        pinchPrevDistance = 0f;
    }

    public void RefreshDebugMode() {
        if (SettingsManager.Instance == null) {
            Debug.LogWarning("[ThirdPersonCameraLook] SettingsManager not ready yet.");
            return;
        }
        showOverlay = SettingsManager.Instance.Current.showDebugOverlay;
    }

    //#if UNITY_EDITOR
    private void OnGUI() {
        if (!showOverlay) return;
        GUI.Label(new Rect(10, 10, 380, 20), $"[3P] Look finger  : {(lookFinger != null ? lookFinger.index.ToString() : "none")}");
        GUI.Label(new Rect(10, 30, 380, 20), $"[3P] Pinch f1     : {(pinchFinger1 != null ? pinchFinger1.index.ToString() : "none")}");
        GUI.Label(new Rect(10, 50, 380, 20), $"[3P] Pinch f2     : {(pinchFinger2 != null ? pinchFinger2.index.ToString() : "none")}");
        GUI.Label(new Rect(10, 70, 380, 20), $"[3P] Radius target: {targetRadius:F2}  actual: {(orbitalFollow != null ? orbitalFollow.Radius.ToString("F2") : "—")}");
        GUI.Label(new Rect(10, 90, 380, 20), $"Active touches: {Touch.activeTouches.Count}");
    }
    //#endif
}