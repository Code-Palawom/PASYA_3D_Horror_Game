using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;


// HMAC-SHA256 integrity check for saved quiz JSON files.

// How it works:
//   Save: compute HMAC of JSON content → store alongside JSON
//   Load: recompute HMAC → compare → reject if mismatch

// The secret key is derived from a base key + device identifier
// so a signature from one device is invalid on another.
public static class QuizDataIntegrity {
    // ── Base secret — change this to something unique for your game ──
    private const string BaseSecret = "GAMELOL";

    // ─────────────────────────────────────────────────────────
    // Derive a device-bound key so signatures don't transfer
    // between devices
    // ─────────────────────────────────────────────────────────
    private static byte[] GetKey() {
        // Combine base secret with device identifier
        string combined = BaseSecret + SystemInfo.deviceUniqueIdentifier;
        return Encoding.UTF8.GetBytes(combined);
    }

    // ─────────────────────────────────────────────────────────
    // Compute HMAC-SHA256 of a string → base64 result
    // ─────────────────────────────────────────────────────────
    public static string ComputeSignature(string data) {
        using var hmac = new HMACSHA256(GetKey());
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    // ─────────────────────────────────────────────────────────
    // Verify that data matches a previously computed signature
    // ─────────────────────────────────────────────────────────
    public static bool Verify(string data, string signature) {
        if (string.IsNullOrEmpty(signature)) return false;

        string expected = ComputeSignature(data);

        // Constant-time comparison to prevent timing attacks
        return CryptographicEquals(expected, signature);
    }

    // ─────────────────────────────────────────────────────────
    // Constant-time string comparison
    // ─────────────────────────────────────────────────────────
    static bool CryptographicEquals(string a, string b) {
        if (a.Length != b.Length) return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }
}