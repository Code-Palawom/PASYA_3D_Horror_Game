using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {
    public void ExitButton() {
        print("EXIT");
        Application.Quit();
    }

    public void Play() {
        SceneManager.LoadScene("Map1");
    }
}
