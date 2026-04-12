using UnityEngine;

public class TempJoinButton : MonoBehaviour {
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        gameObject.SetActive(false);
        if(Application.dataPath.Contains("_clone")){
            gameObject.SetActive(true);
        }

        string[] args = System.Environment.GetCommandLineArgs();
        foreach(string arg in args){
            if(arg.Contains("AppPlayer") || arg.Contains("playerIndex") || arg.Contains("-clone") || arg.Contains("Player 2")){
                gameObject.SetActive(true);
            }
        }
    }
}
