using System;
using UnityEngine;

public class QuizSession {
    public QuestionRuntime question;
    public GameObject interactor;
    public Action onCorrect;
    public Action onWrong;
    public float startTime;
}