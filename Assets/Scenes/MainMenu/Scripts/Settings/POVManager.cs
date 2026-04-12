using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class POVManager : MonoBehaviour {
    public SaveManager saveManager;

    [SerializeField] private Color selectedTextColor;
    [SerializeField] private Color selectedBtnColor;
    [SerializeField] private Button firstPersonBtn;
    [SerializeField] private Button thirdPersonBtn;
    private int currentPOV = 0;

    void Start() {
        currentPOV = int.Parse(saveManager.GetOneData("pov") ?? "0");
        CurrentPOV();
    }

    public void ChangePOV(int povId) {
        currentPOV = povId;
        saveManager.SaveOneData(currentPOV.ToString(), "pov");
        CurrentPOV();
    }

    private void CurrentPOV() {
        TextMeshProUGUI firstBtnText = firstPersonBtn.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI thirdBtnText = thirdPersonBtn.GetComponentInChildren<TextMeshProUGUI>();

        firstBtnText.color = Color.white;
        thirdBtnText.color = Color.white;

        Color c = Color.white;
        c.a = 0;

        firstPersonBtn.image.color = c;
        thirdPersonBtn.image.color = c;

        if(currentPOV == 0) {
            firstPersonBtn.image.color = selectedBtnColor;
            firstBtnText.color = selectedTextColor;
        }else{
            thirdPersonBtn.image.color = selectedBtnColor;
            thirdBtnText.color = selectedTextColor;
        }
    }
}

