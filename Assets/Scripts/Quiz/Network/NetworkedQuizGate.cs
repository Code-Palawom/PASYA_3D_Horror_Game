using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Networked quiz gate. Uses NGO 2.x unified [Rpc] attribute.
// - Question assigned randomly on server spawn, synced to all clients.
// - Unlock state via NetworkVariable.
// - Side effects broadcast to all clients via Rpc(SendTo.Everyone).

public class NetworkedQuizGate : NetworkBehaviour {
    [Header("Quiz")]
    [SerializeField] QuizSetData quizSet;
    [SerializeField] QuestionDifficulty difficulty = QuestionDifficulty.Easy;
    [SerializeField] bool oneTimeUnlock = true;

    [Header("Side Effects")]
    [SerializeField] SideEffectRegistry registry;
    [SerializeField] List<QuizSideEffect> sideEffects;

    // Networked state
    private NetworkVariable<bool> _unlocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _questionIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsUnlocked => _unlocked.Value;

    // Assign question on server spawn
    public override void OnNetworkSpawn() {
        if (IsServer) {
            int idx = quizSet.GetRandomIndexByDifficulty(difficulty);
            _questionIndex.Value = idx;
        }
    }

    public QuestionData GetQuestion() => quizSet.GetByIndex(_questionIndex.Value);

    // Called by interactables
    public void Attempt(GameObject interactor, Action onSuccess, Action onFail) {
        if (_unlocked.Value) { onSuccess?.Invoke(); return; }

        QuizManager.Instance.AskQuestion(
            this,
            interactor,
            onCorrect: () => {
                RequestUnlockRpc();
                onSuccess?.Invoke();
            },
            onWrong: () => {
                if (interactor.TryGetComponent<NetworkObject>(out var netObj))
                    RequestSideEffectsRpc(netObj.NetworkObjectId);

                onFail?.Invoke();
            }
        );
    }

    // Unlock — any client can request, only server writes
    [Rpc(SendTo.Server)]
    void RequestUnlockRpc() {
        if (oneTimeUnlock) _unlocked.Value = true;
    }

    // Side effects — client requests server, server tells everyone
    [Rpc(SendTo.Server)]
    void RequestSideEffectsRpc(ulong playerNetworkObjectId) {
        int[] indices = sideEffects
            .Select(e => registry.IndexOf(e))
            .Where(i => i >= 0)
            .ToArray();

        ApplySideEffectsRpc(playerNetworkObjectId, indices);
    }

    [Rpc(SendTo.Everyone)]
    void ApplySideEffectsRpc(ulong playerNetworkObjectId, int[] effectIndices) {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(playerNetworkObjectId, out var netObj)) return;

        GameObject player = netObj.gameObject;
        bool isLocalPlayer = netObj.IsOwner;

        foreach (int idx in effectIndices) {
            QuizSideEffect effect = registry.GetByIndex(idx);
            if (effect != null)
                StartCoroutine(effect.ApplyWithDuration(player, isLocalPlayer));
        }
    }
}