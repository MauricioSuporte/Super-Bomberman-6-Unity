using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Assets.Scripts.Netcode
{
    // Shared entry point for the title screen and the online version gate.
    public sealed class GameUpdateController : MonoBehaviour
    {
        public const string DiscordInvite = "https://discord.gg/F2dTdmwChF";
        const string ReleaseApi = "https://api.github.com/repos/MauricioSuporte/Super-Bomberman-6-Unity/releases/tags/online-v0.5.0";
        [Serializable] sealed class Release { public string body = ""; }
        [Serializable] sealed class Manifest { public string androidUrl = "", androidSha256 = "", sourceCommit = ""; }
        static GameUpdateController active;
        public static bool IsOpen => active != null;
        TextMeshProUGUI status;
        bool finished;

        public static IEnumerator CheckAvailability(Action<bool> completed)
        {
            completed(false);
            string current = OnlineVersionAuthenticator.BuildIdentity;
            if (!ValidCommit(current)) yield break;
            using var request = UnityWebRequest.Get(ReleaseApi);
            request.SetRequestHeader("User-Agent", "SB6-Update-Check");
            request.timeout = 10;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
                completed(HasPublishedUpdate(request.downloadHandler.text, current));
        }

        public static bool HasPublishedUpdate(string releaseJson, string current)
        {
            if (!ValidCommit(current) || string.IsNullOrEmpty(releaseJson)) return false;
            try
            {
                string body = JsonUtility.FromJson<Release>(releaseJson)?.body;
                if (string.IsNullOrEmpty(body)) return false;
                const string marker = "<!-- sb6-update:";
                int begin = body.IndexOf(marker, StringComparison.Ordinal);
                int end = begin < 0 ? -1 : body.IndexOf(" -->", begin, StringComparison.Ordinal);
                if (end <= begin) return false;
                var manifest = JsonUtility.FromJson<Manifest>(body.Substring(begin + marker.Length, end - begin - marker.Length));
                return manifest != null && ValidCommit(manifest.sourceCommit) &&
                    !string.Equals(manifest.sourceCommit, current, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
        }

        static bool ValidCommit(string value)
        {
            if (value == null || value.Length != 40) return false;
            foreach (char c in value) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        public static void OpenDiscord() => Application.OpenURL(DiscordInvite);
        public static void Run()
        {
            if (active != null) return;
            var go = new GameObject("GameUpdater");
            DontDestroyOnLoad(go);
            active = go.AddComponent<GameUpdateController>();
            active.CreateView();
            active.StartCoroutine(active.UpdateGame());
        }

        void CreateView()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            gameObject.AddComponent<GraphicRaycaster>();
            var panel = new GameObject("UpdateStatus", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.08f, .25f); rect.anchorMax = new Vector2(.92f, .75f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color32(19, 15, 43, 255);
            var label = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(panel.transform, false);
            var textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 12); textRect.offsetMax = new Vector2(-12, -12);
            status = label.GetComponent<TextMeshProUGUI>();
            status.font = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");
            status.fontSize = Mathf.Max(12, Screen.height / 45f);
            status.color = new Color32(255, 216, 106, 255);
            status.alignment = TextAlignmentOptions.Center;
            status.richText = false;
            LocalizedTmpFontFallback.Apply(status);
            var close = panel.AddComponent<Button>();
            close.onClick.AddListener(() => { if (finished) Destroy(gameObject); });
        }

        void Update()
        {
            if (!finished) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            bool dismiss = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            dismiss |= mouse != null && mouse.leftButton.wasPressedThisFrame;
            dismiss |= touch != null && touch.primaryTouch.press.wasPressedThisFrame;
            dismiss |= PlayerInputManager.Instance != null &&
                (PlayerInputManager.Instance.GetDown(1, PlayerAction.ActionB) || PlayerInputManager.Instance.GetDown(1, PlayerAction.Start));
            if (dismiss) Destroy(gameObject);
        }

        IEnumerator UpdateGame()
        {
            status.text = GameTextDatabase.Online.UpdateChecking;
#if UNITY_EDITOR
            Fail(GameTextDatabase.Online.UpdateBuiltPlayer);
            yield break;
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool windows = Application.platform == RuntimePlatform.WindowsPlayer;
            string helper = Path.Combine(directory, windows ? "GameUpdater.exe" : "GameUpdater.py");
            if (!File.Exists(helper)) { Fail(GameTextDatabase.Online.UpdateMissing); yield break; }
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                string args = "--language " + (int)global::SaveSystem.GetLanguage() + " --wait-for-game " + process.Id;
                var start = new System.Diagnostics.ProcessStartInfo(windows ? helper : "python3",
                    windows ? args : "\"" + helper + "\" " + args)
                { UseShellExecute = windows, WorkingDirectory = directory };
                System.Diagnostics.Process.Start(start);
                Application.Quit();
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception || error is IOException)
            { Debug.LogError("[Updater] " + error.Message); Fail(GameTextDatabase.Online.UpdateFailed); }
            yield break;
#elif UNITY_ANDROID
            using var release = UnityWebRequest.Get(ReleaseApi);
            release.SetRequestHeader("User-Agent", "SB6-Android-Updater");
            release.timeout = 30;
            yield return release.SendWebRequest();
            if (release.result != UnityWebRequest.Result.Success) { Fail(GameTextDatabase.Online.UpdateFailed); yield break; }
            Manifest manifest = null;
            try
            {
                string body = JsonUtility.FromJson<Release>(release.downloadHandler.text).body;
                const string marker = "<!-- sb6-update:";
                int begin = body.IndexOf(marker, StringComparison.Ordinal);
                int end = begin < 0 ? -1 : body.IndexOf(" -->", begin, StringComparison.Ordinal);
                if (end > begin) manifest = JsonUtility.FromJson<Manifest>(body.Substring(begin + marker.Length, end - begin - marker.Length));
            }
            catch (ArgumentException) { }
            if (manifest == null || !TrustedDownload(manifest.androidUrl) || !ValidHash(manifest.androidSha256))
            { Fail(GameTextDatabase.Online.UpdateMissing); yield break; }
            string apk = Path.Combine(Application.temporaryCachePath, "SB6-update.apk");
            using var download = UnityWebRequest.Get(manifest.androidUrl);
            download.downloadHandler = new DownloadHandlerFile(apk) { removeFileOnAbort = true };
            download.timeout = 600;
            var operation = download.SendWebRequest();
            while (!operation.isDone)
            {
                status.text = GameTextDatabase.Online.UpdateDownloading + " " + Mathf.FloorToInt(download.downloadProgress * 100) + "%";
                yield return null;
            }
            if (download.result != UnityWebRequest.Result.Success) { Fail(GameTextDatabase.Online.UpdateFailed); yield break; }
            bool verified;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(apk))
                verified = string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), manifest.androidSha256, StringComparison.OrdinalIgnoreCase);
            if (!verified) { File.Delete(apk); Fail(GameTextDatabase.Online.UpdateFailed); yield break; }
            status.text = GameTextDatabase.Online.UpdateInstall;
            try
            {
                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                using var installer = new AndroidJavaClass("com.mauriciosuporte.sb6update.UpdateActivity");
                bool started = installer.CallStatic<bool>("install", activity, apk);
                if (started) Application.Quit();
                else Fail(GameTextDatabase.Online.UpdatePermission);
            }
            catch (AndroidJavaException error) { Debug.LogError("[Updater] " + error.Message); Fail(GameTextDatabase.Online.UpdateFailed); }
#else
            Fail(GameTextDatabase.Online.UpdateMissing);
            yield break;
#endif
        }

        static bool TrustedDownload(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == "https" && uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/MauricioSuporte/Super-Bomberman-6-Unity/releases/download/", StringComparison.Ordinal);
        static bool ValidHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
        void Fail(string message) { status.text = message + "\n\n" + GameTextDatabase.Online.UpdateDismiss; finished = true; }
        void OnDestroy() { if (active == this) active = null; }
    }
}
