using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

[RequireComponent(typeof(OnScreenStick))]
public class OnScreenStickSnap : MonoBehaviour, IPointerDownHandler {
    private OnScreenStick stick;
    private RectTransform stickRect;
    private FieldInfo pointerDownPosField;
    private FieldInfo startPosField;

    private void Awake() {
        stick = GetComponent<OnScreenStick>();
        stickRect = GetComponent<RectTransform>();

        // These are private fields inside Unity's OnScreenStick (Input System package).
        // Field names verified against com.unity.inputsystem's OnScreenStick.cs —
        // re-check these if you upgrade the Input System package and it stops working.
        pointerDownPosField = typeof(OnScreenStick).GetField("m_PointerDownPos", BindingFlags.NonPublic | BindingFlags.Instance);
        startPosField = typeof(OnScreenStick).GetField("m_StartPos", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (pointerDownPosField == null || startPosField == null) return;

        // Force the stick's internal "drag origin" to the background's center
        // (its resting position) instead of the actual click point...
        startPosField.SetValue(stick, (Vector3)stickRect.position);
        pointerDownPosField.SetValue(stick, RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, stickRect.position));

        // ...then immediately replay this event as a drag, so the handle
        // snaps to the click position on the very same frame instead of
        // waiting for the pointer to actually move.
        stick.OnDrag(eventData);
    }
}