using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatUI : NetworkBehaviour {
    [SerializeField] GameObject chatUI;
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

    private readonly List<ChatMessageItemUI> _spawnedItems = new();
    private bool isChatActive = false;
    private bool isTyping = false;

    public override void OnNetworkSpawn() {
        if (!IsOwner) return;

        sendButton.onClick.AddListener(OnSend);
        inputField.onSubmit.AddListener(_ => OnSend());
        inputField.onSelect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(true));
        inputField.onDeselect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(false));

        openChat.performed += _ => ToggleChat();
        openChat.Enable();

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
        chatUI.SetActive(isChatActive);

#if UNITY_EDITOR || UNITY_STANDALONE
        var c = chatIcon.color;
        if (isChatActive) {
            chatIcon.color = new Color(c.r, c.g, c.b, 0.50f);
        } else {
            chatIcon.color = new Color(c.r, c.g, c.b, 0.25f);
        }
#endif
    }

    void ToggleTyping() {
        if(GameManager.Instance != null && GameManager.Instance.IsControlFrozen) return;

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