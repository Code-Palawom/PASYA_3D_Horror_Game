using UnityEngine;
using UnityEngine.UI;

// Drives in-game HUD "edit mode": click-and-drag to move any CustomizableUIElement,
// with a floating panel of X / Y / Scale sliders that follows whichever element is
// currently selected — drag for quick placement, sliders for precise adjustment.
// CustomizableUIElement.OnPointerDown/OnBeginDrag call SelectElement() directly, so
// this only needs to be entered/exited and to own the floating panel.
public class UIEditModeController : MonoBehaviour {
    public static UIEditModeController Instance { get; private set; }

    [SerializeField] private GameObject customizeControlsPanel;

    [Header("Floating control panel (tracks the selected element)")]
    [Tooltip("Parent RectTransform of the panel — this is what gets repositioned. Must live under a Canvas.")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Slider scaleSlider;
    [Tooltip("Screen-space pixel offset from the element's top edge to where the panel appears.")]
    [SerializeField] private Vector2 panelScreenOffset = new Vector2(0f, 80f);

    [Header("Edit mode chrome (optional)")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;

    bool _isSelectedDifferent;
    CustomizableUIElement _selected;
    RectTransform _panelParentRect;
    Camera _panelParentCamera;

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot != null) {
            panelRoot.gameObject.SetActive(false);
            _panelParentRect = panelRoot.parent as RectTransform;
            Canvas parentCanvas = panelRoot.GetComponentInParent<Canvas>();
            _panelParentCamera = parentCanvas != null ? parentCanvas.worldCamera : null;
        }

        if (scaleSlider != null) scaleSlider.onValueChanged.AddListener(_ => OnSliderChanged());

        if (saveButton != null) saveButton.onClick.AddListener(SaveAndExit);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelAndExit);
    }

    void OnEnable() {
        foreach (string id in UILayoutManager.Instance.GetRegisteredIds()) {
            UILayoutManager.Instance.Get(id)?.SetHighlighted(false);
        }
    }

    void LateUpdate() {
        // Keep sliders in sync in case the element moved via drag rather than the sliders
        // (SetValueWithoutNotify so this doesn't re-trigger OnSliderChanged in a loop).
        if(_selected != null) FollowSelectedElement();
    }

    //Called by CustomizableUIElement on pointer-down / begin-drag.
    public void SelectElement(CustomizableUIElement element) {
        _isSelectedDifferent = true;

        if (_selected != null) _selected.SetHighlighted(false);
        _selected = element;
        _selected.SetHighlighted(true);

        if (scaleSlider != null) {
            scaleSlider.minValue = element.minScale;
            scaleSlider.maxValue = element.maxScale;
        }

        if (panelRoot != null) panelRoot.gameObject.SetActive(true);
        scaleSlider.SetValueWithoutNotify(element.GetCurrentScale());

        FollowSelectedElement();
    }

    void FollowSelectedElement() {
        if (_selected == null || _panelParentRect == null) return;

        Vector3[] corners = new Vector3[4];
        _selected.RectTransform.GetWorldCorners(corners);
        Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_selected.CanvasCamera, topCenterWorld);
        screenPoint += panelScreenOffset;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelParentRect, screenPoint, _panelParentCamera, out Vector2 localPoint)) {
            panelRoot.anchoredPosition = ClampToParentBounds(localPoint);
        }
    }

    Vector2 ClampToParentBounds(Vector2 desiredLocalPos) {
        Rect parentRect = _panelParentRect.rect;
        Vector2 size = panelRoot.rect.size;
        Vector2 pivot = panelRoot.pivot;

        float minX = parentRect.xMin + size.x * pivot.x;
        float maxX = parentRect.xMax - size.x * (1f - pivot.x);
        float minY = parentRect.yMin + size.y * pivot.y;
        float maxY = parentRect.yMax - size.y * (1f - pivot.y);

        float x = minX <= maxX ? Mathf.Clamp(desiredLocalPos.x, minX, maxX) : (parentRect.xMin + parentRect.xMax) * 0.5f;
        float y = minY <= maxY ? Mathf.Clamp(desiredLocalPos.y, minY, maxY) : (parentRect.yMin + parentRect.yMax) * 0.5f;

        return new Vector2(x, y);
    }

    void OnSliderChanged() {
        if (_selected == null) return;

        float x = _selected.GetCurrentOffset().x;
        float y = _selected.GetCurrentOffset().y;
        float scale = _isSelectedDifferent ? _selected.GetCurrentScale() : scaleSlider.value;

        bool accepted = UILayoutManager.Instance.TryApply(_selected.elementId, new Vector2(x, y), scale);
        if (!accepted) {
            // Rejected for overlap — snap sliders back to wherever the element actually is.
            scaleSlider.SetValueWithoutNotify(_selected.GetCurrentScale());
        }

        _isSelectedDifferent = false;
    }

    void SaveAndExit() {
        var layouts = UILayoutManager.Instance.CaptureCurrentLayouts();
        SettingsManager.Instance.Save(s => {
            s.buttonLayouts.Clear();
            foreach (var kvp in layouts) s.buttonLayouts[kvp.Key] = kvp.Value;
        });

        OnDisableEditMode();
        gameObject.SetActive(false);
    }

    void CancelAndExit() {
        // Revert every registered element back to whatever's currently saved.
        var saved = SettingsManager.Instance.Current.buttonLayouts;
        foreach (string id in UILayoutManager.Instance.GetRegisteredIds()) {
            var element = UILayoutManager.Instance.Get(id);
            if (element == null) continue;

            if (saved.TryGetValue(id, out var entry))
                UILayoutManager.Instance.TryApply(id, new Vector2(entry.x, entry.y), entry.scale);
            else
                UILayoutManager.Instance.TryApply(id, Vector2.zero, element.defaultScale);
        }

        OnDisableEditMode();
        gameObject.SetActive(false);
    }

    void OnDisableEditMode() {
        foreach (string id in UILayoutManager.Instance.GetRegisteredIds()) {
            UILayoutManager.Instance.Get(id)?.SetHighlighted(true);
        }
        _selected = null;
    }
}