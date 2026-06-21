using UnityEngine;

public class PlatformUIHandler : MonoBehaviour {
    [SerializeField] private GameObject[] mobileUIButtons;
    [SerializeField] private GameObject[] pcUIButtons;
    [SerializeField] private GameObject screenCursor;

#if UNITY_EDITOR
    [Header("Editor Only")]
    [SerializeField] private bool overrideMobilePlatform = false;
#endif

    void Start() {
        bool isMobile = Application.isMobilePlatform;

#if UNITY_EDITOR
        isMobile = overrideMobilePlatform;
#endif

        foreach (GameObject button in mobileUIButtons) {
            if (button != null) button.SetActive(isMobile);
        }

        foreach (GameObject button in pcUIButtons) {
            if (button != null) button.SetActive(!isMobile);
        }

        if (screenCursor != null) screenCursor.SetActive(!isMobile);
    }
}