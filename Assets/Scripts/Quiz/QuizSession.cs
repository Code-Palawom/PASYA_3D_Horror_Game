using System;
using UnityEngine;

// Holds all state for one active quiz attempt.
public class QuizSession {
    public QuestionData question;
    public GameObject interactor;
    public Action onCorrect;
    public Action onWrong;
    public float startTime;
}