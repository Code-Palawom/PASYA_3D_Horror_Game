using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ActiveSelection : MonoBehaviour {
    [SerializeField] private bool showDefault;

    private CanvasGroup selection;

    private void Start() {
        selection = GetComponent<CanvasGroup>();

        if(showDefault) ShowSelection();
    }

    public void ShowSelection() {
        selection.blocksRaycasts = true;
        selection.interactable = true;
        selection.alpha = 1f;
    }

    public void HideSelection() {
        selection.blocksRaycasts = false;
        selection.interactable = false;
        selection.alpha = 0f;
    }
}
