[System.Serializable]
public class SaveData {
    public string playerName;
    public string gender;
    public Graphics graphics = new Graphics();

    [System.Serializable]
    public class Graphics {
        public string resolution;
    }
}
