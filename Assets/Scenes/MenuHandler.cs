using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuHandler : MonoBehaviour {
    private LevelLoader levelLoader;
    public Animator transition;

    public GameObject menuPanel;
    public GameObject menu;
    [SerializeField] private InputAction action;

    private bool isOpen = false;
    private Stack<GameObject> screenStack = new Stack<GameObject>();

    void Awake() {
        action.performed += (InputAction) => Toggle();
        action.Enable();
    }

    void Start() {
        levelLoader = gameObject.AddComponent<LevelLoader>();
    }

    void OnDestroy() {
        action.Disable();
        action.Dispose();
    }
    
    public void Toggle() {
        isOpen = !isOpen;
        if(screenStack.Count > 0){
            CloseTop();
        }else{
            menuPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            OpenMenu();
        }
    }

    void OpenMenu() {
        Push(menu);
        Time.timeScale = 0f;
    }

    public void Push(GameObject screen) {
        if(screenStack.Count > 0) screenStack.Peek().SetActive(false);

        screen.SetActive(true);
        screenStack.Push(screen);
    }

    public void CloseTop() {
        if(screenStack.Count == 0) return;

        screenStack.Pop().SetActive(false);

        if(screenStack.Count > 0){
            screenStack.Peek().SetActive(true);
        }else{
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void MainMenu() {
        Debug.Log("Main Menu");
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(levelLoader.LoadLevel(transition, "MainMenu"));
    }
}