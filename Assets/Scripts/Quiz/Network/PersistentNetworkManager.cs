using UnityEngine;

// Keeps the NetworkManager GameObject alive when loading
// from the Lobby scene into the Gameplay scene.
// Attach to the same GameObject as NetworkManager.
public class PersistentNetworkManager : MonoBehaviour {
    private static PersistentNetworkManager _instance;

    void Awake() {
        if (_instance != null && _instance != this) {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}