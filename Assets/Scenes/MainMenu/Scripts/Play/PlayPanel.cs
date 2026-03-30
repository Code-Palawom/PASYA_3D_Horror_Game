using UnityEngine;

public class PlayPanel : MonoBehaviour {
    [SerializeField] private GameObject panel;

    void Start() {
        panel.SetActive(false);
    }

    public void OpenPlayPanel() {
        panel.SetActive(true);
    }
}
