using System;
using UnityEngine;

public class QuizSession {
    public QuestionRuntime question;
    public GameObject interactor;
    public Action<QuizAnswer> onCorrect;
    public Action<QuizAnswer> onWrong;
    public float startTime;
}