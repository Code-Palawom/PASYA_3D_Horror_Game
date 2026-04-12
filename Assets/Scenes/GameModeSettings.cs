public class GameModeSettings {
    public enum GameMode { SinglePlayer, HostMultiplayer, JoinMultiplayer }
    public static GameMode CurrentMode = GameMode.SinglePlayer;
}