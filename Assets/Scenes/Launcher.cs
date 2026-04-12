using Unity.Netcode;
using UnityEngine;

public class Launcher : MonoBehaviour {
    void Start(){
        if(NetworkManager.Singleton == null){
            Debug.LogError("No NetworkManager found! Is it in the Menu scene?");
            return;
        }

        switch(GameModeSettings.CurrentMode){
            case GameModeSettings.GameMode.SinglePlayer:
                NetworkManager.Singleton.StartHost();
                Debug.Log("Started Single Player Mode");
                break;

            case GameModeSettings.GameMode.HostMultiplayer:
                NetworkManager.Singleton.StartHost();
                Debug.Log("Started Multiplayer Host");
                break;

            case GameModeSettings.GameMode.JoinMultiplayer:
                NetworkManager.Singleton.StartClient();
                Debug.Log("Started Multiplayer Client");
                break;
        }
    }
}