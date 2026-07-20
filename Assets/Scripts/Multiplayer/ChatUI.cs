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

    private RectTransform _chatRect;
    private Tween _scaleTween;
    private Tween _fadeTween;

    private readonly List<ChatMessageItemUI> _spawnedItems = new();
    public bool isChatActive = false;
    private bool isTyping = false;

    public override void OnNetworkSpawn() {
        if (!IsOwner) return;

        sendButton.onClick.AddListener(OnSend);
        inputField.onSubmit.AddListener(_ => OnSend());
        inputField.onSelect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(true));
        inputField.onDeselect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(false));

        openChat.performed += _ => ToggleChat();
        openChat.Enable();

        // Snap to the closed state instantly on spawn — no pop-in animation
        // the moment the player joins, only on subsequent toggles.
        _chatRect = chatUI.GetComponent<RectTransform>();
        _chatRect.localScale = Vector3.zero;
        chatUI.alpha = 0f;
        chatUI.interactable = false;
        chatUI.blocksRaycasts = false;

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

    void OnNewMessage(ChatMessage msg) => SpawnMessageItem(msg);

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
}