using System;

// Server-side only. Parses "/"-prefixed chat messages and dispatches them
// to the relevant system, after checking the sender's role.
// Called from ChatManager.SendChatMessageRpc, BEFORE the message is broadcast
// as normal chat — commands never appear in the public chat log.
public static class ChatCommandProcessor {
    // Returns true if the message was a command (caller should NOT broadcast it as chat).
    public static bool TryHandle(ulong senderId, string content, Action<string> replyToSender) {
        if (string.IsNullOrEmpty(content) || content[0] != '/') return false;

        string[] parts = content.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        switch (parts[0].ToLowerInvariant()) {
            case "time":
                HandleTime(senderId, parts, replyToSender);
                return true;
            case "culling":
                HandleCulling(senderId, replyToSender);
                return true;
            default:
                replyToSender($"Unknown command: /{parts[0]}");
                return true;
        }
    }

    // Looks up the sender's role as stored on GameSessionManager.Players
    // (set once at connection approval — see ConnectionApprovalHandler).
    static bool HasPermission(ulong clientId) {
        if (GameSessionManager.Instance == null) return false;

        foreach (var p in GameSessionManager.Instance.Players) {
            if (p.ClientId != clientId) continue;
            var role = (PlayerRole)p.Role;
            return role == PlayerRole.Admin || role == PlayerRole.Developer;
        }

        return false;
    }

    static void HandleTime(ulong senderId, string[] parts, Action<string> reply) {
        if (!HasPermission(senderId)) {
            reply("You don't have permission to use this command.");
            return;
        }

        if (TimeManager.Instance == null) {
            reply("TimeManager not found.");
            return;
        }

        if (parts.Length < 3) {
            reply("Usage: /time set HH:MM  |  /time skip <hours>");
            return;
        }

        switch (parts[1].ToLowerInvariant()) {
            case "set": {
                    var hm = parts[2].Split(':');
                    if (hm.Length != 2 || !int.TryParse(hm[0], out int h) || !int.TryParse(hm[1], out int m)) {
                        reply("Invalid format. Use HH:MM, e.g. /time set 14:30");
                        return;
                    }
                    TimeManager.Instance.AdminSetTime(h, m);
                    reply($"Time set to {h:00}:{m:00}.");
                    break;
                }
            case "skip": {
                    if (!int.TryParse(parts[2], out int hrs)) {
                        reply("Invalid number of hours.");
                        return;
                    }
                    TimeManager.Instance.AdminSkipHours(hrs);
                    reply($"Skipped {hrs} hour(s).");
                    break;
                }
            case "speed": {
                    if (!float.TryParse(parts[2], out float speed)) {
                        reply("Invalid speed value.");
                        return;
                    }

                    TimeManager.Instance.AdminSetTimeSpeed(speed);
                    reply($"Time speed set to {speed}x.");
                    break;
                }
            default:
                reply("Unknown /time subcommand. Use 'set' or 'skip'.");
                break;
        }
    }

    static void HandleCulling(ulong senderId, Action<string> reply) {
        if (!HasPermission(senderId)) {
            reply("You don't have permission to use this command.");
            return;
        }

        if (DevTools.Instance != null) {
            reply("Toggling culling.");
            DevTools.Instance.ToggleCulling();
        }
    }
}