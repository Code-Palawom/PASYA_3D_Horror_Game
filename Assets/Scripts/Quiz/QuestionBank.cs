using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "QuestionBank", menuName = "Quiz/Question Bank")]
public class QuestionBank : ScriptableObject {
    [System.Serializable]
    public class QuestionData {
        public string question;
        public string[] options;
        public string correctAnswer;
    }

    public List<QuestionData> allQuestions;
}
