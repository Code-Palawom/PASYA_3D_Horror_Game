using UnityEngine;

public class MusicManager : MonoBehaviour {
    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;

    void Start() {
        audioSource.loop = true;
        audioSource.Play();
    }
}