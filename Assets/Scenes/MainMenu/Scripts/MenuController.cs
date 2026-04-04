using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

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
    [SerializeField] private ActiveMenu genderButton;
    [SerializeField] private ActiveMenu headButton;
    [SerializeField] private ActiveMenu bodyButton;
    [SerializeField] private ActiveMenu pantsButton;
    [SerializeField] private ActiveMenu shoesButton;
    [SerializeField] private ActiveMenu applyButton;
    [SerializeField] private ActiveMenu cancelButton;

    [Header("Character Selections")]
    [SerializeField] private ActiveMenu characterSelection;

    [Header("Panels")]
    [SerializeField] private PlayPanel play;
    [SerializeField] private CanvasGroup main;
    [SerializeField] private CanvasGroup character;
    [SerializeField] private AboutPanel about;

    [Header("Sounds")]
    [SerializeField] private AudioSource buttonClickSound;

    [Header("Character Manager")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private CharacterManager characterManager;

    private void Start() {
        levelLoader = gameObject.AddComponent<LevelLoader>();
    }

    public void Play() {
        buttonClickSound.Play();
        play.OpenPlayPanel();
        StartCoroutine(levelLoader.LoadLevel(transition, "Map1"));
    }

    public void OpenCharacterPanel() {
        buttonClickSound.Play();
        cam.Priority = 0;
        characterCam.Priority = 10;

        main.interactable = false;
        main.blocksRaycasts = false;
        character.interactable = true;
        character.blocksRaycasts = true;

        characterManager.CustomizeCharacter();

        playButton.HideBtn();
        multiplayerButton.HideBtn();
        characterButton.HideBtn();
        settingsButton.HideBtn();
        aboutButton.HideBtn();
        exitButton.HideBtn();

        characterSelection.ShowBtn();
        cosmeticsButton.ShowBtn();
        genderButton.ShowBtn();
        headButton.ShowBtn();
        bodyButton.ShowBtn();
        pantsButton.ShowBtn();
        shoesButton.ShowBtn();
        applyButton.ShowBtn();
        cancelButton.ShowBtn();
        Debug.Log("Pressed Character!");
    }

    public void CloseCharacterPanel(bool saveCharacter) {
        buttonClickSound.Play();
        cam.Priority = 10;
        characterCam.Priority = 0;

        main.interactable = true;
        main.blocksRaycasts = true;
        character.interactable = false;
        character.blocksRaycasts = false;

        if(saveCharacter) {
            characterManager.SetCharacter();
        }else{
            characterManager.RevertCharacter();
        }

        cosmeticsButton.HideBtn();
        genderButton.HideBtn();
        headButton.HideBtn();
        bodyButton.HideBtn();
        pantsButton.HideBtn();
        shoesButton.HideBtn();
        applyButton.HideBtn();
        cancelButton.HideBtn();

        characterSelection.HideBtn();
        playButton.ShowBtn();
        multiplayerButton.ShowBtn();
        characterButton.ShowBtn();
        settingsButton.ShowBtn();
        aboutButton.ShowBtn();
        exitButton.ShowBtn();
        Debug.Log("Pressed Character!");
    }

    public void OpenSettingsPanel() {
        buttonClickSound.Play();
        Debug.Log("Pressed Settings!");
    }

    public void CloseSettingsPanel() {
        buttonClickSound.Play();
        Debug.Log("Pressed Settings!");
    }

    public void OpenAboutPanel() {
        buttonClickSound.Play();
        about.OpenAboutPanel();
    }

    public void CloseAboutPanel() {
        buttonClickSound.Play();
        about.CloseAboutPanel();
    }

    public void ExitButton() {
        buttonClickSound.Play();
        print("EXIT");
        Application.Quit();
    }
}
