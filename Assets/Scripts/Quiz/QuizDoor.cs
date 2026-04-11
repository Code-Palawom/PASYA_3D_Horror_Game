using UnityEngine;

public class QuizDoor : MonoBehaviour
{
    public Vector3 openRotation = new Vector3(0, 90, 0); // Where the door should rotate to
    public float openSpeed = 2.0f;
    private bool isOpening = false;

    void Update()
    {
        if (isOpening)
        {
            // Smoothly rotate the door toward the open rotation
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, 
                Quaternion.Euler(openRotation), 
                Time.deltaTime * openSpeed
            );
        }
    }

    public void OpenDoor()
    {
        isOpening = true;
        Debug.Log("Door is opening!");
    }
}
