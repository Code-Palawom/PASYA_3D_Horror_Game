using System;
using UnityEngine;

// Central quiz session manager.

// The quiz canvas is no longer a single shared world-space object —
// each player has their own QuizCanvasController on their own PlayerCanvas
// (screen space). AskQuestion() looks up the canvas from the interactor
// GameObject directly, so the question is only ever visible to the
// player who triggered the interaction.
public class QuizManager : MonoBehaviour {
    public static QuizManager Instance { get; private set; }

    [SerializeField] ScoreManager scoreManager;

    public static event Action<NetworkedQuizGate> OnQuizStarted;
    public static event Action<bool> OnQuizEnded;

    private QuizSession _session;
    private NetworkedQuizGate _currentGate;
    private QuizCanvasController _activeCanvas;   // the interacting player's own canvas
    private bool _isActive;

    void Awake() => Instance = this;

    void Start() {
        if (scoreManager == null)
            Debug.LogError("[QuizManager] Score Manager is NOT assigned in the Inspector.", this);

        if (GameManager.Instance == null)
            Debug.LogError("[QuizManager] GameManager.Instance is null. Add GameManager to the scene.", this);
    }

    // ─────────────────────────────────────────────────────────
    public void AskQuestion(NetworkedQuizGate gate, GameObject interactor,
                        Action<QuizAnswer> onCorrect, Action<QuizAnswer> onWrong) {
        if (_isActive) { Debug.LogWarning("[QuizManager] Already active."); return; }

        // Find the quiz canvas that belongs to THIS interacting player's own UI.
        // Since OnInteract only ever fires on the local owner's client
        // (InteractionController disables itself for non-owners), this is
        // always the local player's canvas — never another player's.
        var canvas = interactor.GetComponentInChildren<QuizCanvasController>(true);
        if (canvas == null) {
            Debug.LogError($"[QuizManager] No QuizCanvasController found on interactor " +
                            $"'{interactor.name}'. Make sure it's on that player's PlayerCanvas.");
            return;
        }

        if (GameManager.Instance == null) {
            Debug.LogError("[QuizManager] Cannot show question — GameManager.Instance is null.");
            return;
        }

        var question = gate.GetQuestion();
        if (question == null) { Debug.LogError("[QuizManager] Null question."); return; }

        _activeCanvas = canvas;
        _currentGate = gate;
        _session = new QuizSession {
            question = question,
            interactor = interactor,
            onCorrect = onCorrect,
            onWrong = onWrong,
            startTime = Time.time
        };

        _isActive = true;
        GameManager.Instance.SetPlayerInputEnabled(false);
        _activeCanvas.ShowQuestion(question, OnAnswerReceived);

        OnQuizStarted?.Invoke(gate);
    }

    void OnAnswerReceived(QuizAnswer answer) {
        if (!_isActive) return;

        bool isCorrect = AnswerEvaluator.Evaluate(_session.question, answer);
        float timeTaken = Time.time - _session.startTime;
        int score = isCorrect ? CalculateScore(_session.question, timeTaken) : 0;

        if (isCorrect && scoreManager != null)
            scoreManager.AddScore(score);

        // Report to server for end-game stats
        GameSessionManager.Instance.RecordAnswerRpc(isCorrect, score);
        string toastMsg = !string.IsNullOrWhiteSpace(_session.question.description)
            ? _session.question.description
            : (isCorrect ? "Correct!" : "Wrong!");

        _activeCanvas.ShowFeedback(isCorrect, toastMsg, () => ResolveSession(isCorrect, answer));
    }

    public void OnTimerExpired() {
        if (!_isActive) return;
        OnAnswerReceived(new QuizAnswer());
    }

    void ResolveSession(bool isCorrect, QuizAnswer answer) {
        _activeCanvas.Hide();

        GameManager.Instance.SetPlayerInputEnabled(true);

        _isActive = false;
        OnQuizEnded?.Invoke(isCorrect);

        if (isCorrect) _session.onCorrect?.Invoke(answer);
        else _session.onWrong?.Invoke(answer);

        _session = null;
        _currentGate = null;
        _activeCanvas = null;
    }

    // Force-closes any active quiz session (e.g. gate cooldown fires mid-answer).
    public void ForceClose() {
        if (!_isActive) return;
        _activeCanvas?.Hide();
        GameManager.Instance.SetPlayerInputEnabled(true);
        _isActive = false;
        _session = null;
        _currentGate = null;
        _activeCanvas = null;
    }

    // Force-closes the active session as a wrong answer without invoking
    // the gate's onWrong callback — used when the player is jumpscared
    // mid-answer. Scoring/stats are recorded as a miss, but the gate's
    // wrong-answer side effects, chat message, and cooldown are skipped
    // since the jumpscare itself is already the consequence.
    public void ForceCloseAsWrong(GameObject interactor) {
        if (!_isActive || _session == null || _session.interactor != interactor) return;

        GameSessionManager.Instance?.RecordAnswerRpc(false, 0);

        _activeCanvas?.Hide();
        GameManager.Instance.SetPlayerInputEnabled(true);

        _isActive = false;
        OnQuizEnded?.Invoke(false);

        // Deliberately NOT calling _session.onWrong — that's the gate's
        // side-effect-applying callback (ApplyWrongSideEffectsToAllRpc,
        // cooldown, chat message). Jumpscare is a separate outcome path.
        _session = null;
        _currentGate = null;
        _activeCanvas = null;
    }

    int CalculateScore(QuestionRuntime question, float timeTaken) {
        float timeRatio = Mathf.Clamp01(1f - (timeTaken / question.timeLimit));
        int timeBonus = Mathf.RoundToInt(question.pointValue * 0.5f * timeRatio);
        int multiplier = question.difficulty switch {
            QuestionDifficulty.Easy => 1,
            QuestionDifficulty.Medium => 2,
            QuestionDifficulty.Hard => 3,
            _ => 1
        };
        return (question.pointValue + timeBonus) * multiplier;
    }
}