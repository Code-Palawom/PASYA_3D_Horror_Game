using TMPro;
using UnityEngine;

public class NameHandler : MonoBehaviour {
    public SaveManager saveManager;

    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text errorText;

    private void Start() {
        nameInputField.text = saveManager.GetOneData("playerName");
        errorText.gameObject.SetActive(false);
    }

    public void SetName() {
        string name = nameInputField.text;

        if(string.IsNullOrWhiteSpace(name)){
            ShowError("Name cannot be empty.");
            return;
        }

        if(name.Length < 3){
            ShowError("Name must be at least 3 characters.");
            return;
        }

        saveManager.SaveOneData(name, "playerName");
        errorText.gameObject.SetActive(false);
    }

    private void ShowError(string message) {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }
}
