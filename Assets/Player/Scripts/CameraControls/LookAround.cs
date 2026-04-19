using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.Cinemachine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Handles right-side touch input for camera look (CinemachineOrbitalFollow)
/// and two-finger pinch to zoom (orbital radius).
///
/// Supports multiple joystick/UI boundary rects — any touch starting inside
/// ANY of them is excluded from camera look and pinch tracking.
///
/// SETUP REQUIREMENTS:
///   1. Attach to any persistent GameObject (e.g. GameManager or Player).
///   2. Assign moveBoundaryRects[] → all joystick / UI Panel RectTransforms to exclude.
///   3. Assign orbitalFollow → CinemachineOrbitalFollow on your 3rd-person vcam.
///   4. DISABLE or REMOVE CinemachineInputAxisController on all virtual cameras.
///   5. Input System: "New" or "Both" in Project Settings → Player.
/// </summary>
public class TouchCameraLook : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Joystick / UI Boundaries (Screen Space Overlay)")]
    [Tooltip("All RectTransforms whose area should be ignored for camera look and pinch zoom.")]
    [SerializeField] private RectTransform[] moveBoundaryRects;

    [Header("Cinemachine References")]
    [Tooltip("CinemachineOrbitalFollow on your third-person virtual camera.")]
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Header("Look Sensitivity")]
    [SerializeField] private float horizontalSensitivity = 0.25f;
    [SerializeField] private float verticalSensitivity   = 0.18f;
    [SerializeField] private bool  invertVertical        = true;

    [Header("Pinch Zoom")]
    [Tooltip("Multiplier for how fast pinch affects the orbital radius.")]
    [SerializeField] private float zoomSensitivity = 0.02f;
    [Tooltip("Minimum allowed orbital radius.")]
    [SerializeField] private float minRadius = 1.5f;
    [Tooltip("Maximum allowed orbital radius.")]
    [SerializeField] private float maxRadius = 15f;
    [Tooltip("Smooth speed for radius lerping. 0 = instant.")]
    [SerializeField] private float zoomSmoothSpeed = 10f;

    // ── Private State ──────────────────────────────────────────────────────────

    // Camera look
    private Finger _lookFinger;

    // Pinch zoom
    private Finger _pinchFinger1;
    private Finger _pinchFinger2;
    private float  _pinchPrevDistance;
    private float  _targetRadius;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        ReleaseLook();
        ReleasePinch();
    }

    private void Start()
    {
        // Seed target radius from current vcam setting so there's no pop on first pinch.
        if (orbitalFollow != null)
            _targetRadius = orbitalFollow.Radius;
    }

    private void Update()
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    OnTouchBegan(touch);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    // Stationary included so pinch distance stays current.
                    OnTouchMoved(touch);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnTouchEnded(touch);
                    break;
            }
        }

        ApplyZoomSmoothing();
    }

    // ── Touch Event Handlers ───────────────────────────────────────────────────

    private void OnTouchBegan(Touch touch)
    {
        bool blocked = IsOnBlockedRect(touch.screenPosition);

        // ── Pinch: claim the second non-blocked free finger ──
        if (!blocked)
        {
            if (_pinchFinger1 == null)
            {
                _pinchFinger1 = touch.finger;
                // Falls through — may also become the look finger below.
            }
            else if (_pinchFinger2 == null && touch.finger != _pinchFinger1)
            {
                _pinchFinger2 = touch.finger;
                // Two fingers confirmed → start pinch, cancel look to avoid drift.
                _pinchPrevDistance = GetFingerScreenDistance();
                ReleaseLook();
                return;
            }
        }

        // ── Look: claim first available non-blocked, non-pinch2 finger ──
        if (!blocked && _lookFinger == null && touch.finger != _pinchFinger2)
            _lookFinger = touch.finger;
    }

    private void OnTouchMoved(Touch touch)
    {
        // ── Look (suspended while pinching) ──
        if (_lookFinger != null
            && touch.finger == _lookFinger
            && _pinchFinger2 == null)
        {
            ApplyLookDelta(touch.delta);
        }

        // ── Pinch ──
        if (_pinchFinger1 != null && _pinchFinger2 != null
            && (touch.finger == _pinchFinger1 || touch.finger == _pinchFinger2))
        {
            float currentDist = GetFingerScreenDistance();
            float deltaDist   = currentDist - _pinchPrevDistance;
            _pinchPrevDistance = currentDist;

            // Fingers moving apart  → zoom in  (decrease radius)
            // Fingers moving closer → zoom out (increase radius)
            _targetRadius -= deltaDist * zoomSensitivity;
            _targetRadius  = Mathf.Clamp(_targetRadius, minRadius, maxRadius);
        }
    }

    private void OnTouchEnded(Touch touch)
    {
        if (_lookFinger != null && touch.finger == _lookFinger)
            ReleaseLook();

        if ((_pinchFinger1 != null && touch.finger == _pinchFinger1) ||
            (_pinchFinger2 != null && touch.finger == _pinchFinger2))
            ReleasePinch();
    }

    // ── Camera Application ─────────────────────────────────────────────────────

    private void ApplyLookDelta(Vector2 delta)
    {
        if (orbitalFollow == null) return;

        orbitalFollow.HorizontalAxis.Value += delta.x * horizontalSensitivity;
        orbitalFollow.VerticalAxis.Value   += delta.y * verticalSensitivity * (invertVertical ? -1f : 1f);
    }

    private void ApplyZoomSmoothing()
    {
        if (orbitalFollow == null) return;

        orbitalFollow.Radius = zoomSmoothSpeed > 0f
            ? Mathf.Lerp(orbitalFollow.Radius, _targetRadius, Time.deltaTime * zoomSmoothSpeed)
            : _targetRadius;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Returns true if screenPos falls inside ANY of the excluded boundary rects.</summary>
    private bool IsOnBlockedRect(Vector2 screenPos)
    {
        if (moveBoundaryRects == null) return false;

        foreach (RectTransform rect in moveBoundaryRects)
        {
            if (rect == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null))
                return true;
        }
        return false;
    }

    private float GetFingerScreenDistance()
    {
        if (_pinchFinger1 == null || _pinchFinger2 == null) return 0f;

        return Vector2.Distance(
            _pinchFinger1.currentTouch.screenPosition,
            _pinchFinger2.currentTouch.screenPosition
        );
    }

    private void ReleaseLook()  => _lookFinger = null;

    private void ReleasePinch()
    {
        _pinchFinger1      = null;
        _pinchFinger2      = null;
        _pinchPrevDistance = 0f;
    }

    // ── Editor Debug Overlay ───────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 350, 20),
            $"Look finger  : {(_lookFinger   != null ? _lookFinger.index.ToString()   : "none")}");
        GUI.Label(new Rect(10, 30, 350, 20),
            $"Pinch f1     : {(_pinchFinger1  != null ? _pinchFinger1.index.ToString()  : "none")}");
        GUI.Label(new Rect(10, 50, 350, 20),
            $"Pinch f2     : {(_pinchFinger2  != null ? _pinchFinger2.index.ToString()  : "none")}");
        GUI.Label(new Rect(10, 70, 350, 20),
            $"Target radius: {_targetRadius:F2}  |  Actual: {(orbitalFollow != null ? orbitalFollow.Radius.ToString("F2") : "—")}");
        GUI.Label(new Rect(10, 90, 350, 20),
            $"Active touches: {Touch.activeTouches.Count}");
    }
#endif
}