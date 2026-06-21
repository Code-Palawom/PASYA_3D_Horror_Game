using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DisconnectNotice : MonoBehaviour {
    [SerializeField] GameObject noticePanel;

    void Start() {
        noticePanel.SetActive(false);

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    void OnDestroy() {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
    }

    void OnClientDisconnect(ulong clientId) {
        // On a client, this fires with the server's clientId (0) when host drops
        if (NetworkManager.Singleton.IsServer) return;
        if (clientId != NetworkManager.ServerClientId) return;

        noticePanel.SetActive(true);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }

    public void OnConfirm() {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("MainMenu");
    }
}