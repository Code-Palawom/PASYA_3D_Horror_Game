using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TooltipTrigger : MonoBehaviour, IPointerClickHandler {
    [TextArea]
    [SerializeField] private string tooltipText;

    [Tooltip("If true, tooltip only shows when the attached Button is disabled (interactable = false).")]
    [SerializeField] private bool onlyShowWhenDisabled = false;

    [Tooltip("Optional: auto-filled from this GameObject if left empty.")]
    [SerializeField] private Button targetButton;

    private RectTransform rectTransform;

    private void Awake() {
        rectTransform = GetComponent<RectTransform>();

        if (targetButton == null)
            targetButton = GetComponent<Button>();
    }

    private void Start() {
        TooltipManager.Instance.RegisterTrigger(rectTransform);
    }

    private void OnDisable() {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.UnregisterTrigger(rectTransform);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (onlyShowWhenDisabled) {
            // No Button reference, or button is currently interactable -> don't show
            if (targetButton == null || targetButton.interactable)
                return;
        }

        TooltipManager.Instance.ShowTooltip(tooltipText, eventData.position, eventData.pressEventCamera);
    }
}