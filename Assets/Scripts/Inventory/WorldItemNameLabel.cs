using TMPro;
using UnityEngine;

// Floating item-name label that appears above/below a WorldItem while it's
// the local player's current interaction focus. Not a NetworkBehaviour —
// IInteractable.OnFocus is already only ever called locally on whichever
// client's raycast is hitting this object, so the label only needs to exist
// on that client; there's nothing here that needs to sync.
public class WorldItemNameLabel : MonoBehaviour {
    [Tooltip("World-space TextMeshPro doing the rendering. Must be a plain TextMeshPro " +
             "object (3D text), NOT one under a Canvas — this script drives its transform directly.")]
    [SerializeField] private TextMeshPro label;

    [Tooltip("Vertical gap between the item collider's edge and the label.")]
    [SerializeField] private float verticalPadding = 0.15f;

    private Camera _mainCamera;
    private int _lastShowFrame = -1;

    void Awake() {
        if (label != null) label.gameObject.SetActive(false);
    }

    public void SetText(string text) {
        if (label != null) label.text = text;
    }

    // Call once whenever the item's physics collider is (re)sized — i.e. right
    // after WorldItem.ApplyColliderForItem — so the label sits at the correct
    // height for THIS item's actual collider bounds instead of a fixed offset
    // that would look wrong on both a tiny key and a big crate.
    public void RepositionForCollider(Collider collider, bool below) {
        if (collider == null || label == null) return;

        float halfHeight = collider.bounds.extents.y;
        Vector3 anchor = collider.bounds.center + (below
            ? Vector3.down * (halfHeight + verticalPadding)
            : Vector3.up * (halfHeight + verticalPadding));

        label.transform.position = anchor;
    }

    // Called every frame this item is the local player's raycast focus (from
    // WorldItem.OnFocus). IInteractable has no matching "OnBlur"/"OnLoseFocus",
    // so visibility is driven by recency instead of an explicit hide call: if
    // Show() isn't called again next frame, Update() below turns it off.
    public void Show() {
        _lastShowFrame = Time.frameCount;
        if (label != null && !label.gameObject.activeSelf)
            label.gameObject.SetActive(true);
    }

    void Update() {
        if (label == null || !label.gameObject.activeSelf) return;

        if (Time.frameCount - _lastShowFrame > 1) {
            label.gameObject.SetActive(false);
            return;
        }

        if (_mainCamera == null) _mainCamera = Camera.main; // lazy-resolved, same as PlayerNameDisplay

        if (_mainCamera != null)
            label.transform.rotation = _mainCamera.transform.rotation; // billboard: match camera facing
    }
}