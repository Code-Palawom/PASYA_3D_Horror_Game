using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CustomizableUIElement : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler {
    [Tooltip("Unique key used to save/load this element. Must be unique across all customizable elements.")]
    public string elementId;

    [Header("Defaults (auto-captured from scene placement)")]
    public Vector2 defaultAnchoredPosition;
    [Range(0.5f, 2f)] public float defaultScale = 1f;

    [Header("Customization limits")]
    //public Vector2 positionRange = new Vector2(300f, 300f); // max +/- offset in x, y
    public float minScale = 0.75f;
    public float maxScale = 1.5f;

    [Header("Selection Highlight")]
    [Tooltip("Image whose alpha is adjusted on select/deselect. Auto-fetched from this GameObject if left empty.")]
    public Image highlightImage;
    [Range(0f, 1f)] public float selectedAlpha = 1f;
    [Range(0f, 1f)] public float deselectedAlpha = 0.5f;

    [Header("Safe area")]
    [Tooltip("Extra inset applied on top of the device safe area, in canvas units.")]
    public float safeAreaPadding = 8f;

    [Header("Overlap")]
    [Tooltip("Extra buffer added around this element's bounds when checking overlap against other elements.")]
    public float overlapPadding = 4f;

    RectTransform _rect;
    RectTransform _canvasRect;
    Canvas _rootCanvas;

    Vector2 _dragStartOffset;
    Vector2 _dragStartLocalPointerPos;

    public RectTransform RectTransform => _rect;
    public Camera CanvasCamera => _rootCanvas != null ? _rootCanvas.worldCamera : null;

    void Awake() {
        _rect = (RectTransform)transform;
        defaultAnchoredPosition = _rect.anchoredPosition; // wherever you placed it in the editor
        //defaultScale = _rect.localScale.x; // assume uniform scale

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null) {
            _rootCanvas = parentCanvas.rootCanvas;
            _canvasRect = (RectTransform)_rootCanvas.transform;
        } else {
            Debug.LogWarning($"[CustomizableUIElement] {name} could not find a parent Canvas.", this);
        }

        if (highlightImage == null) highlightImage = GetComponent<Image>();
    }

    void OnEnable() => UILayoutManager.Instance?.Register(this);
    void OnDisable() => UILayoutManager.Instance?.Unregister(this);

    // Applies a layout offset/scale directly (only clamped to position/scale ranges).
    // Prefer going through UILayoutManager.PreviewLayout/TryApply so safe-area and
    // overlap rules are enforced before this is called.
    public void ApplyLayout(float offsetX, float offsetY, float scale) {
        scale = Mathf.Clamp(scale, minScale, maxScale);

        _rect.anchoredPosition = defaultAnchoredPosition + new Vector2(offsetX, offsetY);
        _rect.localScale = Vector3.one * scale;
    }

    public Vector2 GetCurrentOffset() => (Vector2)_rect.anchoredPosition - defaultAnchoredPosition;
    public float GetCurrentScale() => _rect.localScale.x;

    // ── Click-and-drag (edit mode) ─────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData) {
        if (UIEditModeController.Instance == null) return;
        UIEditModeController.Instance.SelectElement(this);
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (UIEditModeController.Instance == null) return;
        if (_canvasRect == null) return;

        UIEditModeController.Instance.SelectElement(this);
        _dragStartOffset = GetCurrentOffset();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, eventData.position, eventData.pressEventCamera, out _dragStartLocalPointerPos);
    }

    public void OnDrag(PointerEventData eventData) {
        if (UIEditModeController.Instance == null) return;
        if (_canvasRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPointerPos)) {
            Vector2 delta = localPointerPos - _dragStartLocalPointerPos;
            Vector2 candidateOffset = _dragStartOffset + delta;
            UILayoutManager.Instance.TryApply(elementId, candidateOffset, GetCurrentScale());
        }
    }

    public void SetHighlighted(bool highlighted) {
        if (highlightImage == null) return;
        Color c = highlightImage.color;
        c.a = highlighted ? selectedAlpha : deselectedAlpha;
        highlightImage.color = c;
    }

    // ── Safe area ────────────────────────────────────────────────────────

    // Where this element's anchor reference point sits in canvas-center-relative local
    // units, i.e. (0,0) = canvas center regardless of this element's own anchor settings.
    Vector2 AnchorReferenceCanvasLocal() {
        if (_canvasRect == null) return Vector2.zero;

        Vector2 canvasSize = _canvasRect.rect.size;
        Vector2 anchorMid = (_rect.anchorMin + _rect.anchorMax) * 0.5f;

        return new Vector2(
            Mathf.Lerp(-canvasSize.x * 0.5f, canvasSize.x * 0.5f, anchorMid.x),
            Mathf.Lerp(-canvasSize.y * 0.5f, canvasSize.y * 0.5f, anchorMid.y));
    }

    // Converts Screen.safeArea into an anchoredPosition-space rect for THIS element's
    // anchor setup. Returns null if there's no canvas reference to compute against.
    Rect? SafeAreaInAnchoredSpace() {
        if (_canvasRect == null) return null;

        Rect safe = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize.x <= 0f || screenSize.y <= 0f) return null;

        Vector2 canvasSize = _canvasRect.rect.size;

        // Map safe area (pixel space, origin bottom-left) into canvas-center-relative local units.
        Vector2 safeMinLocal = new Vector2(
            (safe.xMin / screenSize.x) * canvasSize.x - canvasSize.x * 0.5f,
            (safe.yMin / screenSize.y) * canvasSize.y - canvasSize.y * 0.5f);
        Vector2 safeMaxLocal = new Vector2(
            (safe.xMax / screenSize.x) * canvasSize.x - canvasSize.x * 0.5f,
            (safe.yMax / screenSize.y) * canvasSize.y - canvasSize.y * 0.5f);

        Vector2 anchorRef = AnchorReferenceCanvasLocal();

        // Shift into this element's anchoredPosition space, then pull in by padding.
        Vector2 min = safeMinLocal - anchorRef + Vector2.one * safeAreaPadding;
        Vector2 max = safeMaxLocal - anchorRef - Vector2.one * safeAreaPadding;

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // Clamps a candidate offset so the element's full bounds (at candidateScale) stay
    // inside the device safe area. Falls back to plain positionRange clamping if no
    // canvas/safe-area info is available (e.g. missing parent Canvas).
    public Vector2 ClampOffsetToSafeArea(Vector2 candidateOffset, float candidateScale) {
        Rect? safeRect = SafeAreaInAnchoredSpace();
        if (safeRect == null) {
            return new Vector2(candidateOffset.x, candidateOffset.y);
        }

        Vector2 halfSize = _rect.rect.size * candidateScale * 0.5f;
        Vector2 desiredAnchoredPos = defaultAnchoredPosition + candidateOffset;

        float minX = safeRect.Value.xMin + halfSize.x;
        float maxX = safeRect.Value.xMax - halfSize.x;
        float minY = safeRect.Value.yMin + halfSize.y;
        float maxY = safeRect.Value.yMax - halfSize.y;

        // If the element is too big to fit the safe area at all, center it instead of
        // producing an inverted (min > max) clamp range.
        float clampedX = minX <= maxX
            ? Mathf.Clamp(desiredAnchoredPos.x, minX, maxX)
            : (safeRect.Value.xMin + safeRect.Value.xMax) * 0.5f;
        float clampedY = minY <= maxY
            ? Mathf.Clamp(desiredAnchoredPos.y, minY, maxY)
            : (safeRect.Value.yMin + safeRect.Value.yMax) * 0.5f;

        Vector2 clampedOffset = new Vector2(clampedX, clampedY) - defaultAnchoredPosition;

        return new Vector2(clampedOffset.x, clampedOffset.y);
    }

    // ── Overlap ──────────────────────────────────────────────────────────

    // Bounds (canvas-center-relative space) this element would occupy at the given
    // offset/scale, without actually moving it. Used for overlap tests before commit.
    public Rect GetProjectedBounds(Vector2 offset, float scale) {
        Vector2 size = _rect.rect.size * scale;
        Vector2 anchoredPos = defaultAnchoredPosition + offset;
        Vector2 centerRelative = AnchorReferenceCanvasLocal() + anchoredPos;

        Rect r = new Rect(centerRelative - size * 0.5f, size);
        r.xMin -= overlapPadding; r.xMax += overlapPadding;
        r.yMin -= overlapPadding; r.yMax += overlapPadding;
        return r;
    }

    // Bounds at this element's current live anchoredPosition/scale.
    public Rect GetCurrentBounds() {
        return GetProjectedBounds(GetCurrentOffset(), GetCurrentScale());
    }
}