using System;
using UnityEngine;

public class QuizManager : MonoBehaviour {
    public static QuizManager Instance { get; private set; }

    //[SerializeField] QuizCanvasController canvasController;
    [SerializeField] ScoreManager scoreManager;

    private QuizSession _session;
    private bool _isActive;

    void Awake() => Instance = this;

    // Entry point — called by NetworkedQuizGate
    public void AskQuestion(NetworkedQuizGate gate, GameObject interactor, Action onCorrect, Action onWrong) {
        if (_isActive) {
            Debug.LogWarning("[QuizManager] Already showing a question.");
            return;
        }

        QuestionData question = gate.GetQuestion();

        if (question == null) {
            Debug.LogError($"[QuizManager] Gate '{gate.name}' returned null question.");
            return;
        }

        _session = new QuizSession {
            question = question,
            interactor = interactor,
            onCorrect = onCorrect,
            onWrong = onWrong,
            startTime = Time.time
        };

        _isActive = true;
        ResolveSession(true); // For testing, immediately resolve as correct. Remove this line in production.
        //GameManager.Instance.SetPlayerInputEnabled(false);

        // Canvas now uses Action<QuizAnswer> unified callback
        //canvasController.ShowQuestion(question, OnAnswerReceived);
    }

    // Called by canvas for all question types
    void OnAnswerReceived(QuizAnswer answer) {
        if (!_isActive) return;

        bool isCorrect = AnswerEvaluator.Evaluate(_session.question, answer);
        float timeTaken = Time.time - _session.startTime;

        if (isCorrect) {
            int score = CalculateScore(_session.question, timeTaken);
            scoreManager.AddScore(score);
        }

        //canvasController.ShowFeedback(isCorrect, afterFeedback: () => ResolveSession(isCorrect));
    }

    // Timer expired — empty answer (always wrong)
    public void OnTimerExpired() {
        if (!_isActive) return;
        // Empty answer evaluates as wrong for all types
        OnAnswerReceived(new QuizAnswer());
    }

    // Finalize session
    void ResolveSession(bool isCorrect) {
        //canvasController.Hide();
        //GameManager.Instance.SetPlayerInputEnabled(true);
        _isActive = false;

        if (isCorrect) _session.onCorrect?.Invoke();
        else _session.onWrong?.Invoke();

        _session = null;
    }

    // Score: base + speed bonus × difficulty multiplier
    int CalculateScore(QuestionData question, float timeTaken) {
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