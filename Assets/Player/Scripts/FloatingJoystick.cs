using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    [SerializeField] private RectTransform background;
    [SerializeField] private CanvasGroup handleGroup;
    [SerializeField] private Canvas canvas; // drag the root Canvas here, for the camera ref
    [SerializeField] private float easeDuration = 0.15f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private RectTransform parentRect;
    private Vector2 homePosition;
    private Coroutine easeRoutine;

    private void Awake() {
        parentRect = background.parent as RectTransform; // <- key change
        homePosition = background.anchoredPosition;
        handleGroup.alpha = 0f;
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (easeRoutine != null) {
            StopCoroutine(easeRoutine);
            easeRoutine = null;
        }

        // Overlay canvas -> camera must be null even if pressEventCamera isn't
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventData.pressEventCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, cam, out var localPoint);

        background.anchoredPosition = localPoint;
        handleGroup.alpha = 1f;
    }

    public void OnPointerUp(PointerEventData eventData) {
        handleGroup.alpha = 0f;
        easeRoutine = StartCoroutine(EaseBackgroundHome());
    }

    private IEnumerator EaseBackgroundHome() {
        Vector2 start = background.anchoredPosition;
        float t = 0f;
        while (t < easeDuration) {
            t += Time.unscaledDeltaTime;
            float normalized = easeCurve.Evaluate(t / easeDuration);
            background.anchoredPosition = Vector2.Lerp(start, homePosition, normalized);
            yield return null;
        }
        background.anchoredPosition = homePosition;
        easeRoutine = null;
    }
}