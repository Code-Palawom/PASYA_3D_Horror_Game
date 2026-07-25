using UnityEngine;
using UnityEngine.UI;

// Single HUD button for whatever action the currently-equipped item grants
// (flashlight toggle, etc.) — hidden entirely when the equipped item has no
// action (ItemActionType.None).
//
// Scene setup:
//   - A button (Image + Button) positioned wherever you want it on the HUD,
//     always ACTIVE in the hierarchy — visibility is driven by buttonRoot's
//     CanvasGroup/GameObject, not by toggling this whole component off.
//   - Assign iconImage (shows the current action's icon) and button.
//   - Call Init(playerItemActions) once, from wherever you already call
//     InventoryUI.Init(inventory) after network spawn (same local-player
//     setup pass).
public class ItemActionButtonUI : MonoBehaviour {
    [Tooltip("The GameObject to show/hide. Usually this component's own GameObject, but can be " +
             "a parent if this script lives somewhere else for organizational reasons.")]
    [SerializeField] private GameObject buttonRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    private PlayerItemActions _actions;

    public void Init(PlayerItemActions actions) {
        _actions = actions;
        _actions.OnActiveActionChanged += Refresh;

        if (button != null) button.onClick.AddListener(() => _actions.TriggerCurrentAction());

        Refresh(_actions.CurrentActionType, _actions.CurrentActionIcon, false);
    }

    void OnDestroy() {
        if (_actions != null) _actions.OnActiveActionChanged -= Refresh;
    }

    private void Refresh(ItemActionType type, Sprite icon, bool isActive) {
        bool show = type != ItemActionType.None;

        if (buttonRoot != null) buttonRoot.SetActive(show);
        if (!show) return;

        if (iconImage != null) {
            iconImage.enabled = icon != null;
            iconImage.sprite = icon;

            var c = iconImage.color;
            if (isActive) {
                iconImage.color = new Color(c.r, c.g, c.b, 0.50f);
            } else {
                iconImage.color = new Color(c.r, c.g, c.b, 0.25f);
            }
        }
    }
}