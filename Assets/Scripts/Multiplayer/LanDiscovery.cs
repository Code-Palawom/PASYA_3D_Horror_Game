using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[System.Serializable]
public class DiscoveredHost {
    public string HostName;
    public string QuizSetName;
    public string LevelSceneName;
    public int QuestionCount;
    public int PlayerCount;
    public List<string> PlayerNames = new();
    public string Address;
    public ushort GamePort;
    public int PingMs;         // round-trip time of the discovery broadcast
}

// LAN discovery over UDP broadcast — completely separate from the
// actual game NetworkTransport (which uses its own port for real
// connections, e.g. 7777). This only finds hosts; joining still
// goes through NetworkManager.StartClient() afterward.

// Host:   listens on discoveryPort, replies to any request with its
//         current info (level, quiz, live player names).
// Client: broadcasts a request and collects replies for a few seconds.
public class LanDiscovery : MonoBehaviour {
    public static LanDiscovery Instance { get; private set; }

    [SerializeField] int discoveryPort = 47777;

    private const string RequestToken = "QUIZGAME_DISCOVER_REQUEST";
    private const string ResponsePrefix = "QUIZGAME_DISCOVER_RESPONSE";

    private UdpClient _hostListener;
    private UdpClient _clientListener;

    private string _hostNameToBroadcast;
    private string _quizNameToBroadcast;
    private string _levelSceneNameToBroadcast;
    private int _questionCountToBroadcast;
    private ushort _gamePortToBroadcast;
    private Func<List<string>> _playerNamesProvider;

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────────
    // HOST SIDE
    // ─────────────────────────────────────────────────────────
    public void StartHostBroadcast(
        string hostName,
        string quizName,
        string levelSceneName,
        int questionCount,
        ushort gamePort,
        Func<List<string>> playerNamesProvider) {
        StopHostBroadcast();

        _hostNameToBroadcast = AuthManager.Instance.CurrentUser?.DisplayName ?? hostName;
        _quizNameToBroadcast = quizName;
        _levelSceneNameToBroadcast = levelSceneName;
        _questionCountToBroadcast = questionCount;
        _gamePortToBroadcast = gamePort;
        _playerNamesProvider = playerNamesProvider;

        try {
            _hostListener = new UdpClient(discoveryPort) { EnableBroadcast = true };
            StartCoroutine(HostListenLoop());
            Debug.Log($"[LanDiscovery] Host broadcasting on port {discoveryPort}.");
        } catch (Exception e) {
            Debug.LogWarning($"[LanDiscovery] Failed to start host broadcast: {e.Message}");
        }
    }

    public void StopHostBroadcast() {
        _hostListener?.Close();
        _hostListener = null;
    }

    IEnumerator HostListenLoop() {
        while (_hostListener != null) {
            if (_hostListener.Available > 0) {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = null;
                try { data = _hostListener.Receive(ref remote); } catch { /* socket closed mid-read */ }

                if (data != null && Encoding.UTF8.GetString(data) == RequestToken) {
                    List<string> names = _playerNamesProvider?.Invoke() ?? new List<string>();
                    string namesCsv = string.Join(",", names);

                    string response = $"{ResponsePrefix}|{_hostNameToBroadcast}|{_quizNameToBroadcast}|" +
                                       $"{_levelSceneNameToBroadcast}|{_questionCountToBroadcast}|" +
                                       $"{names.Count}|{_gamePortToBroadcast}|{namesCsv}";

                    byte[] respBytes = Encoding.UTF8.GetBytes(response);
                    _hostListener.Send(respBytes, respBytes.Length, remote);
                }
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────
    // CLIENT SIDE
    // ─────────────────────────────────────────────────────────
    public void StartClientDiscovery(Action<DiscoveredHost> onHostFound, float duration = 4f) {
        StopClientDiscovery();

        try {
            _clientListener = new UdpClient(0) { EnableBroadcast = true };
        } catch (Exception e) {
            Debug.LogWarning($"[LanDiscovery] Failed to start client discovery: {e.Message}");
            return;
        }

        StartCoroutine(ClientDiscoveryRoutine(onHostFound, duration));
    }

    public void StopClientDiscovery() {
        _clientListener?.Close();
        _clientListener = null;
    }

    IEnumerator ClientDiscoveryRoutine(Action<DiscoveredHost> onHostFound, float duration) {
        byte[] requestBytes = Encoding.UTF8.GetBytes(RequestToken);
        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

        var seen = new HashSet<string>();
        float elapsed = 0f;
        float nextBroadcast = 0f;
        float lastSentTime = -1f;   // tracks when latest broadcast was sent for ping calc

        while (elapsed < duration && _clientListener != null) {
            if (elapsed >= nextBroadcast) {
                try {
                    _clientListener.Send(requestBytes, requestBytes.Length, broadcastEndpoint);
                    lastSentTime = elapsed;   // record send time
                } catch (Exception e) { Debug.LogWarning($"[LanDiscovery] Broadcast failed: {e.Message}"); }
                nextBroadcast = elapsed + 1f;
            }

            if (_clientListener.Available > 0) {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = null;
                try { data = _clientListener.Receive(ref remote); } catch { /* ignore */ }

                if (data != null) {
                    // Ping = time between last broadcast send and this response
                    int pingMs = lastSentTime >= 0
                        ? Mathf.RoundToInt((elapsed - lastSentTime) * 1000f)
                        : -1;

                    string msg = Encoding.UTF8.GetString(data);
                    if (msg.StartsWith(ResponsePrefix)) {
                        string[] parts = msg.Split('|');
                        // 0=prefix 1=hostName 2=quizName 3=levelScene 4=questionCount
                        // 5=playerCount 6=gamePort 7=playerNamesCsv (optional)
                        if (parts.Length >= 7) {
                            string key = $"{remote.Address}:{parts[6]}";
                            if (seen.Add(key)) {
                                var names = parts.Length >= 8 && !string.IsNullOrEmpty(parts[7])
                                    ? parts[7].Split(',').ToList()
                                    : new List<string>();

                                onHostFound?.Invoke(new DiscoveredHost {
                                    HostName = parts[1],
                                    QuizSetName = parts[2],
                                    LevelSceneName = parts[3],
                                    QuestionCount = int.TryParse(parts[4], out int qc) ? qc : 0,
                                    PlayerCount = int.TryParse(parts[5], out int pc) ? pc : 0,
                                    GamePort = ushort.TryParse(parts[6], out ushort gp) ? gp : (ushort)7777,
                                    Address = remote.Address.ToString(),
                                    PlayerNames = names,
                                    PingMs = pingMs
                                });
                            }
                        }
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopClientDiscovery();
    }

    void OnDestroy() {
        StopHostBroadcast();
        StopClientDiscovery();
    }
}