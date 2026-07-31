using System;
using PrimeTween;
using TMPro;
using UnityEngine;

// Where the dialog should sit relative to the highlight target.
// Auto picks above/below automatically depending on target's screen position.
// The other 9 values are a tooltip-style anchor grid around the target's box:
// Top/Bottom = above/below the target, Middle = beside it (left/right) or
// directly centered on it. Left/Center/Right controls horizontal alignment
// for Top/Bottom, and is ignored (uses Center) for Middle.
public enum DialogPosition {
    Auto,
    TopLeft, TopCenter, TopRight,
    MiddleLeft, MiddleCenter, MiddleRight,
    BottomLeft, BottomCenter, BottomRight
}

// Tutorial dialog box. Clicking anywhere on the dialog (via the click-catcher
// covering its full rect) advances to the next tutorial step, if enabled.
// Positions itself relative to the current highlight target — either Auto
// (above/below, whichever fits) or a fixed anchor around the target's box —
// animating smoothly into place on each step.
public class TutorialDialog : MonoBehaviour {
    public GameObject root;
    public RectTransform dialogRect;   // the movable panel (drag the Panel/root's RectTransform)
    public RectTransform canvasRect;   // the parent TutorialCanvas RectTransform
    public TMP_Text bodyText;
    public ClickCatcher clickCatcher;  // full-rect transparent Image with raycastTarget = true

    [Header("Positioning")]
    public float spacing = 24f;        // gap between target and dialog
    public float screenEdgePadding = 20f;
    public float moveDuration = 0.35f;
    public Ease moveEase = Ease.OutCubic;

    Action onContinue;
    Tween moveTween;
    bool clickToContinue;

    void Awake() {
        if (clickCatcher != null)
            clickCatcher.OnClicked += HandleClicked;
    }

    void OnDestroy() {
        if (clickCatcher != null)
            clickCatcher.OnClicked -= HandleClicked;
    }

    // Shows the dialog with text, animating it into position relative to target.
    public void Show(string text, RectTransform target, DialogPosition position, bool clickToContinue, Action onContinueCallback = null) {
        root.SetActive(true);
        bodyText.text = text;
        onContinue = onContinueCallback;
        this.clickToContinue = clickToContinue;
        if (clickCatcher != null) clickCatcher.SetInteractable(clickToContinue);

        if (target != null) {
            Vector2 destination = CalculatePosition(target, position);
            if (moveTween.isAlive) moveTween.Stop();
            moveTween = Tween.UIAnchoredPosition(dialogRect, destination, moveDuration, moveEase);
        }
    }

    // Hides the dialog immediately. Call this when the step's actual completion
    // condition (button click, custom event) fires, for non-click-to-continue steps.
    public void Hide() {
        if (moveTween.isAlive) moveTween.Stop();
        root.SetActive(false);
        onContinue = null;
        if (clickCatcher != null) clickCatcher.SetInteractable(false);
    }

    Vector2 CalculatePosition(RectTransform target, DialogPosition position) {
        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners); // 0=BL, 1=TL, 2=TR, 3=BR

        Vector2 bl = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 tr = canvasRect.InverseTransformPoint(corners[2]);
        float minX = bl.x, maxX = tr.x;
        float minY = bl.y, maxY = tr.y;
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;

        float dialogHalfW = dialogRect.rect.width / 2f;
        float dialogHalfH = dialogRect.rect.height / 2f;

        Vector2 pos;

        switch (position) {
            case DialogPosition.TopLeft:
                pos = new Vector2(minX + dialogHalfW, maxY + spacing + dialogHalfH);
                break;
            case DialogPosition.TopCenter:
                pos = new Vector2(centerX, maxY + spacing + dialogHalfH);
                break;
            case DialogPosition.TopRight:
                pos = new Vector2(maxX - dialogHalfW, maxY + spacing + dialogHalfH);
                break;
            case DialogPosition.MiddleLeft:
                pos = new Vector2(minX - spacing - dialogHalfW, centerY);
                break;
            case DialogPosition.MiddleCenter:
                pos = new Vector2(centerX, centerY);
                break;
            case DialogPosition.MiddleRight:
                pos = new Vector2(maxX + spacing + dialogHalfW, centerY);
                break;
            case DialogPosition.BottomLeft:
                pos = new Vector2(minX + dialogHalfW, minY - spacing - dialogHalfH);
                break;
            case DialogPosition.BottomCenter:
                pos = new Vector2(centerX, minY - spacing - dialogHalfH);
                break;
            case DialogPosition.BottomRight:
                pos = new Vector2(maxX - dialogHalfW, minY - spacing - dialogHalfH);
                break;
            default: // Auto: above/below, whichever side of the screen has room
                bool targetInUpperHalf = centerY > 0;
                float y = targetInUpperHalf
                    ? minY - spacing - dialogHalfH   // place below target
                    : maxY + spacing + dialogHalfH;  // place above target
                pos = new Vector2(centerX, y);
                break;
        }

        return ClampToScreen(pos, dialogHalfW, dialogHalfH);
    }

    Vector2 ClampToScreen(Vector2 pos, float dialogHalfW, float dialogHalfH) {
        float halfW = canvasRect.rect.width / 2f;
        float halfH = canvasRect.rect.height / 2f;

        pos.x = Mathf.Clamp(pos.x, -halfW + dialogHalfW + screenEdgePadding, halfW - dialogHalfW - screenEdgePadding);
        pos.y = Mathf.Clamp(pos.y, -halfH + dialogHalfH + screenEdgePadding, halfH - dialogHalfH - screenEdgePadding);
        return pos;
    }

    void HandleClicked() {
        if (!root.activeSelf || !clickToContinue) return;

        var callback = onContinue;
        onContinue = null;
        callback?.Invoke();
    }
}