using System;
using System.Collections.Generic;
using UnityEngine;

// Maps an IconId string (set on a remote achievement in the admin
// dashboard, see RemoteAchievementDefinition.IconId) to a Sprite already
// bundled in the build. Exists because Firestore can't ship new sprite
// assets into an already-built client — this only lets a remote
// achievement *reuse* an icon the client already has.
//
// Populate entries with whatever sprites you want remotely-addressable;
// the IconId string in Firestore must match an entry's iconId exactly
// (case-sensitive). Same "optional serialized reference, resolved by id"
// shape as SkinDatabaseSO — pass this into AchievementToastItemUI /
// AchievementListUI the same way you'd pass a SkinDatabaseSO.
[CreateAssetMenu(fileName = "AchievementIconDatabase", menuName = "Pasya/Achievement Icon Database")]
public class AchievementIconDatabaseSO : ScriptableObject {
    [Serializable]
    public class IconEntry {
        public string iconId;
        public Sprite sprite;
    }

    public List<IconEntry> icons = new List<IconEntry>();

    private Dictionary<string, Sprite> _lookup;

    // Returns null if iconId is blank or has no matching entry — callers
    // (AchievementToastItemUI, AchievementRowUI) treat that the same as no
    // icon at all and fall back to their own fallbackIcon.
    public Sprite GetById(string iconId) {
        if (string.IsNullOrEmpty(iconId)) return null;

        // Built lazily rather than in OnEnable — ScriptableObject OnEnable
        // can run before the Inspector-edited `icons` list is fully
        // deserialized in editor-reload scenarios; lazy-build on first use
        // avoids that ordering hazard.
        if (_lookup == null) {
            _lookup = new Dictionary<string, Sprite>();
            foreach (var entry in icons) {
                if (string.IsNullOrEmpty(entry.iconId)) continue;
                _lookup[entry.iconId] = entry.sprite;
            }
        }

        return _lookup.TryGetValue(iconId, out var sprite) ? sprite : null;
    }
}