using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.Cinemachine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class LookAround : MonoBehaviour {
    [SerializeField] private RectTransform moveBoundaryRect;

    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [SerializeField] private float horizontalSensitivity = 0.25f;
    [SerializeField] private float verticalSensitivity   = 0.18f;

    [SerializeField] private bool invertVertical = true;

    private Finger _lookFinger;

    private void OnEnable() {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable() {
        EnhancedTouchSupport.Disable();
        _lookFinger = null;
    }

    private void Update() {
        foreach(Touch touch in Touch.activeTouches){
            switch(touch.phase){
                case TouchPhase.Began:
                    TryClaimLookFinger(touch);
                    break;
                case TouchPhase.Moved:
                    if(_lookFinger != null && touch.finger == _lookFinger) ApplyLookDelta(touch.delta);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if(_lookFinger != null && touch.finger == _lookFinger) _lookFinger = null;
                    break;
            }
        }
    }

    private void TryClaimLookFinger(Touch touch) {
        if(_lookFinger != null) return;

        bool onJoystick = moveBoundaryRect != null && RectTransformUtility.RectangleContainsScreenPoint(moveBoundaryRect, touch.screenPosition, null);

        if(!onJoystick) _lookFinger = touch.finger;
    }

    private void ApplyLookDelta(Vector2 delta) {
        if(orbitalFollow == null) return;

        float yaw   =  delta.x * horizontalSensitivity;
        float pitch =  delta.y * verticalSensitivity * (invertVertical ? -1f : 1f);

        orbitalFollow.HorizontalAxis.Value += yaw;
        orbitalFollow.VerticalAxis.Value   += pitch;
    }


#if UNITY_EDITOR
    private void OnGUI() {
        GUI.Label(new Rect(10, 10, 300, 20), $"Look finger: {(_lookFinger != null ? _lookFinger.index.ToString() : "none")}");
        GUI.Label(new Rect(10, 30, 300, 20), $"Active touches: {Touch.activeTouches.Count}");
    }
#endif
}