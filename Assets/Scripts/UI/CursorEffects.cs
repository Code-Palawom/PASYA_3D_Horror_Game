using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CursorEffects : MonoBehaviour {
    public float maxDistance = 3.0f;
    public Color hoverColor = Color.green;
    private Color normalColor;
    private Image cursorImage;

    void Start() {
        cursorImage = GetComponent<Image>();
        normalColor = cursorImage.color;
    }

    void Update() {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = transform.position;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        bool isCloseAndHovering = false;

        foreach (RaycastResult result in results) {
            if (result.gameObject.GetComponent<Button>() != null) {
                float dist = Vector3.Distance(Camera.main.transform.position, result.gameObject.transform.position);
                
                if (dist <= maxDistance) {
                    isCloseAndHovering = true;
                }
                break; 
            }
        }

        cursorImage.color = isCloseAndHovering ? hoverColor : normalColor;
    }
}