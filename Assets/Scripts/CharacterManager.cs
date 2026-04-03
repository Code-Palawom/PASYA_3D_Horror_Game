using UnityEngine;

public class CharacterManager : MonoBehaviour {
    public SaveManager saveManager;
    public CharacterData currentData;
    public ChangeCharacterHandler character;

    void Start() {
        string savedName = saveManager.Load();

        if(!string.IsNullOrEmpty(savedName)){
            CharacterData data = Resources.Load<CharacterData>("Characters/" + savedName);
            if(data != null) SetCharacter(data);
        }
    }

    public void TryHairMaterial(Material material) {
        currentData.hairMaterial = material;
        character.ApplyCharacter();
    }

    public void TryBodyMaterial(Material material) {
        currentData.bodyMaterial = material;
        character.ApplyCharacter();
    }

    public void SetCharacter(CharacterData data) {
        character.currentData = data;
        character.ApplyCharacter();
        saveManager.Save(data.name);
    }
}