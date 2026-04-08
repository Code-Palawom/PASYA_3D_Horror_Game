using UnityEngine;

public class HandleMobile : MonoBehaviour {
    [SerializeField] private GameObject mobileUIButtons;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        #if UNITY_ANDROID || UNITY_IOS
            mobileUIButtons.SetActive(true);
        #endif

        // For development otherwise remove
        if(UnityEngine.Device.Application.isMobilePlatform) {
            mobileUIButtons.SetActive(true);
        }
    }
}
