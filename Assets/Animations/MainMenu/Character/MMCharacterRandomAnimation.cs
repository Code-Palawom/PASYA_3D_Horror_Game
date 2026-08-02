using System.Collections;
using UnityEngine;

public class MMCharacterRandomAnimation : MonoBehaviour {
    [Header("Random Animation Settings")]
    public float minTime = 5f;
    public float maxTime = 15f;

    private new MMCharacterAnimation animation;
    public bool isCustomizing = false;

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
            if(!isCustomizing) animation.SetAnimationState("lookingOver");
        }
    }

    public void SetAnimationState(string state) {
        animation.SetAnimationState(state);
    }

    public void SetIsCustomizing(bool customizing) {
        isCustomizing = customizing;
    }
}
