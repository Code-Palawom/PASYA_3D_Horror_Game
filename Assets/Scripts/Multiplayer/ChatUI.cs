using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PrimeTween;

// NOTE: chatUI's RectTransform pivot must be set to (1, 1) in the Inspector
// for the "scale from top-right" effect to work — the panel should already
// be positioned with its top-right corner anchored where you want it to
// stay fixed while the rest scales in/out from that corner.
public class ChatUI : NetworkBehaviour {
    [SerializeField] CanvasGroup chatUI;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] Transform messageContainer;
    [SerializeField] ChatMessageItemUI messagePrefab;
    [SerializeField] TMP_InputField inputField;
    [SerializeField] Button sendButton;

#if UNITY_EDITOR || UNITY_STANDALONE
    [SerializeField] Image chatIcon;
#endif

    [Header("Input")]
    [SerializeField] private InputAction openChat;

    [Header("Toggle Animation")]
    [Tooltip("chatUI's RectTransform pivot must be (1, 1) in the Inspector so it scales " +
             "inward from its top-right corner instead of its center.")]
    [SerializeField] private float toggleInDuration = 0.3f;
    [SerializeField] private float toggleOutDuration = 0.2f;

    [Header("Notification Badge")]
    [Tooltip("Small circle/pill GameObject. Should live OUTSIDE the chatUI CanvasGroup hierarchy " +
             "(e.g. as a sibling attached to chatIcon) so it stays visible while the chat panel " +
             "itself is scaled/faded to zero when closed.")]
    [SerializeField] private GameObject notificationBadge;
    [SerializeField] private TMP_Text notificationBadgeText;
    [SerializeField] private float badgePopDuration = 0.25f;
    [SerializeField] private float badgePopScale = 1.25f;

    [Header("Position Follow")]
    [Tooltip("The RectTransform of the icon the chat panel should snap next to each time it opens. " +
             "Default attach point is the icon's bottom-left corner (panel sits below-left of the icon); " +
             "if that would push the panel off-screen, the attach corner flips horizontally and/or " +
             "vertically (e.g. bottom-right, top-left, top-right) so it stays fully on screen.")]
    [SerializeField] private RectTransform followIcon;
    [SerializeField] private Canvas parentCanvas;

    // ─── Keyboard Avoidance (mobile) ────────────────────────────
    // A separate overlay row that sits above the on-screen keyboard, shown only while this
    // ChatUI's own `inputField` is the field that opened the keyboard. The chat panel itself
    // never moves — the overlay's own input field becomes the active typing field instead,
    // mirroring/replacing the real one for the duration the keyboard is up.
    //
    // The target's anchors (and its horizontal anchoredPosition/offsets) are never touched —
    // whatever full-width stretch you've authored in the Inspector stays exactly as-is. The
    // only thing this code moves is anchoredPosition.y, to sit the row's bottom edge flush
    // against the top of the keyboard.
    [Header("Keyboard Avoider — Overlay Row")]
    [Tooltip("RectTransform of the overlay row that should appear above the keyboard while " +
             "this ChatUI's inputField is focused. Its anchors/horizontal layout are left exactly " +
             "as authored — only its vertical anchoredPosition is adjusted.")]
    [SerializeField] private RectTransform keyboardAvoiderTarget;

    [Header("Keyboard Avoider — Overlay Input Field")]
    [Tooltip("The input field living inside the overlay row. It's activated and takes over typing " +
             "while the keyboard is shown, then hands text back to the real inputField once closed.")]
    [SerializeField] private TMP_InputField overlayInputField;
    [Tooltip("Optional. Send button that lives next to the overlay input field.")]
    [SerializeField] private Button overlaySendButton;

    private bool _kaInitialized = false;
    private bool _kaWasKeyboardVisible = false;
    private RectTransform _kaCanvasRect;
    private Vector2 _kaDefaultAnchoredPosition;

    private bool _kaShowOverlay;
    private bool _kaDebugKeyboardVisible;
    private float _kaDebugKeyboardHeightPx;
    private float _kaDebugKbHeightInCanvas;
    private float _kaDebugCanvasHeight;

#if UNITY_EDITOR
    [Header("Keyboard Avoider — Editor Testing (Play Mode)")]
    [SerializeField] private Key kaToggleFakeKeyboardKey = Key.K;
    [SerializeField] private float kaFakeKeyboardHeight = 300f;
    private bool _kaFakeKeyboardVisible = false;
#endif

    private RectTransform _chatRect;
    private RectTransform _badgeRect;
    private Tween _scaleTween;
    private Tween _fadeTween;
    private Tween _badgePopTween;

    private readonly List<ChatMessageItemUI> _spawnedItems = new();
    public bool isChatActive = false;
    private bool isTyping = false;

    private int unreadCount = 0;

    public override void OnNetworkSpawn() {
        if (!IsOwner) return;

        sendButton.onClick.AddListener(OnSend);
        inputField.onSubmit.AddListener(_ => OnSend());

        if (overlayInputField != null) {
            overlayInputField.onSubmit.AddListener(_ => OnSend());
        }
        if (overlaySendButton != null) {
            overlaySendButton.onClick.AddListener(OnSend);
        }

        // Overlay row starts hidden — only shown while the keyboard is up for this field.
        if (keyboardAvoiderTarget != null) keyboardAvoiderTarget.gameObject.SetActive(false);

#if UNITY_EDITOR || UNITY_STANDALONE
        inputField.onSelect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(false));
        inputField.onDeselect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(true));
#endif

        openChat.performed += _ => ToggleChat();
        openChat.Enable();

        // Snap to the closed state instantly on spawn — no pop-in animation
        // the moment the player joins, only on subsequent toggles.
        _chatRect = chatUI.GetComponent<RectTransform>();
        _chatRect.localScale = Vector3.zero;
        chatUI.alpha = 0f;
        chatUI.interactable = false;
        chatUI.blocksRaycasts = false;

        // Badge starts hidden, no unread messages yet.
        unreadCount = 0;
        if (notificationBadge != null) {
            _badgeRect = notificationBadge.GetComponent<RectTransform>();
            notificationBadge.SetActive(false);
        }

        if (ChatManager.Instance != null)
            InitChat();
        else
            StartCoroutine(WaitForChatManager());
    }

    public override void OnNetworkDespawn() {
        if (!IsOwner) return;

        sendButton.onClick.RemoveListener(OnSend);
        inputField.onSelect.RemoveAllListeners();
        inputField.onDeselect.RemoveAllListeners();

        if (overlayInputField != null) overlayInputField.onSubmit.RemoveAllListeners();
        if (overlaySendButton != null) overlaySendButton.onClick.RemoveListener(OnSend);

        if (ChatManager.Instance != null) {
            ChatManager.Instance.OnMessageReceived.RemoveListener(OnNewMessage);
            ChatManager.Instance.OnChatCleared.RemoveListener(OnChatCleared);
        }

        openChat.Disable();
        openChat.Dispose();

        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();
        if (_badgePopTween.isAlive) _badgePopTween.Stop();
    }

    // ─── Update ───────────────────────────────────────────────

    void Update() {
        if (!IsOwner) return;
        UpdateKeyboardAvoider();
    }

    // ─── Init ─────────────────────────────────────────────────

    IEnumerator WaitForChatManager() {
        yield return new WaitUntil(() => ChatManager.Instance != null);
        InitChat();
    }

    void InitChat() {
        // Host already has Messages; clients receive history via SyncHistoryRpc
        if (IsServer) {
            foreach (var msg in ChatManager.Instance.Messages)
                SpawnMessageItem(msg);
        }

        ChatManager.Instance.OnMessageReceived.AddListener(OnNewMessage);
        ChatManager.Instance.OnChatCleared.AddListener(OnChatCleared);
    }

    // ─── Callbacks ────────────────────────────────────────────
    void ToggleChat() {
        if (GameManager.Instance != null && GameManager.Instance.IsControlFrozen) return;

        isChatActive = !isChatActive;

        if (isChatActive && followIcon != null) SnapToIcon(); // snap into place each time it opens

        AnimateChatPanel(isChatActive);

        if (isChatActive) {
            unreadCount = 0;
            UpdateNotificationBadge(animatePop: false);
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        var c = chatIcon.color;
        if (isChatActive) {
            chatIcon.color = new Color(c.r, c.g, c.b, 0.50f);
        } else {
            chatIcon.color = new Color(c.r, c.g, c.b, 0.25f);
        }
#endif
    }

    private void AnimateChatPanel(bool open) {
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();

        // Block interaction immediately either way — closing shouldn't be
        // clickable mid-shrink, and opening only becomes interactable once
        // the pop settles (below).
        chatUI.interactable = false;

        if (open) {
            chatUI.blocksRaycasts = true; // let it start catching input right away as it grows in
            _scaleTween = Tween.Scale(_chatRect, endValue: Vector3.one, duration: toggleInDuration, ease: Ease.OutBack)
                .OnComplete(() => chatUI.interactable = true);
            _fadeTween = Tween.Alpha(chatUI, endValue: 1f, duration: toggleInDuration * 0.6f);
        } else {
            _scaleTween = Tween.Scale(_chatRect, endValue: Vector3.zero, duration: toggleOutDuration, ease: Ease.OutBack)
                .OnComplete(() => chatUI.blocksRaycasts = false);
            _fadeTween = Tween.Alpha(chatUI, endValue: 0f, duration: toggleOutDuration * 0.8f);
        }
    }

    void ToggleTyping() {
        if (GameManager.Instance != null && GameManager.Instance.IsControlFrozen) return;

        if (isTyping)
            inputField.ActivateInputField();
        else
            inputField.DeactivateInputField();

        isTyping = !isTyping;
    }

    void OnSend() {
        // While the keyboard overlay is up, the overlay field holds the live text;
        // otherwise the real inputField does.
        bool usingOverlay = _kaWasKeyboardVisible && overlayInputField != null;
        string textToSend = usingOverlay ? overlayInputField.text : inputField.text;

        if (string.IsNullOrWhiteSpace(textToSend)) return;
        ChatManager.Instance.SendChatMessage(textToSend);

        inputField.text = "";
        if (overlayInputField != null) overlayInputField.text = "";

        if (usingOverlay)
            overlayInputField.ActivateInputField();
        else
            inputField.ActivateInputField();
    }

    void OnNewMessage(ChatMessage msg) {
        SpawnMessageItem(msg);

        if (!isChatActive) {
            unreadCount++;
            UpdateNotificationBadge(animatePop: true);
        }
    }

    void OnChatCleared() {
        foreach (var item in _spawnedItems) Destroy(item.gameObject);
        _spawnedItems.Clear();
    }

    // ─── Helpers ──────────────────────────────────────────────

    void SpawnMessageItem(ChatMessage msg) {
        var item = Instantiate(messagePrefab, messageContainer);
        item.Setup(msg);
        _spawnedItems.Add(item);

        StartCoroutine(ScrollToBottom());
    }

    IEnumerator ScrollToBottom() {
        yield return null;
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageContainer.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    void UpdateNotificationBadge(bool animatePop) {
        if (notificationBadge == null) return;

        bool hasUnread = unreadCount > 0;

        if (hasUnread) {
            notificationBadgeText.text = unreadCount > 99 ? $"{99}+" : unreadCount.ToString();

            bool wasInactive = !notificationBadge.activeSelf;
            notificationBadge.SetActive(true);

            if (animatePop && _badgeRect != null) {
                if (_badgePopTween.isAlive) _badgePopTween.Stop();

                if (wasInactive) {
                    // First unread message: pop in from zero.
                    _badgeRect.localScale = Vector3.zero;
                    _badgePopTween = Tween.Scale(_badgeRect, endValue: Vector3.one, duration: badgePopDuration, ease: Ease.OutBack);
                } else {
                    // Subsequent unread messages: quick punch to draw the eye, then settle back to 1.
                    _badgePopTween = Tween.Scale(_badgeRect, endValue: Vector3.one * badgePopScale, duration: badgePopDuration * 0.4f, ease: Ease.OutQuad)
                        .OnComplete(() => Tween.Scale(_badgeRect, endValue: Vector3.one, duration: badgePopDuration * 0.6f, ease: Ease.OutBack));
                }
            }
        } else {
            if (_badgePopTween.isAlive) _badgePopTween.Stop();
            notificationBadge.SetActive(false);
            if (_badgeRect != null) _badgeRect.localScale = Vector3.one; // reset so next pop-in starts clean
        }
    }

    // ─── Position Follow ──────────────────────────────────────

    void SnapToIcon() {
        if (parentCanvas == null) return;

        var canvasRect = (RectTransform)parentCanvas.transform;
        var cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        RectTransform panelParent = (RectTransform)_chatRect.parent;

        // World corners of the icon: [0]=bottom-left, [1]=top-left, [2]=top-right, [3]=bottom-right
        Vector3[] iconCorners = new Vector3[4];
        followIcon.GetWorldCorners(iconCorners);

        Vector2 iconBL = WorldToLocalInParent(iconCorners[0], cam, panelParent);
        Vector2 iconTR = WorldToLocalInParent(iconCorners[2], cam, panelParent);

        float iconLeftX = iconBL.x;
        float iconRightX = iconTR.x;
        float iconBottomY = iconBL.y;
        float iconTopY = iconTR.y;

        Vector2 size = _chatRect.rect.size;

        // Canvas bounds in panelParent local space (assumes panelParent shares the canvas rect, centered pivot).
        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;
        float minX = -halfW;
        float maxX = halfW;
        float minY = -halfH;
        float maxY = halfH;

        // chatUI's pivot is (1,1), so anchoredPosition is always the panel's top-right corner,
        // and the panel body extends left/down from that point.
        //
        // Default (below-left of icon): anchorX = icon's left edge, anchorY = icon's bottom edge.
        // If the panel's opposite edge would land off-screen, flip that axis to attach to the
        // icon's other edge instead (right edge for X, top edge for Y).

        bool leftFits = (iconLeftX - size.x) >= minX;
        float anchorX = leftFits ? iconLeftX : iconRightX + size.x;
        // If flipping to the right side would itself overflow the right bound, fall back to
        // whichever side clips less by clamping afterward (ClampToCanvas handles that safety net).
        if (!leftFits && anchorX > maxX) anchorX = iconLeftX;

        bool belowFits = (iconBottomY - size.y) >= minY;
        float anchorY = belowFits ? iconBottomY : iconTopY + size.y;
        if (!belowFits && anchorY > maxY) anchorY = iconBottomY;

        _chatRect.anchoredPosition = new Vector2(anchorX, anchorY);
        ClampToCanvas(canvasRect); // final safety net for icons near a corner or a screen smaller than the panel
    }

    Vector2 WorldToLocalInParent(Vector3 worldPoint, Camera cam, RectTransform parent) {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPoint);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }

    void ClampToCanvas(RectTransform canvasRect) {
        Vector2 size = _chatRect.rect.size;
        Vector2 pivot = _chatRect.pivot;
        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;

        Vector2 pos = _chatRect.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -halfW + size.x * pivot.x, halfW - size.x * (1f - pivot.x));
        pos.y = Mathf.Clamp(pos.y, -halfH + size.y * pivot.y, halfH - size.y * (1f - pivot.y));
        _chatRect.anchoredPosition = pos;
    }

    // ─── Keyboard Avoider (overlay input field above the on-screen keyboard) ─────────

    void InitKeyboardAvoider() {
        if (_kaInitialized || keyboardAvoiderTarget == null) return;

        _kaCanvasRect = parentCanvas != null ? (RectTransform)parentCanvas.transform : null;

        // Only the vertical anchoredPosition is ever touched, so that's all we need to remember
        // to restore it once the keyboard closes. Anchors and horizontal layout are never changed.
        _kaDefaultAnchoredPosition = keyboardAvoiderTarget.anchoredPosition;

        _kaInitialized = true;
    }

    void UpdateKeyboardAvoider() {
        if (keyboardAvoiderTarget == null) return;
        InitKeyboardAvoider();

        bool keyboardVisible;
        float keyboardHeightPx;

#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current[kaToggleFakeKeyboardKey].wasPressedThisFrame)
            _kaFakeKeyboardVisible = !_kaFakeKeyboardVisible;
        keyboardVisible = _kaFakeKeyboardVisible;
        keyboardHeightPx = _kaFakeKeyboardVisible ? kaFakeKeyboardHeight : 0f;
#elif UNITY_ANDROID
        // TouchScreenKeyboard.area.height is unreliable on Android (often 0 or full-screen
        // depending on OEM keyboard/Android version). Use the decor view visible frame instead.
        keyboardHeightPx = AndroidKeyboardHeight.GetHeightPx();
        keyboardVisible = keyboardHeightPx > 0f;
#else
        keyboardVisible = TouchScreenKeyboard.visible;
        keyboardHeightPx = TouchScreenKeyboard.area.height;
#endif

        // This overlay only cares about the keyboard when it was THIS ChatUI's own inputField
        // (or the overlay field it hands off to) that's focused — ignore the keyboard being
        // open for any other reason.
        bool ownsFocus = inputField.isFocused || (overlayInputField != null && overlayInputField.isFocused);
        if (!ownsFocus) keyboardVisible = false;

        // Debug-overlay-only figure (canvas-relative estimate); NOT used for positioning anymore.
        float canvasHeight = _kaCanvasRect != null ? _kaCanvasRect.rect.height : Screen.height;
        float kbHeightInCanvas = (keyboardHeightPx / Screen.height) * canvasHeight;

        _kaDebugKeyboardVisible = keyboardVisible;
        _kaDebugKeyboardHeightPx = keyboardHeightPx;
        _kaDebugKbHeightInCanvas = kbHeightInCanvas;
        _kaDebugCanvasHeight = canvasHeight;

        if (keyboardVisible) {
            RectTransform targetParent = (RectTransform)keyboardAvoiderTarget.parent;
            Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            // Convert the keyboard's screen-space top edge into targetParent's local space, then
            // into a "distance from targetParent's bottom edge" figure. With the target anchored
            // at the bottom of its parent (as authored), that figure is exactly the anchoredPosition.y
            // that puts the row's bottom edge flush against the top of the keyboard.
            Vector2 screenPoint = new Vector2(Screen.width * 0.5f, keyboardHeightPx);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screenPoint, cam, out Vector2 localPoint);
            float distanceFromParentBottom = localPoint.y - targetParent.rect.yMin;

            Vector2 pos = keyboardAvoiderTarget.anchoredPosition;
            pos.y = distanceFromParentBottom + 40f;
            keyboardAvoiderTarget.anchoredPosition = pos; // x untouched — horizontal stretch stays exactly as authored
        } else if (_kaWasKeyboardVisible) {
            // Restore original vertical position (only needs doing once, right as it closes)
            keyboardAvoiderTarget.anchoredPosition = _kaDefaultAnchoredPosition;
        }

        if (keyboardVisible != _kaWasKeyboardVisible) {
            HandleKeyboardOverlayTransition(keyboardVisible);
            _kaWasKeyboardVisible = keyboardVisible;
        }
    }

    void HandleKeyboardOverlayTransition(bool keyboardVisible) {
        if (keyboardAvoiderTarget != null) keyboardAvoiderTarget.gameObject.SetActive(keyboardVisible);

        if (overlayInputField == null) return;

        if (keyboardVisible) {
            // Hand typing off to the overlay field: carry the current text and caret over, then focus it.
            overlayInputField.text = inputField.text;
            overlayInputField.ActivateInputField();
            overlayInputField.caretPosition = overlayInputField.text.Length;
        } else {
            // Hand text back to the real field and release the overlay's focus.
            inputField.text = overlayInputField.text;
            overlayInputField.DeactivateInputField();
        }
    }

    public void RefreshKeyboardAvoiderDebugMode() {
        if (SettingsManager.Instance == null) {
            Debug.LogWarning("[ChatUI/KeyboardAvoider] SettingsManager not ready yet.");
            return;
        }
        _kaShowOverlay = SettingsManager.Instance.Current.showDebugOverlay;
    }

    private void OnGUI() {
        if (!_kaShowOverlay) return;
        GUI.Label(new Rect(10, 110, 420, 20), $"[KB] Visible : {_kaDebugKeyboardVisible}");
        GUI.Label(new Rect(10, 130, 420, 20), $"[KB] Height px : {_kaDebugKeyboardHeightPx:F0}  (Screen.height={Screen.height})");
        GUI.Label(new Rect(10, 150, 420, 20), $"[KB] Height in canvas : {_kaDebugKbHeightInCanvas:F1}  (canvasHeight={_kaDebugCanvasHeight:F0})");
    }
}
