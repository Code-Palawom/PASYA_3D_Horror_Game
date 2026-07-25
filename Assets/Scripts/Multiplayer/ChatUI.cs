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
        inputField.onSelect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(false));
        inputField.onDeselect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(true));

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
        if (string.IsNullOrWhiteSpace(inputField.text)) return;
        ChatManager.Instance.SendChatMessage(inputField.text);
        inputField.text = "";
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
}