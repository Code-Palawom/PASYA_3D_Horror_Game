using UnityEngine;

public class CustomizationController : MonoBehaviour {
    [SerializeField] private ActiveSelection head;
    [SerializeField] private ActiveSelection body;

    public void OpenBodySelection() {
        head.HideSelection();
        body.ShowSelection();
    }

    public void OpenHeadSelection() {
        head.ShowSelection();
        body.HideSelection();
    }
}
