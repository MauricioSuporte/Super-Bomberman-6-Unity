using System.Collections;
using Assets.Scripts.Netcode;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class StageIntroTransition
{
    IEnumerator BattleRoundEntrance()
    {
        if (!NetSync.IsOnline) PlayerPersistentStats.EnsureSessionBooted();
        skipPreIntroWalkNextRound = false;
        skipPreIntroWalkNextRound_Inspector = false;
        StagePreIntroPlayersWalk.ConsumeSkipOnNextLoad();
        IntroRunning = true;
        EndingRunning = false;
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 0f;
        GameMusicController.Instance?.StopMusic();
        if (stageLabel != null) stageLabel.gameObject.SetActive(false);
        if (gameplayRoot != null) gameplayRoot.SetActive(true);
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        HoldBattlePlayers();
        yield return PreloadIntroAudio();
        FindAnyObjectByType<PlayersSpawner>()?.SpawnNow();
        if (!NetSync.IsOnline)
        {
            ResyncSpawnedPlayersFromIdentity();
            ApplyPersistentPlayerSkin();
        }

        for (float elapsed = 0; elapsed < 0.5f; elapsed += Time.unscaledDeltaTime)
        {
            if (fadeImage != null) SetFadeAlpha(1f - Mathf.Clamp01(elapsed / 0.5f));
            yield return null;
        }
        if (fadeImage != null) fadeImage.gameObject.SetActive(false);

        bool graphical = !Application.isBatchMode && !(NetworkManager.singleton is BombermanNetworkManager manager && manager.IsDedicatedServer && !NetworkClient.active);
        GameObject overlay = null;
        RectTransform safe = null;
        Image shade = null, cursor = null;
        TextMeshProUGUI title = null, waiting = null;
        Canvas canvas = null;
        if (graphical)
        {
            overlay = new GameObject("BattleRoundEntrance", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var viewport = PixelPerfectViewport.ResolveFinalPixelRect(Camera.main);
            scaler.scaleFactor = Mathf.Max(1, Mathf.Floor(Mathf.Min(viewport.width / 256f, viewport.height / 224f)));
            safe = new GameObject("SafeFrame", typeof(RectTransform), typeof(UICameraViewportFitter)).GetComponent<RectTransform>();
            safe.SetParent(overlay.transform, false);
            safe.GetComponent<UICameraViewportFitter>().ForceApplyNow();
            shade = new GameObject("Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shade.transform.SetParent(safe, false);
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.sizeDelta = Vector2.zero;
            shade.color = new Color(0, 0, 0, 0.55f); shade.raycastTarget = false;
            title = BattleEntranceText(safe, "StartingMatch", 16);
            waiting = BattleEntranceText(safe, "PleaseWait", -14);
            title.text = GameTextDatabase.Online.StartingMatch;
            waiting.text = GameTextDatabase.Online.PleaseWait;
            if (NetSync.IsOnline)
            {
                cursor = CreateBattlePlayerCursor(safe);
            }
        }

        yield return null; // Let the HUD and spawned visual components finish their first layout.
        bool acknowledged = false;
        int readyFrames = 0;
        double localRelease = Time.realtimeSinceStartupAsDouble + 3;
        while (NetSync.IsOnline && (BombermanNetworkManager.BattleReleaseTime == 0))
        {
            var hud = FindAnyObjectByType<BattleModeHud>();
            var portrait = hud != null ? hud.GetPortraitAnchor(BombermanNetworkManager.LocalRoomPlayerId) : null;
            if (cursor != null && portrait != null)
            {
                var hudCanvas = portrait.GetComponentInParent<Canvas>().rootCanvas;
                var hudCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
                SetEntranceCursorFromScreen(cursor, safe, RectTransformUtility.WorldToScreenPoint(hudCamera, portrait.position));
                UpdateBattleCursorIdentity(cursor);
            }
            if (waiting != null)
            {
                waiting.enabled = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                waiting.rectTransform.anchoredPosition = new Vector2(0, -14 + Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 2f));
            }
            readyFrames = NetworkClient.active && portrait != null && BombermanNetworkManager.HasAllBattlePlayerViews() ? readyFrames + 1 : 0;
            if (!acknowledged && readyFrames >= 2)
            {
                Canvas.ForceUpdateCanvases();
                BombermanNetworkManager.AcknowledgeBattleScreen();
                acknowledged = true;
            }
            yield return null;
        }

        double release = NetSync.IsOnline ? BombermanNetworkManager.BattleReleaseTime : localRelease;
        if (graphical)
        {
            shade.color = new Color(0, 0, 0, 0.2f);
            waiting.enabled = false;
            title.fontSize = 18;
            title.rectTransform.anchoredPosition = Vector2.zero;
        }
        Vector2 cursorOrigin = cursor != null ? cursor.rectTransform.anchoredPosition : Vector2.zero;
        double cursorStartedAt = Time.realtimeSinceStartupAsDouble;
        while ((NetSync.IsOnline ? NetworkTime.time : Time.realtimeSinceStartupAsDouble) < release)
        {
            double now = NetSync.IsOnline ? NetworkTime.time : Time.realtimeSinceStartupAsDouble;
            if (title != null) title.text = Mathf.Clamp(Mathf.CeilToInt((float)(release - now)), 1, 3).ToString();
            if (cursor != null)
            {
                float cursorElapsed = (float)(Time.realtimeSinceStartupAsDouble - cursorStartedAt);
                var player = NetworkClient.localPlayer;
                if (player != null && Camera.main != null)
                {
                    var viewport = PixelPerfectViewport.ResolveFinalPixelRect(Camera.main);
                    var point = Camera.main.WorldToViewportPoint(player.transform.position + Vector3.up * 1.25f);
                    var screen = new Vector2(viewport.x + point.x * viewport.width, viewport.y + point.y * viewport.height);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var destination);
                    Vector2 position = Vector2.Lerp(cursorOrigin, destination, Mathf.Clamp01(cursorElapsed / 0.37f));
                    cursor.rectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
                }
                cursor.GetComponent<CanvasGroup>().alpha = cursorElapsed < 0.37f || (cursorElapsed < 1.37f && Mathf.Repeat(cursorElapsed - 0.37f, 0.2f) < 0.12f) ? 1f : 0f;
            }
            yield return null;
        }
        if (graphical) PlayBattleStartVoice();
        if (overlay != null) Destroy(overlay);
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        EnableGameplay();
        TryStartDefaultMusicNormalFlow();
    }

    void PlayBattleStartVoice()
    {
        EnsureStartClipLoaded();
        if (!playStartSfxOnIntroEnd || s_startSfxClip == null) return;
        // The sound outlives the entrance overlay and plays once on each graphical peer.
        var sound = new GameObject("BattleStartVoice", typeof(AudioSource));
        var source = sound.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.PlayOneShot(s_startSfxClip, GameAudioSettings.ApplyVoiceSfxVolume(startSfxVolume));
        Destroy(sound, s_startSfxClip.length + 0.25f);
    }

    static Image CreateBattlePlayerCursor(RectTransform parent)
    {
        var cursor = new GameObject("LocalPlayerCursor", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        cursor.transform.SetParent(parent, false);
        cursor.rectTransform.sizeDelta = new Vector2(16, 18);
        cursor.color = Color.clear;
        cursor.raycastTarget = false;
        var letter = CreateBattleCursorPart(cursor.transform, "Letter", new Vector2(-3, 3), new Vector2(8, 8));
        letter.sprite = Resources.Load<Sprite>("Sprites/CharacterSelect/Cursor/P");
        CreateBattleCursorPart(cursor.transform, "Number", new Vector2(3, 3), new Vector2(8, 8));
        for (int row = 0; row < 3; row++)
        {
            var arrow = CreateBattleCursorPart(cursor.transform, "Arrow" + row, new Vector2(0, -3 - row), new Vector2(5 - row * 2, 1));
            arrow.color = new Color32(255, 216, 106, 255);
        }
        UpdateBattleCursorIdentity(cursor);
        return cursor;
    }

    static Image CreateBattleCursorPart(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var part = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Shadow)).GetComponent<Image>();
        part.transform.SetParent(parent, false);
        part.rectTransform.anchoredPosition = position;
        part.rectTransform.sizeDelta = size;
        part.raycastTarget = false;
        part.GetComponent<Shadow>().effectDistance = new Vector2(1, -1);
        return part;
    }

    static void UpdateBattleCursorIdentity(Image cursor)
    {
        int id = BombermanNetworkManager.LocalRoomPlayerId;
        var number = cursor.transform.Find("Number").GetComponent<Image>();
        number.sprite = id >= 1 && id <= 6 ? Resources.Load<Sprite>("Sprites/CharacterSelect/Cursor/" + id) : null;
        cursor.GetComponent<CanvasGroup>().alpha = number.sprite != null ? 1f : 0f;
    }

    void Update()
    {
        if (IsBattleModeScene() && IntroRunning) HoldBattlePlayers();
    }

    void HoldBattlePlayers()
    {
        Time.timeScale = 0f;
        RefreshControllers(includeInactive: false);
        foreach (var movement in movementControllers)
            if (movement != null && movement.GetComponent<PlayerIdentity>() != null)
                movement.SetInputLocked(true, true);
    }

    static TextMeshProUGUI BattleEntranceText(Transform parent, string name, float y)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.rectTransform.sizeDelta = new Vector2(244, 24);
        text.rectTransform.anchoredPosition = new Vector2(0, y);
        text.font = Resources.Load<TMP_FontAsset>("Fonts/Retro Gaming SDF");
        text.fontSize = 8;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        LocalizedTmpFontFallback.Apply(text);
        return text;
    }

    static void SetEntranceCursorFromScreen(Image cursor, RectTransform safe, Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var point);
        cursor.rectTransform.anchoredPosition = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
    }
}
