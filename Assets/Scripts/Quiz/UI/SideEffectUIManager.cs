using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Central manager for side effect UI.
// - HUD tray: only shown on the local player's screen.
// - World billboard: shown above any affected player, visible to all.
public class SideEffectUIManager : MonoBehaviour {
    public static SideEffectUIManager Instance { get; private set; }

    [Header("HUD (Screen-space)")]
    [SerializeField] Transform hudEffectContainer;          // Horizontal Layout Group
    [SerializeField] GameObject hudEffectEntryPrefab;

    [Header("World-space Billboard")]
    [SerializeField] GameObject worldEffectBillboardPrefab;
    [SerializeField] float billboardYOffset = 2.5f;

    private class ActiveEntry {
        public QuizSideEffect effect;
        public GameObject hudEntry;
        public WorldEffectBillboard billboard;
    }

    // Track per player
    private Dictionary<GameObject, List<ActiveEntry>> _active = new();

    void Awake() => Instance = this;

    // Called by QuizSideEffect.ApplyWithDuration on all clients.
    // isLocalPlayer: controls whether HUD entry is created.
    // Billboard always shown.
    public void AddEffect(QuizSideEffect effect, GameObject player, bool isLocalPlayer) {
        if (!_active.ContainsKey(player))
            _active[player] = new List<ActiveEntry>();

        // Prevent duplicate entries for same effect on same player
        if (_active[player].Any(e => e.effect == effect)) return;

        // HUD — local player only
        GameObject hudEntry = null;
        if (isLocalPlayer) {
            hudEntry = Instantiate(hudEffectEntryPrefab, hudEffectContainer);
            hudEntry.GetComponent<HUDEffectEntry>().Setup(effect);
        }

        // Billboard — all clients see this above the affected player
        var billboard = GetOrCreateBillboard(player);
        billboard.AddEffect(effect);

        _active[player].Add(new ActiveEntry {
            effect = effect,
            hudEntry = hudEntry,
            billboard = billboard
        });
    }

    public void RemoveEffect(QuizSideEffect effect, GameObject player) {
        if (!_active.TryGetValue(player, out var list)) return;

        var entry = list.FirstOrDefault(e => e.effect == effect);
        if (entry == null) return;

        if (entry.hudEntry != null) Destroy(entry.hudEntry);
        entry.billboard?.RemoveEffect(effect);
        list.Remove(entry);
    }

    WorldEffectBillboard GetOrCreateBillboard(GameObject player) {
        var existing = player.GetComponentInChildren<WorldEffectBillboard>();
        if (existing != null) return existing;

        var go = Instantiate(worldEffectBillboardPrefab, player.transform);
        go.transform.localPosition = new Vector3(0f, billboardYOffset, 0f);
        return go.GetComponent<WorldEffectBillboard>();
    }
}