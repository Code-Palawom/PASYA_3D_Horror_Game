// Represents a public online session returned by Unity Lobby Service queries.
// Counterpart to DiscoveredHost (which covers LAN UDP discovery).
[System.Serializable]
public class OnlineDiscoveredSession {
    public string LobbyId;
    public string HostName;
    public string QuizSetName;
    public string LevelSceneName;
    public int QuestionCount;
    public int PlayerCount;
    public int MaxPlayers;
    public string RelayJoinCode;   // used to configure UnityTransport before StartClient()
}