using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// End-game podium screen. Shows once, right after EndGameTriggerZone's
// countdown finishes and teleports everyone — subscribes to the same
// GameSessionManager.OnResultsReceived event PlayerStatsRecorder uses, so
// both fire off the same broadcast.
//
// Local player's XP counts up from their pre-game cached total to
// pre-game + this session's Score, purely client-side — no extra Firestore
// read needed, since PlayerStatsRecorder's write increments Firestore by
// exactly Score, so cached-before + Score IS the new total.
// Attach to a screen-space Canvas panel in your Level scene, wire up the
// serialized fields, and leave screenRoot inactive by default.
public class EndGameScreenUI : MonoBehaviour {
    public static EndGameScreenUI Instance { get; private set; }

    [SerializeField] GameObject gameUi;
    [SerializeField] GameObject screenRoot;      // whole panel, toggled on/off
    [SerializeField] TMP_Text titleText;         // big header, e.g. "Game Ended"
    [SerializeField] string titleString = "Game Ended";
    [SerializeField] TMP_Text subtitleText;      // small text, e.g. "Ending in 7 seconds"
    [SerializeField] Transform cardContainer;
    [SerializeField] EndGamePlayerCardUI cardPrefab;
    [SerializeField] SkinDatabaseSO skinDatabase; // resolves PlayerSessionStats.SkinId -> CharacterSkinSO.previewIcon
    [SerializeField] float xpCountUpDuration = 1.5f;
    [SerializeField] Button exitButton;

    readonly List<EndGamePlayerCardUI> _spawnedCards = new();

    void Awake() {
        Instance = this;
        if (screenRoot != null) screenRoot.SetActive(false);
    }

    void OnEnable() => GameSessionManager.OnResultsReceived += HandleResults;
    void OnDisable() => GameSessionManager.OnResultsReceived -= HandleResults;

    // Called by EndGameTriggerZone's countdown RPC, every second, on every
    // client. Brings the panel up early with the title + a live countdown;
    // no cards yet since results haven't been sent. HandleResults() below
    // takes over (populates cards, hides the subtitle) once it fires.
    public void ShowCountdown(int secondsRemaining) {
        if (screenRoot != null) screenRoot.SetActive(true);
        if (titleText != null) titleText.text = titleString;

        if (subtitleText != null) {
            subtitleText.gameObject.SetActive(true);
            subtitleText.text = secondsRemaining > 0
                ? $"Ending in {secondsRemaining} second{(secondsRemaining == 1 ? "" : "s")}"
                : "Ending now...";
        }
    }

    void HandleResults(GameSessionManager.PlayerSessionStats[] results) {
        // Already sorted descending by Score (GameSessionManager.SendResults),
        // so rank is just index + 1.
        ulong localId = NetworkManager.Singleton.LocalClientId;

        ClearCards();

        EndGamePlayerCardUI localCard = null;
        GameSessionManager.PlayerSessionStats localStats = null;

        gameUi.SetActive(false); // hide the in-game HUD while the podium is up
        exitButton.gameObject.SetActive(true);

        for (int i = 0; i < results.Length; i++) {
            var stats = results[i];
            bool isLocal = stats.ClientId == localId;

            var card = Instantiate(cardPrefab, cardContainer);
            Sprite icon = null;
            if (skinDatabase != null && !string.IsNullOrEmpty(stats.SkinId)) {
                var skin = skinDatabase.GetById(stats.SkinId);
                icon = skin != null ? skin.previewIcon : null;
            }
            card.Setup(i + 1, stats, isLocal, icon);
            _spawnedCards.Add(card);

            if (isLocal) {
                localCard = card;
                localStats = stats;
            }
        }

        if (screenRoot != null) screenRoot.SetActive(true);
        if (titleText != null) titleText.text = titleString;
        if (subtitleText != null) subtitleText.gameObject.SetActive(false);

        if (localCard != null && localStats != null && !localStats.Disconnected)
            StartCoroutine(CountUpLocalXp(localCard, localStats.Score));
    }

    IEnumerator CountUpLocalXp(EndGamePlayerCardUI card, int scoreGained) {
        var profile = AuthManager.Instance.CurrentProfile;
        if (profile == null) yield break;

        long startXp = profile.Xp;
        long endXp = GameSessionManager.CalculateXp(scoreGained);

        // Reflect the new total locally right away so the rest of the app
        // (menus, HUD) isn't stale until the next Firestore read.
        profile.Xp = startXp + endXp;

        float t = 0f;
        while (t < xpCountUpDuration) {
            t += Time.deltaTime;
            long current = (long)Mathf.Lerp(0, endXp, t / xpCountUpDuration);
            card.SetXpValue(current);
            yield return null;
        }
        card.SetXpValue(endXp);
    }

    void ClearCards() {
        foreach (var card in _spawnedCards)
            if (card != null) Destroy(card.gameObject);
        _spawnedCards.Clear();
    }

    public void Close() {
        if (screenRoot != null) screenRoot.SetActive(false);
    }
}