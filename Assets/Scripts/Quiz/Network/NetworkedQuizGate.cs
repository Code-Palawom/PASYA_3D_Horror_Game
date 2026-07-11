using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Networked quiz gate.
//
// - Tracks ALL currently interacting players via NetworkList
// - On wrong answer: side effects applied to every interacting player
// - Cooldown after wrong answer, visible to all
// - Interacting player can allow others to join
// - Optionally gated by an InteractionRequirements component (key items,
//   other doors unlocked, etc) checked BEFORE the quiz even starts
public class NetworkedQuizGate : NetworkBehaviour, IInteractable, IUnlockable {
    [Header("Quiz")]
    [SerializeField] QuestionDifficulty difficulty = QuestionDifficulty.Easy;
    [SerializeField] bool oneTimeUnlock = true;
    [SerializeField] float wrongAnswerCooldown = 10f;

    [Header("Side Effects (Wrong Answer — applied to ALL interacting players)")]
    [SerializeField] SideEffectRegistry registry;
    [SerializeField] List<QuizSideEffect> wrongSideEffects;

    // Optional — if present on this GameObject, checked before Attempt() runs.
    private InteractionRequirements _requirements;

    // ── Networked state ───────────────────────────────────────
    private NetworkVariable<bool> _unlocked = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<NetworkedQuestionData> _questionData = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // All players currently interacting with this gate
    private NetworkList<ulong> _interactingPlayers;

    // Allow other players to also interact while someone is answering
    private NetworkVariable<bool> _allowOthers = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Server time when cooldown ends (0 = no cooldown active)
    private NetworkVariable<double> _cooldownEndTime = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Public state ──────────────────────────────────────────
    public bool IsUnlocked => _unlocked.Value;
    public bool HasInteractingPlayer => _interactingPlayers.Count > 0;
    public bool AllowOthers => _allowOthers.Value;
    public bool IsCooldownActive => _cooldownEndTime.Value > 0 &&
                                          NetworkManager.ServerTime.Time < _cooldownEndTime.Value;
    public float WrongAnswerCooldown => wrongAnswerCooldown;
    public double CooldownRemaining => IsCooldownActive
                                          ? _cooldownEndTime.Value - NetworkManager.ServerTime.Time
                                          : 0;

    private QuestionRuntime _cachedQuestion;

    // ── IInteractable ─────────────────────────────────────────
    public string InteractPrompt => "Open Gate";
    public bool IsLocked => (HasInteractingPlayer && !_allowOthers.Value) || IsCooldownActive;

    public void OnInteract(GameObject interactor) {
        if (_requirements != null && _requirements.HasRequirements
            && !_requirements.CheckAll(interactor, out string failMsg)) {
            PlayerInteractionUI.ShowMessageForPlayer(interactor, failMsg);
            return;
        }

        Attempt(interactor, onSuccess: () => {
            _requirements?.NotifyConsumed(interactor);
            OpenGate();
        }, onFail: null);
    }

    public void OnFocus(PlayerInteractionUI ui) {
        if (_unlocked.Value) {
            ui.Hide();
            return;
        }

        if (IsCooldownActive) {
            ui.ShowWithCooldown(CooldownRemaining, wrongAnswerCooldown);
            return;
        }

        if (HasInteractingPlayer && !_allowOthers.Value) {
            ui.Show("Someone is answering...", "");
            return;
        }

        ui.Show(InteractPrompt);
    }

    // Override in subclass or wire via UnityEvent for door animation/trigger
    protected virtual void OpenGate() { }

    // Sets the quiz difficulty for THIS instance before it spawns. Must be
    // called after Instantiate() but before NetworkObject.Spawn() — the
    // question is claimed inside OnNetworkSpawn using whatever difficulty
    // is set at that point. Calling this after spawn has no effect (the
    // question is already claimed) and logs a warning instead of silently
    // doing nothing.
    public void SetDifficulty(QuestionDifficulty newDifficulty) {
        if (IsSpawned) {
            Debug.LogWarning($"[NetworkedQuizGate] '{name}': SetDifficulty called after spawn — " +
                              "too late, the question was already claimed. Call this before Spawn().");
            return;
        }
        difficulty = newDifficulty;
    }

    // ─────────────────────────────────────────────────────────
    // NetworkList must be created in Awake
    // ─────────────────────────────────────────────────────────
    void Awake() {
        _interactingPlayers = new NetworkList<ulong>(
            new List<ulong>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        _requirements = GetComponent<InteractionRequirements>();
    }

    // ─────────────────────────────────────────────────────────
    // Server-only: held for answer evaluation without re-fetching
    private QuestionRuntime _runtimeQuestion;

    public override void OnNetworkSpawn() {
        if (IsServer) {
            // Guard: coordinator must be initialized first
            if (QuizAssignmentCoordinator.Instance == null) {
                Debug.LogError("[NetworkedQuizGate] QuizAssignmentCoordinator not ready. " +
                               "Check scene execution order.");
                return;
            }

            if (QuizAssignmentCoordinator.Instance.ClaimQuestion(
                    difficulty, out var runtime)) {
                _questionData.Value = NetworkedQuestionData.FromRuntime(runtime);
                _runtimeQuestion = runtime; // server holds for answer evaluation
            } else {
                Debug.LogError($"[NetworkedQuizGate] '{name}' could not claim a question.");
            }
        }

        _questionData.OnValueChanged += (_, _) => _cachedQuestion = null;
    }

    public override void OnNetworkDespawn() {
        _questionData.OnValueChanged -= (_, _) => _cachedQuestion = null;
    }

    // ─────────────────────────────────────────────────────────
    public QuestionRuntime GetQuestion() {
        if (_cachedQuestion == null)
            _cachedQuestion = _questionData.Value.ToRuntime();
        return _cachedQuestion;
    }

    // ─────────────────────────────────────────────────────────
    // Attempt — called by interactables
    // ─────────────────────────────────────────────────────────
    public void Attempt(GameObject interactor, Action onSuccess, Action onFail) {
        if (_unlocked.Value) { onSuccess?.Invoke(); return; }

        // ── Cooldown check ────────────────────────────────────
        if (IsCooldownActive) {
            string msg = $"[Gate '{name}'] Cooldown active — {CooldownRemaining:F1}s remaining.";
            Debug.Log(msg);
            PlayerInteractionUI.ShowMessageForPlayer(interactor, msg);
            return;
        }

        // ── Lock check ────────────────────────────────────────
        if (HasInteractingPlayer && !_allowOthers.Value) {
            ulong interactorId = interactor.GetComponent<NetworkObject>()?.NetworkObjectId
                                 ?? ulong.MaxValue;

            // Allow if this player is already in the list
            if (!_interactingPlayers.Contains(interactorId)) {
                string msg = $"[Gate '{name}'] Another player is already interacting.";
                Debug.Log(msg);
                PlayerInteractionUI.ShowMessageForPlayer(interactor, msg);
                return;
            }
        }

        // ── Add this player to interacting list ───────────────
        if (interactor.TryGetComponent<NetworkObject>(out var netObj))
            AddInteractingPlayerRpc(netObj.NetworkObjectId);

        // ── Ask question ──────────────────────────────────────
        QuizManager.Instance.AskQuestion(
            this,
            interactor,
            onCorrect: () => {
                RequestUnlockRpc();

                var q = GetQuestion();
                string playerName = ResolveLocalPlayerName(interactor);
                ChatManager.Instance.SendSystemMessage($"{playerName} answered correctly!\nQ: \"{q.questionText}\"\nA: \"{q.correctAnswer}\"");

                // Remove only this player from the list on correct
                if (interactor.TryGetComponent<NetworkObject>(out var n))
                    RemoveInteractingPlayerRpc(n.NetworkObjectId);

                onSuccess?.Invoke();
            },
            onWrong: () => {
                // Apply side effects to ALL currently interacting players
                ApplyWrongSideEffectsToAllRpc(BuildIndices(wrongSideEffects));

                var q = GetQuestion();
                string playerName = ResolveLocalPlayerName(interactor);
                ChatManager.Instance.SendSystemMessage($"{playerName} answered incorrectly.\nQ: \"{q.questionText}\"");

                // Start cooldown — clears entire interacting list when done
                StartCooldownRpc();

                onFail?.Invoke();
            }
        );
    }

    // ─────────────────────────────────────────────────────────
    // Allow Others — called by InteractionStatusUI
    // ─────────────────────────────────────────────────────────
    public void RequestSetAllowOthers(bool allow) => SetAllowOthersRpc(allow);

    // ─────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────

    [Rpc(SendTo.Server)]
    void AddInteractingPlayerRpc(ulong id) {
        Debug.Log("[RPC][Server] AddInteractingPlayer");
        if (!_interactingPlayers.Contains(id))
            _interactingPlayers.Add(id);
    }

    [Rpc(SendTo.Server)]
    void RemoveInteractingPlayerRpc(ulong id) {
        Debug.Log("[RPC][Server] RemoveInteractingPlayer");
        if (_interactingPlayers.Contains(id))
            _interactingPlayers.Remove(id);

        // Reset AllowOthers when list is empty
        if (_interactingPlayers.Count == 0)
            _allowOthers.Value = false;
    }

    [Rpc(SendTo.Server)]
    void RequestUnlockRpc() {
        Debug.Log("[RPC][Server] RequestUnlock");
        if (oneTimeUnlock) _unlocked.Value = true;
    }

    [Rpc(SendTo.Server)]
    void SetAllowOthersRpc(bool allow) {
        Debug.Log("[RPC][Server] SetAllowOthers");
        _allowOthers.Value = allow;
    }
    //void SetAllowOthersRpc(bool allow) => _allowOthers.Value = allow;

    [Rpc(SendTo.Server)]
    void StartCooldownRpc() {
        Debug.Log("[RPC][Server] StartCooldown");
        _cooldownEndTime.Value = NetworkManager.ServerTime.Time + wrongAnswerCooldown;
        StartCoroutine(CooldownRoutine());
    }

    IEnumerator CooldownRoutine() {
        yield return new WaitForSeconds(wrongAnswerCooldown);
        _cooldownEndTime.Value = 0;

        // Force-close any open quiz session mid-answer
        QuizManager.Instance.ForceClose();

        // Clear ALL interacting players after cooldown
        _interactingPlayers.Clear();
        _allowOthers.Value = false;
    }

    // ─────────────────────────────────────────────────────────
    // Apply wrong side effects to EVERY player in _interactingPlayers
    // ─────────────────────────────────────────────────────────

    [Rpc(SendTo.Server)]
    void ApplyWrongSideEffectsToAllRpc(int[] indices) {
        Debug.Log("[RPC][Server] ApplyWrongSideEffects");
        if (indices.Length == 0) return;

        // Collect all NetworkObjectIds currently in the list
        ulong[] ids = new ulong[_interactingPlayers.Count];
        for (int i = 0; i < _interactingPlayers.Count; i++)
            ids[i] = _interactingPlayers[i];

        BroadcastSideEffectsToAllRpc(ids, indices);
    }

    [Rpc(SendTo.Everyone)]
    void BroadcastSideEffectsToAllRpc(ulong[] playerIds, int[] effectIndices) {
        Debug.Log("[RPC][Everyone] BroadcastSideEffectsToAll");
        foreach (ulong playerId in playerIds) {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(playerId, out var netObj)) continue;

            GameObject player = netObj.gameObject;
            bool isLocalPlayer = netObj.IsOwner;

            foreach (int idx in effectIndices) {
                var effect = registry.GetByIndex(idx);
                if (effect != null)
                    StartCoroutine(effect.ApplyWithDuration(player, isLocalPlayer));
            }
        }
    }

    string ResolveLocalPlayerName(GameObject interactor) {
        if (interactor.TryGetComponent<NetworkObject>(out var netObj)
            && GameSessionManager.Instance != null) {
            foreach (var player in GameSessionManager.Instance.Players)
                if (player.ClientId == netObj.OwnerClientId)
                    return player.PlayerName.ToString();
        }
        return "Unknown";
    }

    // ─────────────────────────────────────────────────────────
    int[] BuildIndices(List<QuizSideEffect> effects) =>
        effects?
            .Select(e => registry.IndexOf(e))
            .Where(i => i >= 0)
            .ToArray() ?? Array.Empty<int>();
}