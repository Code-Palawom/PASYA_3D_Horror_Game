using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ActiveSettings : MonoBehaviour {
    [SerializeField] private bool showDefault;

    private CanvasGroup setting;

    private void Start() {
        setting = GetComponent<CanvasGroup>();

        if(showDefault) ShowSelection();
    }

    public void ShowSelection() {
        setting.blocksRaycasts = true;
        setting.interactable = true;
        setting.alpha = 1f;
    }

    public void HideSelection() {
        setting.blocksRaycasts = false;
        setting.interactable = false;
        setting.alpha = 0f;
    }
}
