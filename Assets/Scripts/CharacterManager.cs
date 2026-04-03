using UnityEditor.Overlays;
using UnityEngine;

public class CharacterManager : MonoBehaviour {
    public SaveManager saveManager;
    public ChangeCharacterHandler character;

    public CharacterData currentData;

    private CharacterData saveData;
    private CharacterData backupCharacterData;

    private Material hair;
    private Material body;

    void Start() {
        string savedName = saveManager.Load();

        CharacterData data = Resources.Load<CharacterData>("Characters/character");
        
        if(data != null){
            saveData = Instantiate(data);
            saveData.name = data.name;

            character.ApplyCharacter(saveData);
        }
    }

    public void CustomizeCharacter() {
        backupCharacterData = Instantiate(saveData);
        backupCharacterData.name = saveData.name;
    }

    public void TryHairMaterial(Material material) {
        saveData.hairMaterial = material;
        hair = material;
        character.ApplyCharacter(saveData);
    }

    public void TryBodyMaterial(Material material) {
        saveData.bodyMaterial = material;
        body = material;
        character.ApplyCharacter(saveData);
    }

    public void SetCharacter() {
        Debug.Log("Character Saved!");

        if(hair != null) currentData.hairMaterial = hair;
        if(body != null) currentData.bodyMaterial = body;
    }

    public void RevertCharacter() {
        saveData = backupCharacterData;
        backupCharacterData = Instantiate(saveData);
        backupCharacterData.name = saveData.name;

        character.ApplyCharacter(saveData);
    }

    private void OnDestroy() {
        if(backupCharacterData != null){
            Destroy(backupCharacterData);
            backupCharacterData = null;
        }
    }
}