using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XInput;

public class LookAround : MonoBehaviour {
    private CinemachineInputAxisController inputController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        inputController = GetComponent<CinemachineInputAxisController>();
    }

    // Update is called once per frame
    void Update() {
        HandleRotationBlocking();
    }

    private void HandleRotationBlocking() {
        if (inputController == null) return;

        bool isFingerLooking = false;

        if(Input.touchCount > 0){
            for (int i = 0; i < Input.touchCount; i++) {
                Touch touch = Input.GetTouch(i);

                if (!EventSystem.current.IsPointerOverGameObject(touch.fingerId)) {
                    if (touch.phase == UnityEngine.TouchPhase.Moved || touch.phase == UnityEngine.TouchPhase.Stationary) {
                        isFingerLooking = true;
                        break; 
                    }
                }
            }
        }else{
            if (!EventSystem.current.IsPointerOverGameObject()) {
                isFingerLooking = true; 
            }
        }

        inputController.enabled = isFingerLooking;
    }
}
