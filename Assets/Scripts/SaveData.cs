[System.Serializable]
public class SaveData {
    public string playerName;
    public string gender;
    public Graphics graphics = new Graphics();

    public string pov;

    public bool debugMode = false;

    [System.Serializable]
    public class Graphics {
        public string resolution;
    }
}
