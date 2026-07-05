using Firebase.Firestore;

// Firestore-mapped player profile
[FirestoreData]
public class PlayerProfile {
    [FirestoreProperty] public string DisplayName { get; set; }
    [FirestoreProperty] public int Level { get; set; }
    [FirestoreProperty] public long Xp { get; set; }
    [FirestoreProperty] public int GamesPlayed { get; set; }
    [FirestoreProperty] public int HighScore { get; set; }
    [FirestoreProperty] public long CorrectAnswers { get; set; }
    [FirestoreProperty] public long IncorrectAnswers { get; set; }
    [FirestoreProperty] public Timestamp CreatedAt { get; set; }
    [FirestoreProperty] public Timestamp LastLoginAt { get; set; }

    // Not a Firestore field — the document ID *is* the uid, populated after fetch.
    public string Uid { get; set; }
}