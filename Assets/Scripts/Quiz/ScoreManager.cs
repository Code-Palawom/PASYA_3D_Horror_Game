using UnityEngine;

public class ScoreManager : MonoBehaviour {
    public static ScoreManager Instance { get; private set; }

    public int TotalScore { get; private set; }

    void Awake() => Instance = this;

    public void AddScore(int amount) {
        TotalScore += amount;
        Debug.Log($"[ScoreManager] +{amount} pts → Total: {TotalScore}");
        // Hook into your score UI here
    }

    public void ResetScore() => TotalScore = 0;
}