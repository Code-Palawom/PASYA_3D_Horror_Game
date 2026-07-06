using System;
using Firebase.Firestore;

// Firestore-mapped player profile document: users/{uid}
[FirestoreData]
public class PlayerProfile {
    [FirestoreProperty] public string DisplayName { get; set; }
    [FirestoreProperty] public long Xp { get; set; }
    [FirestoreProperty] public int GamesPlayed { get; set; }
    [FirestoreProperty] public int HighScore { get; set; }
    [FirestoreProperty] public long CorrectAnswers { get; set; }
    [FirestoreProperty] public long IncorrectAnswers { get; set; }

    // Stored as a plain string ("Player", "Admin", etc.) so it round-trips
    // exactly against Firestore Security Rules string comparisons and the
    // Console's manual-edit workflow. Firebase's enum serialization isn't
    // guaranteed to be the string name, so we don't store PlayerRole directly.
    [FirestoreProperty] public string Role { get; set; } = PlayerRole.Player.ToString();

    // Typed access to Role. Falls back to Player if the stored value
    // doesn't match a known enum name (e.g. manual typo in the Console).
    public PlayerRole RoleEnum {
        get => Enum.TryParse(Role, out PlayerRole parsed) ? parsed : PlayerRole.Player;
        set => Role = value.ToString();
    }

    [FirestoreProperty] public Timestamp CreatedAt { get; set; }
    [FirestoreProperty] public Timestamp LastLoginAt { get; set; }

    // Not a Firestore field — the document ID *is* the uid, populated after fetch.
    public string Uid { get; set; }
}