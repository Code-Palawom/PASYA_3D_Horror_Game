using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Put this on a full-stretch, transparent Image (Raycast Target = true) that
// covers the dialog panel (or the whole screen), so any click on it counts
// as "continue". TutorialDialog subscribes to OnClicked.
public class ClickCatcher : MonoBehaviour, IPointerClickHandler {
    public event Action OnClicked;

    public void OnPointerClick(PointerEventData eventData) {
        OnClicked?.Invoke();
    }
}