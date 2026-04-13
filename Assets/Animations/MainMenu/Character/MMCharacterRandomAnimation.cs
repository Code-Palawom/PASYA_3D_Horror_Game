using System.Collections;
using UnityEngine;

public class MMCharacterRandomAnimation : MonoBehaviour {
    [Header("Random Animation Settings")]
    public float minTime = 5f;
    public float maxTime = 15f;

    private MMCharacterAnimation animation;

    void Start() {
        animation = GetComponent<MMCharacterAnimation>();
        StartCoroutine(RandomAnimation());
    }

    private IEnumerator RandomAnimation() {
        while(true){
            float waitTime = Random.Range(minTime, maxTime);
            Debug.Log(waitTime);
            yield return new WaitForSeconds(waitTime);

            Debug.Log("Triggered");
            animation.SetAnimationState("lookingOver");
        }
    }
}
