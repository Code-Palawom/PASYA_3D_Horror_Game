using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

public class TooltipManager : MonoBehaviour {
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private TMP_Text tooltipLabel;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private float screenPadding = 20f;
    [SerializeField] private Vector2 offsetFromTarget = new Vector2(0, 60f);
    [SerializeField] private float autoHideDuration = 3f;

    private float autoHideAtTime = -1f;

    // Tracks all active tooltip trigger buttons so we can punch holes for them
    private readonly List<RectTransform> registeredTriggers = new List<RectTransform>();

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        tooltipPanel.SetActive(false);
    }

    public void RegisterTrigger(RectTransform triggerRect) {
        if (!registeredTriggers.Contains(triggerRect))
            registeredTriggers.Add(triggerRect);
    }

    public void UnregisterTrigger(RectTransform triggerRect) {
        registeredTriggers.Remove(triggerRect);
    }

    // Kept for compatibility — positions relative to a world-space point (e.g. a button's transform)
    public void ShowTooltip(string text, Vector3 worldPosition) {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
        ShowTooltip(text, screenPoint, null);
    }

    // Positions relative to an actual screen-space click point (e.g. eventData.position)
    public void ShowTooltip(string text, Vector2 screenPoint, Camera eventCamera) {
        tooltipLabel.text = text;

        tooltipPanel.SetActive(true);
        tooltipPanel.transform.SetAsLastSibling();

        // Tooltip canvas is Screen Space - Overlay, so camera is always null for this conversion.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, null, out Vector2 targetLocalPoint);

        PositionTooltip(targetLocalPoint);

        autoHideAtTime = Time.time + autoHideDuration;
    }

    public void HideTooltip() {
        tooltipPanel.SetActive(false);
    }

    private void Update() {
        if (!tooltipPanel.activeSelf)
            return;

        if (Time.time >= autoHideAtTime) {
            HideTooltip();
            return;
        }

        // Pointer.current unifies Mouse, Touchscreen, and Pen under one API — no need to
        // branch between Mouse.current and Touchscreen.current separately.
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) {
            Vector2 screenPoint = Pointer.current.position.ReadValue();

            // If the click landed on a registered tooltip trigger, let that trigger's own
            // OnPointerClick handle things (opening/switching tooltip, resetting the timer) — don't hide here.
            if (!IsScreenPointOverRegisteredTrigger(screenPoint))
                HideTooltip();
        }
    }

    private bool IsScreenPointOverRegisteredTrigger(Vector2 screenPoint) {
        for (int i = 0; i < registeredTriggers.Count; i++) {
            RectTransform rt = registeredTriggers[i];
            if (rt == null) continue;

            Canvas triggerCanvas = rt.GetComponentInParent<Canvas>();
            Camera cam = (triggerCanvas != null && triggerCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? triggerCanvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, cam))
                return true;
        }
        return false;
    }

    private void PositionTooltip(Vector2 targetLocalPoint) {
        Canvas.ForceUpdateCanvases();
        Vector2 size = tooltipRect.rect.size;
        Vector2 canvasSize = canvasRect.rect.size;

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        float canvasLeft = -canvasSize.x * 0.5f + screenPadding;
        float canvasRightB = canvasSize.x * 0.5f - screenPadding;
        float canvasBottom = -canvasSize.y * 0.5f + screenPadding;
        float canvasTop = canvasSize.y * 0.5f - screenPadding;

        // --- Vertical flip: prefer above, fall back to below ---
        float verticalGap = Mathf.Abs(offsetFromTarget.y);
        float aboveCenterY = targetLocalPoint.y + verticalGap + halfHeight;
        float belowCenterY = targetLocalPoint.y - verticalGap - halfHeight;

        bool fitsAbove = (aboveCenterY + halfHeight) <= canvasTop;
        bool fitsBelow = (belowCenterY - halfHeight) >= canvasBottom;

        float finalY;
        if (fitsAbove) finalY = aboveCenterY;
        else if (fitsBelow) finalY = belowCenterY;
        else finalY = aboveCenterY; // neither fits cleanly; clamp below handles it

        // --- Horizontal flip: prefer centered on target, shift to left/right-aligned if it overflows ---
        float centeredX = targetLocalPoint.x + offsetFromTarget.x;
        bool overflowsRight = (centeredX + halfWidth) > canvasRightB;
        bool overflowsLeft = (centeredX - halfWidth) < canvasLeft;

        float finalX;
        if (overflowsRight && !overflowsLeft)
            finalX = canvasRightB - halfWidth;   // anchor tooltip's right edge inside the screen
        else if (overflowsLeft && !overflowsRight)
            finalX = canvasLeft + halfWidth;     // anchor tooltip's left edge inside the screen
        else
            finalX = centeredX;

        Vector2 finalPos = new Vector2(finalX, finalY);

        // Safety net if the tooltip is still too big for the available space
        finalPos.x = Mathf.Clamp(finalPos.x, canvasLeft + halfWidth, canvasRightB - halfWidth);
        finalPos.y = Mathf.Clamp(finalPos.y, canvasBottom + halfHeight, canvasTop - halfHeight);

        tooltipRect.anchoredPosition = finalPos;
    }
}