using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {
    private LevelLoader levelLoader;
    public Animator transition;

    [SerializeField] private AboutPanel about;
    [SerializeField] private PlayPanel play;

    private void Start() {
        levelLoader = gameObject.AddComponent<LevelLoader>();
    }

    public void Play() {
        play.OpenPlayPanel();
        StartCoroutine(levelLoader.LoadLevel(transition, "Map1"));
    }

    public void OpenCharacterPanel() {
        Debug.Log("Pressed Character!");
    }

    public void OpenSettingsPanel() {
        Debug.Log("Pressed Settings!");
    }

    public void OpenAboutPanel() {
        about.OpenAboutPanel();
    }

    public void CloseAboutPanel() {
        about.CloseAboutPanel();
    }

    public void ExitButton() {
        print("EXIT");
        Application.Quit();
    }
}
