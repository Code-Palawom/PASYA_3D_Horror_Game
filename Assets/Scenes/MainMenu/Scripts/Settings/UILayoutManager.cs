using System.Collections.Generic;
using UnityEngine;

public class UILayoutManager : MonoBehaviour {
    public static UILayoutManager Instance { get; private set; }

    readonly Dictionary<string, CustomizableUIElement> _elements = new();
    readonly Dictionary<string, Vector3> _lastValid = new(); // x offset, y offset, scale

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(CustomizableUIElement element) {
        if (string.IsNullOrEmpty(element.elementId)) {
            Debug.LogWarning($"[UILayoutManager] {element.name} has no elementId.", element);
            return;
        }

        _elements[element.elementId] = element;
        ApplyFromSettings(element);
    }

    public void Unregister(CustomizableUIElement element) {
        if (_elements.TryGetValue(element.elementId, out var e) && e == element)
            _elements.Remove(element.elementId);
    }

    void ApplyFromSettings(CustomizableUIElement element) {
        var settings = SettingsManager.Instance?.Current;
        if (settings != null && settings.buttonLayouts.TryGetValue(element.elementId, out var entry))
            CommitLayout(element, new Vector2(entry.x, entry.y), entry.scale, revertOnConflict: false);
        else
            CommitLayout(element, Vector2.zero, element.defaultScale, revertOnConflict: false);
    }

    // Attempts to move/resize an element from a settings-menu slider. Safe-area clamping
    // is always enforced; overlap prevention rejects the candidate and reverts to the
    // last valid layout. Returns false when a candidate was rejected for overlap.
    public bool PreviewLayout(string elementId, float offsetX, float offsetY, float scale)
        => TryApply(elementId, new Vector2(offsetX, offsetY), scale);

    // Attempts to move/resize an element to an arbitrary offset/scale — used for both
    // slider-driven changes and live click-and-drag / scale-slider edit mode. Same
    // safe-area + overlap rules as PreviewLayout. Returns false if rejected for overlap.
    public bool TryApply(string elementId, Vector2 offset, float scale) {
        if (!_elements.TryGetValue(elementId, out var element)) return false;
        return CommitLayout(element, offset, scale, revertOnConflict: true);
    }

    bool CommitLayout(CustomizableUIElement element, Vector2 offset, float scale, bool revertOnConflict) {
        scale = Mathf.Clamp(scale, element.minScale, element.maxScale);
        Vector2 safeOffset = element.ClampOffsetToSafeArea(offset, scale);

        Rect candidateBounds = element.GetProjectedBounds(safeOffset, scale);
        bool overlaps = false;

        foreach (var kvp in _elements) {
            if (kvp.Value == element) continue;
            if (candidateBounds.Overlaps(kvp.Value.GetCurrentBounds())) {
                overlaps = true;
                break;
            }
        }

        if (overlaps && revertOnConflict) {
            // Reject the move — snap back to the last known-good layout for this element.
            // During a drag this effectively "stops" the element at the last frame that
            // didn't overlap, rather than letting it pass through a neighbor.
            Vector3 last = _lastValid.TryGetValue(element.elementId, out var v)
                ? v
                : new Vector3(0f, 0f, element.defaultScale);

            element.ApplyLayout(last.x, last.y, last.z);
            return false;
        }

        element.ApplyLayout(safeOffset.x, safeOffset.y, scale);
        _lastValid[element.elementId] = new Vector3(safeOffset.x, safeOffset.y, scale);
        return true;
    }

    // Snapshot of every registered element's current live offset/scale, for saving.
    public Dictionary<string, ButtonLayoutEntry> CaptureCurrentLayouts() {
        var result = new Dictionary<string, ButtonLayoutEntry>();
        foreach (var kvp in _elements) {
            Vector2 offset = kvp.Value.GetCurrentOffset();
            result[kvp.Key] = new ButtonLayoutEntry(offset.x, offset.y, kvp.Value.GetCurrentScale());
        }
        return result;
    }

    public CustomizableUIElement Get(string elementId) =>
        _elements.TryGetValue(elementId, out var e) ? e : null;

    public IEnumerable<string> GetRegisteredIds() => _elements.Keys;
}