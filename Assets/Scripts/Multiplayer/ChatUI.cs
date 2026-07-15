using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatUI : NetworkBehaviour {
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] Transform messageContainer;
    [SerializeField] ChatMessageItemUI messagePrefab;
    [SerializeField] TMP_InputField inputField;
    [SerializeField] Button sendButton;

    [Header("Input")]
    [SerializeField] private InputAction _typeInChat;
    [SerializeField] private InputAction _exitTypeMode;

    private readonly List<ChatMessageItemUI> _spawnedItems = new();

    public override void OnNetworkSpawn() {
        if (!IsOwner) return;

        sendButton.onClick.AddListener(OnSend);
        inputField.onSubmit.AddListener(_ => OnSend());
        inputField.onSelect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(true));
        inputField.onDeselect.AddListener(_ => GameManager.Instance.SetPlayerInputEnabled(false));

        _typeInChat.performed += _ => WillTypeInChat(true); ;
        _typeInChat.Enable();
        _exitTypeMode.performed += _ => WillTypeInChat(false);
        _exitTypeMode.Enable();

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

        _typeInChat.Disable();
        _typeInChat.Dispose();
        _exitTypeMode.Disable();
        _exitTypeMode.Dispose();
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

    void WillTypeInChat(bool willType) {
        if(GameManager.Instance != null && GameManager.Instance.IsControlFrozen) return;

        if (willType)
            inputField.ActivateInputField();
        else
            inputField.DeactivateInputField();
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