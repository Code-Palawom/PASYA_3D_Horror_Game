using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {
    private LevelLoader levelLoader;
    public Animator transition;

    private void Start() {
        levelLoader = gameObject.AddComponent<LevelLoader>();
    }

    public void Play() {
        StartCoroutine(levelLoader.LoadLevel(transition, "Map1"));
    }

    public void SettingsButton() {
        Debug.Log("Pressed Settings!");
    }

    public void ExitButton() {
        print("EXIT");
        Application.Quit();
    }
}
