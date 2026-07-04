using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json;
using UnityEngine;

// Handles Google Sign-In on Desktop/Editor via PKCE OAuth loopback redirect.
// Attach to the same GameObject as AuthManager.
public class DesktopGoogleAuth : MonoBehaviour {
    [Tooltip("Must match one of the Authorized Redirect URIs in Google Cloud Console")]
    [SerializeField] private int port = 5000;

    // Pulled from SecretStore at runtime — never stored in a serialized field
    private string ClientId => SecretStore.GoogleClientId;
    private string ClientSecret => SecretStore.GoogleClientSecret;

    private string RedirectUri => $"http://localhost:{port}/";

    // ── Public entry point ───────────────────────────────────────────────

    // Opens the system browser for Google Sign-In and returns the Firebase user on success.
    public async Task<FirebaseUser> SignInAsync(CancellationToken cancellationToken = default) {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        string state = GenerateState();

        Application.OpenURL(BuildAuthUrl(codeChallenge, state));

        string code = await ListenForCodeAsync(state, cancellationToken);  // pass it down
        if (string.IsNullOrEmpty(code))
            throw new OperationCanceledException("OAuth sign-in was cancelled or timed out.");

        TokenResponse tokens = await ExchangeCodeAsync(code, codeVerifier);
        var credential = GoogleAuthProvider.GetCredential(tokens.IdToken, null);
        return await FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(credential);
    }

    // ── Auth URL ─────────────────────────────────────────────────────────

    private string BuildAuthUrl(string codeChallenge, string state) {
        string scope = Uri.EscapeDataString("openid email profile");
        string redirect = Uri.EscapeDataString(RedirectUri);

        return "https://accounts.google.com/o/oauth2/v2/auth"
             + $"?client_id={ClientId}"
             + $"&redirect_uri={redirect}"
             + $"&response_type=code"
             + $"&scope={scope}"
             + $"&code_challenge={codeChallenge}"
             + $"&code_challenge_method=S256"
             + $"&state={state}"
             + $"&access_type=offline"
             + $"&prompt=select_account";
    }

    // ── Loopback listener ─────────────────────────────────────────────────

    private async Task<string> ListenForCodeAsync(string expectedState, CancellationToken externalToken = default) {
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);

        try { listener.Start(); } catch (HttpListenerException e) {
            Debug.LogError($"[DesktopAuth] Could not start listener on port {port}: {e.Message}");
            return null;
        }

        // Link internal 3min timeout with the external cancel button token
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            new CancellationTokenSource(TimeSpan.FromMinutes(3)).Token
        );
        cts.Token.Register(() => { try { listener.Stop(); } catch { } });

        HttpListenerContext ctx;
        try {
            ctx = await listener.GetContextAsync();
        } catch (Exception) when (cts.IsCancellationRequested) {
            // Throw so the caller's catch (OperationCanceledException) fires
            throw new OperationCanceledException("Sign-in cancelled.", cts.Token);
        } catch (Exception e) {
            Debug.LogError($"[DesktopAuth] Listener error: {e.Message}");
            return null;
        }

        // Send close page BEFORE stopping the listener —
        // stopping first disposes the response and causes a crash.
        await SendClosePageAsync(ctx.Response);
        if (listener.IsListening) listener.Stop();

        var query = ctx.Request.QueryString;

        if (query["error"] != null) {
            Debug.LogError($"[DesktopAuth] OAuth error returned: {query["error"]}");
            return null;
        }

        if (query["state"] != expectedState) {
            Debug.LogError("[DesktopAuth] State mismatch — possible CSRF, aborting.");
            return null;
        }

        return query["code"];
    }

    private static async Task SendClosePageAsync(HttpListenerResponse response) {
        const string html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>You're signed in to PASYA</title>
<style>
  * { box-sizing: border-box; }
  html, body { height: 100%; margin: 0; }
  body {
    font-family: 'Google Sans', 'Segoe UI', Roboto, Arial, sans-serif;
    background-color: #ffffff;
    background-image:
      linear-gradient(rgba(0,0,0,0.04) 1px, transparent 1px),
      linear-gradient(90deg, rgba(0,0,0,0.04) 1px, transparent 1px);
    background-size: 40px 40px;
    color: #202124;
    display: flex; align-items: center; justify-content: center;
    min-height: 100vh; overflow: hidden; position: relative;
  }
  .card {
    position: relative; z-index: 2;
    background: #ffffff; border: 1px solid #e0e0e0; border-radius: 16px;
    padding: 48px 40px; max-width: 380px; width: 90%; text-align: center;
    box-shadow: 0 4px 24px rgba(60, 64, 67, 0.15);
    animation: pop-in 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
  }
  @keyframes pop-in {
    0% { opacity: 0; transform: scale(0.85) translateY(10px); }
    100% { opacity: 1; transform: scale(1) translateY(0); }
  }
  .check-ring {
    width: 76px; height: 76px; margin: 0 auto 24px; border-radius: 50%;
    background: conic-gradient(#4285F4 0deg 90deg, #34A853 90deg 180deg, #FBBC05 180deg 270deg, #EA4335 270deg 360deg);
    display: flex; align-items: center; justify-content: center; padding: 4px;
    animation: ring-pulse 1.8s ease-out infinite;
  }
  @keyframes ring-pulse {
    0% { box-shadow: 0 0 0 0 rgba(66, 133, 244, 0.35); }
    70% { box-shadow: 0 0 0 14px rgba(66, 133, 244, 0); }
    100% { box-shadow: 0 0 0 0 rgba(66, 133, 244, 0); }
  }
  .check-ring-inner {
    width: 100%; height: 100%; border-radius: 50%; background: #ffffff;
    display: flex; align-items: center; justify-content: center;
  }
  .check-ring svg { width: 34px; height: 34px; stroke: #34A853; stroke-width: 3; fill: none; }
  h2 { margin: 0 0 10px; font-size: 22px; font-weight: 500; letter-spacing: 0.1px; color: #202124; }
  p { margin: 0; font-size: 14.5px; color: #5f6368; line-height: 1.5; }
</style>
</head>
<body>
  <div class=""card"">
    <div class=""check-ring"">
      <div class=""check-ring-inner"">
        <svg viewBox=""0 0 24 24""><polyline points=""4,13 9,18 20,6""/></svg>
      </div>
    </div>
    <h2>Sign-in complete</h2>
    <p>You can close this tab now.</p>
  </div>
</body>
</html>";
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }

    // ── Token exchange ────────────────────────────────────────────────────

    private async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier) {
        using var http = new HttpClient();
        var body = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["code"] = code,
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        });

        var resp = await http.PostAsync("https://oauth2.googleapis.com/token", body);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"[DesktopAuth] Token exchange failed ({resp.StatusCode}): {json}");

        return JsonConvert.DeserializeObject<TokenResponse>(json);
    }

    // ── PKCE helpers ──────────────────────────────────────────────────────

    private static string GenerateCodeVerifier() {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier) {
        using var sha = SHA256.Create();
        return Base64UrlEncode(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
    }

    private static string GenerateState() {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ── Token model ───────────────────────────────────────────────────────

    [Serializable]
    private class TokenResponse {
        [JsonProperty("id_token")] public string IdToken { get; set; }
        [JsonProperty("access_token")] public string AccessToken { get; set; }
        [JsonProperty("refresh_token")] public string RefreshToken { get; set; }
    }
}