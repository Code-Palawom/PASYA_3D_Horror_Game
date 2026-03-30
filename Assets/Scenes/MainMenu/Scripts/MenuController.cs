using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;

public class MenuController : MonoBehaviour {
    private LevelLoader levelLoader;
    public Animator transition;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private CinemachineCamera characterCam;

    [Header("Buttons")]
    [SerializeField] private ActiveMenu playButton;
    [SerializeField] private ActiveMenu multiplayerButton;
    [SerializeField] private ActiveMenu characterButton;
    [SerializeField] private ActiveMenu settingsButton;
    [SerializeField] private ActiveMenu aboutButton;
    [SerializeField] private ActiveMenu exitButton;

    [SerializeField] private ActiveMenu cosmeticsButton;
    [SerializeField] private ActiveMenu headButton;
    [SerializeField] private ActiveMenu bodyButton;
    [SerializeField] private ActiveMenu pantsButton;
    [SerializeField] private ActiveMenu shoesButton;
    [SerializeField] private ActiveMenu applyButton;
    [SerializeField] private ActiveMenu cancelButton;

    [Header("Panels")]
    [SerializeField] private PlayPanel play;
    [SerializeField] private CanvasGroup main;
    [SerializeField] private CanvasGroup character;
    [SerializeField] private AboutPanel about;

    private void Start() {
        levelLoader = gameObject.AddComponent<LevelLoader>();
    }

    public void Play() {
        play.OpenPlayPanel();
        StartCoroutine(levelLoader.LoadLevel(transition, "Map1"));
    }

    public void OpenCharacterPanel() {
        cam.Priority = 0;
        characterCam.Priority = 10;

        main.interactable = false;
        main.blocksRaycasts = false;
        character.interactable = true;
        character.blocksRaycasts = true;

        playButton.HideBtn();
        multiplayerButton.HideBtn();
        characterButton.HideBtn();
        settingsButton.HideBtn();
        aboutButton.HideBtn();
        exitButton.HideBtn();

        cosmeticsButton.ShowBtn();
        headButton.ShowBtn();
        bodyButton.ShowBtn();
        pantsButton.ShowBtn();
        shoesButton.ShowBtn();
        applyButton.ShowBtn();
        cancelButton.ShowBtn();
        Debug.Log("Pressed Character!");
    }

    public void CloseCharacterPanel() {
        cam.Priority = 10;
        characterCam.Priority = 0;

        main.interactable = true;
        main.blocksRaycasts = true;
        character.interactable = false;
        character.blocksRaycasts = false;

        cosmeticsButton.HideBtn();
        headButton.HideBtn();
        bodyButton.HideBtn();
        pantsButton.HideBtn();
        shoesButton.HideBtn();
        applyButton.HideBtn();
        cancelButton.HideBtn();

        playButton.ShowBtn();
        multiplayerButton.ShowBtn();
        characterButton.ShowBtn();
        settingsButton.ShowBtn();
        aboutButton.ShowBtn();
        exitButton.ShowBtn();
        Debug.Log("Pressed Character!");
    }

    public void OpenSettingsPanel() {
        Debug.Log("Pressed Settings!");
    }

    public void CloseSettingsPanel() {
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
