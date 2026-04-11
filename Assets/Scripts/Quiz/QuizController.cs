using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuizController : MonoBehaviour {
    public enum Difficulty { Easy, Medium, Hard }
    public Difficulty currentDifficulty;

    [Header("Banks")]
    public QuestionBank easyBank;
    public QuestionBank mediumBank;
    public QuestionBank hardBank;

    [Header("UI")]
    public TextMeshProUGUI questionText;
    public Button[] optionButtons;

    public QuizDoor connectedDoor;

    private QuestionBank.QuestionData selectedQuestion;
    private string[] letters = {"A", "B", "C", "D"};

    void Start(){
        QuestionBank bank = currentDifficulty == Difficulty.Easy ? easyBank : currentDifficulty == Difficulty.Medium ? mediumBank : hardBank;

        if(bank != null && bank.allQuestions.Count > 0){
            selectedQuestion = bank.allQuestions[Random.Range(0, bank.allQuestions.Count)];
            SetupUI();
        }
    }

    void SetupUI() {
        questionText.text = selectedQuestion.question;
        List<string> shuffled = selectedQuestion.options.ToList();
        
        for(int i = 0; i < shuffled.Count; i++){
            string temp = shuffled[i];
            int r = Random.Range(i, shuffled.Count);
            shuffled[i] = shuffled[r];
            shuffled[r] = temp;
        }

        for(int i = 0; i < optionButtons.Length; i++){
            int index = i; 
            string choice = shuffled[index];

            optionButtons[index].GetComponentInChildren<TextMeshProUGUI>().text = $"{letters[index]}. {choice}";
    
            optionButtons[index].onClick.RemoveAllListeners();
            optionButtons[index].onClick.AddListener(() => {
                if(choice == selectedQuestion.correctAnswer){
                    TextMeshProUGUI btnText = optionButtons[index].GetComponentInChildren<TextMeshProUGUI>();
            
                    connectedDoor.OpenDoor();
                    btnText.color = Color.green;
                    optionButtons[index].image.color = Color.white;

                    foreach (var btn in optionButtons) {
                        btn.interactable = false; 
                    }
                    Invoke("HideQuizObject", 1.5f);
                }else{
                    Color c = optionButtons[index].image.color;
                    c.a = 0f; 
                    optionButtons[index].image.color = c;
                    foreach (var btn in optionButtons) {
                        StartCoroutine(WrongAnswer(btn));
                    }
                }
            });
        }
    }

    IEnumerator WrongAnswer(Button btn) {
        btn.interactable = false;
        TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
        if(btnText == null) yield break;
        Color wrongColor = Color.red;
        Color normalColor = Color.white;
    
        float duration = 1.5f;
        float elapsed = 0f;

        btnText.color = wrongColor;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            btnText.color = Color.Lerp(wrongColor, normalColor, elapsed / duration);
            yield return null;
        }

        btnText.color = normalColor;
        btn.interactable = true;
    }

    void HideQuizObject() {
        gameObject.SetActive(false); 
    }
}
