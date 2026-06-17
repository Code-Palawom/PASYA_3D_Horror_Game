using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// World-space quiz canvas controller.
// Switches panels based on QuestionType.
// On wrong answer: feedback shown, canvas hides. No correct answer revealed.
public class QuizCanvasController : MonoBehaviour {
    [Header("Root Panel")]
    [SerializeField] GameObject quizPanel;
    [SerializeField] Animator panelAnimator;

    [Header("Question Header")]
    [SerializeField] TMP_Text questionText;
    [SerializeField] TMP_Text difficultyBadge;
    [SerializeField] Image questionImage;
    [SerializeField] Color easyColor = Color.green;
    [SerializeField] Color mediumColor = Color.yellow;
    [SerializeField] Color hardColor = Color.red;

    [Header("Timer")]
    [SerializeField] TimerController timer;
    [SerializeField] Image timerBar;

    [Header("Feedback")]
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] float feedbackDuration = 1.5f;

    // Per-type panels
    [Header("Multiple Choice Panel")]
    [SerializeField] GameObject multipleChoicePanel;
    [SerializeField] AnswerButtonUI[] choiceButtons;    // 4 buttons (A B C D)

    [Header("True / False Panel")]
    [SerializeField] GameObject trueFalsePanel;
    [SerializeField] TrueFalseButtonUI trueFalseButtons;

    [Header("Text Answer Panel (Fill-in / Short)")]
    [SerializeField] GameObject textAnswerPanel;
    [SerializeField] TextAnswerInputUI textAnswerInput;

    private Action<QuizAnswer> _onAnswerSelected;
    private bool _answered;

    // Public API

    public void ShowQuestion(QuestionData question, Action<QuizAnswer> onAnswerSelected) {
        _onAnswerSelected = onAnswerSelected;
        _answered = false;

        quizPanel.SetActive(true);
        feedbackText.gameObject.SetActive(false);
        panelAnimator?.SetTrigger("Open");

        SetupHeader(question);
        ActivatePanel(question);

        timer.OnTimeUp += OnTimerUp;
        timer.StartTimer(question.timeLimit);

        FaceCamera();
    }

    public void ShowFeedback(bool isCorrect, Action afterFeedback) {
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = isCorrect ? "✓ Correct!" : "✗ Wrong!";
        feedbackText.color = isCorrect ? Color.green : Color.red;
        StartCoroutine(FeedbackRoutine(afterFeedback));
    }

    public void Hide() {
        panelAnimator?.SetTrigger("Close");
        StartCoroutine(HideAfterAnimation());
    }

    // Header (shared across all types)

    void SetupHeader(QuestionData question) {
        questionText.text = question.questionText;

        // Difficulty badge
        difficultyBadge.text = question.difficulty.ToString().ToUpper();
        difficultyBadge.color = question.difficulty switch {
            QuestionDifficulty.Easy => easyColor,
            QuestionDifficulty.Medium => mediumColor,
            QuestionDifficulty.Hard => hardColor,
            _ => Color.white
        };

        // Optional image
        bool hasImage = question.questionImage != null;
        questionImage.gameObject.SetActive(hasImage);
        if (hasImage) questionImage.sprite = question.questionImage;
    }

    // Panel switching

    void ActivatePanel(QuestionData question) {
        multipleChoicePanel.SetActive(false);
        trueFalsePanel.SetActive(false);
        textAnswerPanel.SetActive(false);

        switch (question.questionType) {
            case QuestionType.MultipleChoice:
                multipleChoicePanel.SetActive(true);
                SetupMultipleChoice(question);
                break;

            case QuestionType.TrueOrFalse:
                trueFalsePanel.SetActive(true);
                SetupTrueFalse();
                break;

            case QuestionType.FillInTheBlank:
            case QuestionType.ShortAnswer:
                textAnswerPanel.SetActive(true);
                SetupTextAnswer(question.questionType);
                break;
        }
    }

    // Per-type setup

    void SetupMultipleChoice(QuestionData question) {
        string[] choices = question.GetChoices();
        for (int i = 0; i < choiceButtons.Length; i++) {
            bool hasChoice = i < choices.Length;
            choiceButtons[i].gameObject.SetActive(hasChoice);
            if (hasChoice) {
                int captured = i;
                choiceButtons[i].Setup(i, choices[i], index => {
                    SubmitIndexAnswer(index);
                });
            }
        }
    }

    void SetupTrueFalse() {
        trueFalseButtons.Setup(index => SubmitIndexAnswer(index));
    }

    void SetupTextAnswer(QuestionType type) {
        textAnswerInput.Setup(type, text => SubmitTextAnswer(text));
    }

    // Answer submission

    void SubmitIndexAnswer(int index) {
        if (_answered) return;
        _answered = true;

        timer.StopTimer();
        timer.OnTimeUp -= OnTimerUp;

        LockAllInputs();
        _onAnswerSelected?.Invoke(QuizAnswer.FromIndex(index));
    }

    void SubmitTextAnswer(string text) {
        if (_answered) return;
        _answered = true;

        timer.StopTimer();
        timer.OnTimeUp -= OnTimerUp;

        LockAllInputs();
        _onAnswerSelected?.Invoke(QuizAnswer.FromText(text));
    }

    void OnTimerUp() {
        timer.OnTimeUp -= OnTimerUp;
        QuizManager.Instance.OnTimerExpired();
    }

    void LockAllInputs() {
        foreach (var btn in choiceButtons) btn.SetInteractable(false);
        trueFalseButtons.SetInteractable(false);
        // TextAnswerInputUI locks itself after submit
    }

    // Utilities

    IEnumerator FeedbackRoutine(Action after) {
        yield return new WaitForSeconds(feedbackDuration);
        after?.Invoke();
    }

    IEnumerator HideAfterAnimation() {
        yield return new WaitForSeconds(0.3f);
        quizPanel.SetActive(false);
    }

    void FaceCamera() {
        if (Camera.main == null) return;
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 180f, 0);
    }

    void Update() {
        if (quizPanel.activeSelf && timer != null && timer.TotalDuration > 0f)
            timerBar.fillAmount = timer.TimeRemaining / timer.TotalDuration;
    }
}