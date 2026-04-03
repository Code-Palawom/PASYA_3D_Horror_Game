using UnityEngine;

public class ChangeCharacterController : MonoBehaviour {
    public ChangeCharacterHandler characterHandler;

    public void SetCharacter(CharacterData data) {
        if(data != null){
            characterHandler.currentData = data;
            characterHandler.ApplyCharacter();
        }
    }
}
