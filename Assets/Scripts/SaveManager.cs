using UnityEngine;
using System.IO;

public class SaveManager : MonoBehaviour {
    private string savePath;
    private string key = "pasya123";

    void Awake() {
        savePath = Path.Combine(Application.persistentDataPath, "player_save.dat");
    }

    public void Save(string charName) {
        SaveData data = new SaveData { selectedCharacter = charName };
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(savePath, EncryptDecrypt(json));
    }

    public string Load() {
        if (!File.Exists(savePath)) return null;
        string encryptedJson = File.ReadAllText(savePath);
        SaveData data = JsonUtility.FromJson<SaveData>(EncryptDecrypt(encryptedJson));
        return data.selectedCharacter;
    }

    private string EncryptDecrypt(string text) {
        string result = "";
        for (int i = 0; i < text.Length; i++)
            result += (char)(text[i] ^ key[i % key.Length]);
        return result;
    }
}