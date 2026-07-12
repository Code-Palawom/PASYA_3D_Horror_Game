using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuizCanvasController : MonoBehaviour {
    [Header("Panel")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject chatUI;

    [Header("Root")]
    [SerializeField] GameObject quizPanel;
    //[SerializeField] Animator panelAnimator;

    [Header("Header")]
    [SerializeField] TMP_Text questionText;
    [SerializeField] TMP_Text difficultyBadge;
    [SerializeField] Image questionImage;
    [SerializeField] Color easyColor = Color.green;
    [SerializeField] Color mediumColor = Color.yellow;
    [SerializeField] Color hardColor = Color.red;

    [Header("Timer")]
    [SerializeField] TimerController timer;
    [SerializeField] Image timerBar;
    [SerializeField] TMP_Text timerLabel;     // numeric seconds e.g. "12.3s"

    [Header("Feedback")]
    [SerializeField] TMP_Text feedbackText;

    [Header("Multiple Choice")]
    [SerializeField] GameObject multipleChoicePanel;
    [SerializeField] AnswerButtonUI[] choiceButtons;

    [Header("True / False")]
    [SerializeField] GameObject trueFalsePanel;
    [SerializeField] TrueFalseButtonUI trueFalseButtons;

    [Header("Text Answer")]
    [SerializeField] GameObject textAnswerPanel;
    [SerializeField] TextAnswerInputUI textAnswerInput;
    
    private Action<QuizAnswer> _onAnswer;
    private bool _answered;

    // ─────────────────────────────────────────────────────────
    public void ShowQuestion(QuestionRuntime question, Action<QuizAnswer> onAnswer) {
        _onAnswer = onAnswer;
        _answered = false;

        mainPanel.SetActive(false);
        chatUI.SetActive(false);
        quizPanel.SetActive(true);
        feedbackText.gameObject.SetActive(false);
        //panelAnimator.SetTrigger("Open");

        SetupHeader(question);
        ActivatePanel(question);

        timer.OnTimeUp += OnTimerUp;
        timer.StartTimer(question.timeLimit);
    }
    
    public void ShowFeedback(bool isCorrect, Action afterFeedback) {
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = isCorrect ? "Correct!" : "Wrong!";
        feedbackText.color = isCorrect ? Color.green : Color.red;

        if (isCorrect) ScreenFlashController.Local?.FlashCorrect(); else ScreenFlashController.Local?.FlashWrong();
        ActionbarToastNotification.Instance.ShowLocalToast(feedbackText.text, isCorrect ? ToastType.Success : ToastType.Error);
        afterFeedback?.Invoke();
    }

    public void Hide() {
        // No-op if already hidden (guards against ForceClose on inactive panel)
        if (!quizPanel.activeSelf) return;

        // Clean up timer subscription before closing
        timer.StopTimer();
        timer.OnTimeUp -= OnTimerUp;

        //panelAnimator.SetTrigger("Close");
        quizPanel.SetActive(false);
        mainPanel.SetActive(true);
        chatUI.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────
    void SetupHeader(QuestionRuntime question) {
        questionText.text = question.questionText;

        difficultyBadge.text = question.difficulty.ToString().ToUpper();
        difficultyBadge.color = question.difficulty switch {
            QuestionDifficulty.Easy => easyColor,
            QuestionDifficulty.Medium => mediumColor,
            QuestionDifficulty.Hard => hardColor,
            _ => Color.white
        };

        // Question image not synced over network — hide for now
        if (questionImage != null)
            questionImage.gameObject.SetActive(false);
    }

    void ActivatePanel(QuestionRuntime question) {
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
                trueFalseButtons.Setup(i => SubmitIndex(i));
                break;
            case QuestionType.FillInTheBlank:
            case QuestionType.ShortAnswer:
                textAnswerPanel.SetActive(true);
                textAnswerInput.Setup(question.questionType, t => SubmitText(t));
                break;
        }
    }

    void SetupMultipleChoice(QuestionRuntime question) {
        var choices = question.GetChoices();

        // Build (originalIndex, text) pairs and shuffle display order
        var pairs = new System.Collections.Generic.List<(int originalIndex, string text)>();
        for (int i = 0; i < choices.Count; i++)
            pairs.Add((i, choices[i]));

        // Fisher-Yates shuffle
        for (int i = pairs.Count - 1; i > 0; i--) {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pairs[i], pairs[j]) = (pairs[j], pairs[i]);
        }

        string[] labels = { "A", "B", "C", "D" };

        for (int i = 0; i < choiceButtons.Length; i++) {
            bool hasChoice = i < pairs.Count;
            choiceButtons[i].gameObject.SetActive(hasChoice);
            if (hasChoice) {
                int originalIndex = pairs[i].originalIndex;
                string label = labels[i];
                string display = $"{label}.  {pairs[i].text}";
                choiceButtons[i].Setup(i, display, _ => SubmitIndex(originalIndex));
            }
        }
    }

    void SubmitIndex(int index) {
        if (_answered) return;
        _answered = true;
        timer.StopTimer();
        timer.OnTimeUp -= OnTimerUp;
        LockAll();
        _onAnswer?.Invoke(QuizAnswer.FromIndex(index));
    }

    void SubmitText(string text) {
        if (_answered) return;
        _answered = true;
        timer.StopTimer();
        timer.OnTimeUp -= OnTimerUp;
        LockAll();
        _onAnswer?.Invoke(QuizAnswer.FromText(text));
    }

    void OnTimerUp() {
        timer.OnTimeUp -= OnTimerUp;
        QuizManager.Instance.OnTimerExpired();
    }

    void LockAll() {
        foreach (var btn in choiceButtons) btn.SetInteractable(false);
        trueFalseButtons.SetInteractable(false);
        textAnswerInput.SetInteractable(false);
    }

    void Update() {
        if (quizPanel.activeSelf && timer != null && timer.TotalDuration > 0f) {
            timerBar.fillAmount = timer.TimeRemaining / timer.TotalDuration;

            if (timerLabel != null)
                timerLabel.text = $"{timer.TimeRemaining:F1}s";
        }
    }
}