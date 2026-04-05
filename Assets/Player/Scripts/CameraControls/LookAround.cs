using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class LookAround : MonoBehaviour {
    private CinemachineInputAxisController inputController;
    private HashSet<int> uiFingers = new HashSet<int>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        inputController = GetComponent<CinemachineInputAxisController>();
    }

    // Update is called once per frame
    void Update() {
        HandleRotationBlocking();
    }

    private void HandleRotationBlocking() {
        if(inputController == null) return;

        bool isAnyValidFingerLooking = false;
        var activeTouches = Touch.activeTouches; 

        for(int i = 0; i < activeTouches.Count; i++){
            Touch t = activeTouches[i];
            if(t.phase == UnityEngine.InputSystem.TouchPhase.Began){
                if (EventSystem.current.IsPointerOverGameObject(t.touchId)){
                    uiFingers.Add(t.touchId);
                    //Debug.Log($"Finger {t.touchId} started on UI. Ignoring.");
                }
            }

            if(t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled){
                uiFingers.Remove(t.touchId);
                continue;
            }

            if(!uiFingers.Contains(t.touchId)){
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved || t.phase == UnityEngine.InputSystem.TouchPhase.Stationary) {
                    isAnyValidFingerLooking = true;
                }
            }
        }

        if(Input.touchCount == 0 && Mouse.current != null){
            if(Mouse.current.leftButton.isPressed && !EventSystem.current.IsPointerOverGameObject()){
                isAnyValidFingerLooking = true;
            }
        }

        inputController.enabled = isAnyValidFingerLooking;
    }

    void OnEnable() {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable() {
        EnhancedTouchSupport.Disable();
    }
}
