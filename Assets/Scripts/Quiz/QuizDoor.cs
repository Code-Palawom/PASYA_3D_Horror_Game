using UnityEngine;

public class QuizDoor : MonoBehaviour {
    public Vector3 openRotation = new Vector3(0, 90, 0);
    public float openSpeed = 2.0f;
    private bool isOpening = false;

    void Update() {
        if(isOpening){
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, 
                Quaternion.Euler(openRotation), 
                Time.deltaTime * openSpeed
            );
        }
    }

    public void OpenDoor() {
        isOpening = true;
        Debug.Log("Door is opening!");
    }
}
