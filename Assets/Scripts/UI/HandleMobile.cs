using UnityEngine;

public class HandleMobile : MonoBehaviour {
    [SerializeField] private GameObject[] mobileUIButtons;
    [SerializeField] private GameObject screenCursor;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        if (!Application.isMobilePlatform) {
            foreach (GameObject button in mobileUIButtons) {
                if (button != null) button.SetActive(false);
            }

            screenCursor.SetActive(true);
        }
    }
}
