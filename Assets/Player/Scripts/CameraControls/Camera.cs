using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControls : MonoBehaviour {
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private float minDistance = 3f;

    private PlayerControls controls;
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbitalFollow;
    private Vector2 scrollDelta;

    private float targetZoom;
    private float currentZoom;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        controls = new PlayerControls();
        controls.Enable();
        controls.Camera.Zoom.performed += HandleMouseScroll;

        Cursor.lockState = CursorLockMode.Locked;

        cam = GetComponent<CinemachineCamera>();
        orbitalFollow = cam.GetComponent<CinemachineOrbitalFollow>();

        targetZoom = currentZoom = orbitalFollow.Radius;
    }

    // Update is called once per frame
    void Update() {
        if(scrollDelta.y != 0) {
            if(orbitalFollow != null) {
                targetZoom = Mathf.Clamp(orbitalFollow.Radius - scrollDelta.y * zoomSpeed, minDistance, maxDistance);
                scrollDelta = Vector2.zero;
            }
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * lerpSpeed);
        orbitalFollow.Radius = currentZoom;
    }

    void HandleMouseScroll(InputAction.CallbackContext context) {
        scrollDelta = context.ReadValue<Vector2>();
        Debug.Log($"Scroll value: {scrollDelta}");
    }
}
