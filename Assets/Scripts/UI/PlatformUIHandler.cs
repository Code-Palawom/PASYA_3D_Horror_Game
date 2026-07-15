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

        foreach (GameObject ui in mobileUIButtons) {
            if (ui != null) {
                ui.tag = isMobile ? "PlatformMobile" : "Untagged";
                ui.SetActive(isMobile);
            }
        }

        foreach (GameObject ui in pcUIButtons) {
            if (ui != null) {
                ui.tag = !isMobile ? "PlatformPC" : "Untagged";
                ui.SetActive(!isMobile);
            }
        }

        if (screenCursor != null) screenCursor.SetActive(!isMobile);
    }
}