using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

public class CameraCursor : MonoBehaviour {
    public EventSystem eventSystem;
    public Transform character;
    public float maxClickDistance = 1.5f;
    public Color hoverColor = Color.white;
    public Color textHoverColor = Color.black;

    private Button lastHoveredButton;
    private Color originalColor;
    private Color originalTextColor;
    private TextMeshProUGUI lastButtonText;

    void Update() {
        #if UNITY_EDITOR || UNITY_STANDALONE
            HandleCursorAndHover(); 
        #endif
    }

    private void HandleCursorAndHover() {
        PointerEventData hoverData = new PointerEventData(eventSystem);
        hoverData.position = new Vector2(Screen.width / 2, Screen.height / 2);

        List<RaycastResult> hoverResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(hoverData, hoverResults);

        Button currentButton = null;

        foreach(RaycastResult result in hoverResults){
            float dist = Vector3.Distance(character.position, result.gameObject.transform.position);
            if (dist <= maxClickDistance){
                Button btn = result.gameObject.GetComponent<Button>();
        
                if(btn != null && btn.interactable){
                    currentButton = btn;
                    break; 
                }
            }
        }

        if(currentButton != lastHoveredButton) {
            if(lastHoveredButton != null && lastHoveredButton.interactable){
                if(lastButtonText != null){
                    lastHoveredButton.image.color = originalColor;
                    lastButtonText.color = originalTextColor;
                }
            }

            if(currentButton != null){
                lastButtonText = currentButton.GetComponentInChildren<TextMeshProUGUI>();
                if(lastButtonText != null){
                    originalColor = currentButton.image.color;
                    originalTextColor = lastButtonText.color;

                    currentButton.image.color = hoverColor;
                    lastButtonText.color = textHoverColor;
                }
            }

            lastHoveredButton = currentButton;
        }

        if(Pointer.current != null && Pointer.current.press.wasPressedThisFrame){
            if(currentButton != null){
                ExecuteEvents.Execute(currentButton.gameObject, hoverData, ExecuteEvents.pointerClickHandler);
            }
        }
    }
}
