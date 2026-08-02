using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RotateCharacter : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler {
    [SerializeField] private Image dragArea;           // invisible UI image that catches the drag; auto-uses this GameObject's Image if unassigned
    [SerializeField] private float dragSpeed = 0.3f;   // degrees rotated per pixel of drag
    [SerializeField] private bool invertX = false;

    [SerializeField] private ResetCharacterRotation resetRotationController; // optional reference to a ResetCharacterRotation component to reset the rotation when needed

    private void Awake() {
        if (dragArea != null)
            dragArea.raycastTarget = true; // must be true to receive drag events, even fully transparent
    }

    public void OnBeginDrag(PointerEventData eventData) {
        resetRotationController.BeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        resetRotationController.UpdateRotation(eventData, dragSpeed, invertX);
    }

    public void OnEndDrag(PointerEventData eventData) { }
}