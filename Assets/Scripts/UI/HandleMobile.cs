using UnityEngine;

public class HandleMobile : MonoBehaviour {
    [SerializeField] private GameObject mobileUIButtons;
    [SerializeField] private GameObject screenCursor;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        #if UNITY_ANDROID || UNITY_IOS
            mobileUIButtons.SetActive(true);
            screenCursor.SetActive(false);
        #endif

        // For development otherwise remove
        if(UnityEngine.Device.Application.isMobilePlatform) {
            mobileUIButtons.SetActive(true);
            screenCursor.SetActive(false);
        }
    }
}
