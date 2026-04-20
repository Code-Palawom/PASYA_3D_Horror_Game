using UnityEngine;
using System.IO;
using System;

public class SaveManager : MonoBehaviour {
    private string savePath;
    private string key = "QWERTY";

    void Awake() {
        savePath = Path.Combine(Application.persistentDataPath, "player_save.dat");
    }

    //public void Save(string charName) {
    //    SaveData data = new SaveData { gender = charName };
    //    string json = JsonUtility.ToJson(data);
    //    File.WriteAllText(savePath, EncryptDecrypt(json));
    //}

    public void SaveOneData(string d, string type) {
        SaveData data = Load();

        if(type == "playerName") {
            data.playerName = d;
        }else if(type == "gender") {
            data.gender = d;
        }else if(type == "resolution") {
            data.graphics = new SaveData.Graphics {
                resolution = d
            };
        }else if(type == "pov") {
            data.pov = d;
        }

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(savePath, EncryptDecrypt(json));
    }

    public void SaveOneData(bool d, string type) {
        SaveData data = Load();

        if(type == "debugMode") {
            data.debugMode = d;
        }

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(savePath, EncryptDecrypt(json));
    }

    public SaveData Load() {
        if(!File.Exists(savePath)) {
            SaveData data = new SaveData();
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(savePath, EncryptDecrypt(json));
        }
        
        string encryptedJson = File.ReadAllText(savePath);

        try{
            SaveData data = JsonUtility.FromJson<SaveData>(EncryptDecrypt(encryptedJson));

            return data;
        }catch{
            return null;
        }
    }

    public string GetOneData(string type) {
        if (!File.Exists(savePath)) return null;
        string encryptedJson = File.ReadAllText(savePath);

        try{
            SaveData data = JsonUtility.FromJson<SaveData>(EncryptDecrypt(encryptedJson));

            if(type == "playerName") {
                return data.playerName;
            }else if(type == "gender") {
                return data.gender;
            }else if(type == "resolution"){
                return data.graphics.resolution;
            }else if(type == "pov"){
                return data.pov;
            }else{
                return null;
            }
        }catch{
            return null;
        }
    }

    public bool GetOneData(int type) {
        if (!File.Exists(savePath)) return false;
        string encryptedJson = File.ReadAllText(savePath);

        static string d(int i) {
            return i switch {
                0 => "debugMode",
                _ => null,
            };
        }

        try {
            SaveData data = JsonUtility.FromJson<SaveData>(EncryptDecrypt(encryptedJson));

            if(d(type) == "debugMode") {
                return data.debugMode;
            }else{
                return false;
            }
        }catch{
            return false;
        }
    }

    private string EncryptDecrypt(string text) {
        string result = "";
        for (int i = 0; i < text.Length; i++)
            result += (char)(text[i] ^ key[i % key.Length]);
        return result;
    }
}