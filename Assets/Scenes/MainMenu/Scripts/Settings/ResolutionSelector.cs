using TMPro;
using UnityEngine;

public class ResolutionSelector : MonoBehaviour {
    public SaveManager saveManager;

    [SerializeField] private TMP_Text displayLabel;
    private string[] qualityLevels = { "Low", "Medium", "High"};
    private int currentIndex = 1;

    void Start() {
        currentIndex = int.Parse(saveManager.GetOneData("resolution") ?? "1");
        UpdateResolution();
    }

    public void ChangeNext() {
        currentIndex++;
        if (currentIndex >= qualityLevels.Length) currentIndex = 0;
        saveManager.SaveOneData(currentIndex.ToString(), "resolution");
        UpdateResolution();
    }

    public void ChangePrevious() {
        currentIndex--;
        if (currentIndex < 0) currentIndex = qualityLevels.Length - 1;
        saveManager.SaveOneData(currentIndex.ToString(), "resolution");
        UpdateResolution();
    }

    private void UpdateResolution() {
        string selectedRes = qualityLevels[currentIndex];
        displayLabel.text = selectedRes;

        //QualitySettings.SetQualityLevel(currentIndex, true);
    }
}
