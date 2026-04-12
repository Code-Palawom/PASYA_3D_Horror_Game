using UnityEngine;

public class ActiveMode : MonoBehaviour {
    [SerializeField] private bool showDefault;

    private CanvasGroup mode;

    private void Start() {
        mode = GetComponent<CanvasGroup>();

        if(showDefault) ShowSelection();
    }

    public void ShowSelection() {
        mode.blocksRaycasts = true;
        mode.interactable = true;
        mode.alpha = 1f;
    }

    public void HideSelection() {
        mode.blocksRaycasts = false;
        mode.interactable = false;
        mode.alpha = 0f;
    }
}
