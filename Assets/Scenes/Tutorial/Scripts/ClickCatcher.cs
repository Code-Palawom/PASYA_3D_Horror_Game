using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Put this on a full-stretch, transparent Image that covers the dialog panel
// (or the whole screen). TutorialDialog toggles SetInteractable() so this
// only actually blocks/catches clicks while a click-to-continue dialog is
// showing — otherwise it's raycast-transparent and clicks pass through.
[RequireComponent(typeof(Image))]
public class ClickCatcher : MonoBehaviour, IPointerClickHandler {
    public event Action OnClicked;

    Image image;

    void Awake() {
        image = GetComponent<Image>();
        image.raycastTarget = false; // off until a step actually needs it
    }

    // Enable/disable whether this catches clicks (blocks the raycast).
    public void SetInteractable(bool interactable) => image.raycastTarget = interactable;

    public void OnPointerClick(PointerEventData eventData) {
        OnClicked?.Invoke();
    }
}