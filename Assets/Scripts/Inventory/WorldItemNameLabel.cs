using TMPro;
using UnityEngine;

// Floating item-name label that appears above a WorldItem while it's the
// local player's current interaction focus. Not a NetworkBehaviour —
// IInteractable.OnFocus is already only ever called locally on whichever
// client's raycast is hitting this object, so the label only needs to exist
// on that client; there's nothing here that needs to sync.
public class WorldItemNameLabel : MonoBehaviour {
    [Tooltip("World-space TextMeshPro doing the rendering. Must be a plain TextMeshPro " +
             "object (3D text), NOT one under a Canvas — this script drives its transform directly.")]
    [SerializeField] private TextMeshPro label;

    [Tooltip("Vertical gap between the item collider's top and the label.")]
    [SerializeField] private float verticalPadding = 0.15f;

    private Camera _cam;
    private Collider _collider;
    private int _lastShowFrame = -1;

    void Awake() {
        if (label != null) label.gameObject.SetActive(false);
    }

    public void SetText(string text) {
        if (label != null) label.text = text;
    }

    // Call whenever the item's physics collider is (re)assigned/resized — i.e.
    // right after WorldItem.ApplyColliderForItem. We only cache the reference
    // here; actual positioning happens every frame in LateUpdate (see below),
    // because collider.bounds is a WORLD-SPACE axis-aligned box recalculated
    // from the current transform automatically. That's what keeps the label
    // reading "up" even while this item's Rigidbody is tumbling — a one-off
    // local-offset placement would rotate along with the object and end up
    // sideways/upside-down instead.
    public void SetCollider(Collider collider) {
        _collider = collider;
        RepositionAboveCollider();
    }

    // Called every frame this item is the local player's raycast focus (from
    // WorldItem.OnFocus). IInteractable has no matching "OnBlur"/"OnLoseFocus",
    // so visibility is driven by recency instead of an explicit hide call: if
    // Show() isn't called again next frame, LateUpdate below turns it off.
    public void Show() {
        _lastShowFrame = Time.frameCount;
        if (label != null && !label.gameObject.activeSelf)
            label.gameObject.SetActive(true);
    }

    void LateUpdate() {
        if (label == null || !label.gameObject.activeSelf) return;

        if (Time.frameCount - _lastShowFrame > 1) {
            label.gameObject.SetActive(false);
            return;
        }

        // Re-run every frame (not just once) so the label stays pinned
        // directly above the item even as a tumbling Rigidbody rotates.
        RepositionAboveCollider();

        if (_cam == null) _cam = Camera.main; // lazy-resolved, same as PlayerNameDisplay
        if (_cam != null)
            label.transform.rotation = _cam.transform.rotation; // billboard: match camera facing
    }

    private void RepositionAboveCollider() {
        if (_collider == null || label == null) return;

        // collider.bounds is world-space and axis-aligned regardless of the
        // object's current rotation, so Vector3.up here is always true world
        // "up" — not affected by however the item is currently tumbling.
        float halfHeight = _collider.bounds.extents.y;
        label.transform.position = _collider.bounds.center + Vector3.up * (halfHeight + verticalPadding);
    }
}