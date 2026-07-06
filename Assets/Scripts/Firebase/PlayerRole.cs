// Player permission tier. Assigned manually via Firebase Console (or Admin SDK) —
// never writable by the client except the implicit "Player" default on account creation.
public enum PlayerRole {
    Player,
    Creator,
    Developer,
    Admin
}