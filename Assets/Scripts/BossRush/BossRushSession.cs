using System;
using UnityEngine;

public static class BossRushSession
{
    static readonly string[] stageOrder =
    {
        "Stage_1-6",
        "Stage_1-7",
        "Stage_2-6",
        "Stage_2-7"
    };

    static bool active;
    static bool timerPaused;
    static int currentStageIndex;
    static BossRushDifficulty selectedDifficulty;
    static BossRushLoadoutPreset selectedPreset;

    static float elapsedSeconds;

    static bool hasLastCompletedRun;
    static BossRushDifficulty lastCompletedDifficulty;
    static float lastCompletedTime;
    static int lastCompletedRank = -1;

    static int runPlayerMask = GameSession.CreateMaskFromCount(1);
    static readonly bool[] eliminatedPlayers = new bool[GameSession.MaxPlayerId + 1];

    public static bool IsActive => active;
    public static bool HasLastCompletedRun => hasLastCompletedRun;
    public static BossRushDifficulty CurrentDifficulty => selectedDifficulty;
    public static BossRushDifficulty LastCompletedDifficulty => lastCompletedDifficulty;
    public static float LastCompletedTime => lastCompletedTime;
    public static int LastCompletedRank => lastCompletedRank;
    public static int RunPlayerCount => GameSession.CountPlayersInMask(runPlayerMask);
    public static int RunPlayerMask => runPlayerMask;

    public static void StartRun(BossRushDifficulty difficulty, BossRushLoadoutPreset preset)
    {
        PlayerPersistentStats.EnsureSessionBooted();

        active = true;
        timerPaused = false;
        selectedDifficulty = difficulty;
        selectedPreset = preset;
        currentStageIndex = 0;
        elapsedSeconds = 0f;

        hasLastCompletedRun = false;
        lastCompletedRank = -1;
        lastCompletedTime = 0f;

        runPlayerMask = GameSession.CreateMaskFromCount(1);
        if (GameSession.Instance != null)
            runPlayerMask = SanitizeRunPlayerMask(GameSession.Instance.ActivePlayerMask);

        ClearEliminatedPlayers();
        ApplySelectedLoadoutToRunPlayers();
    }

    public static void CancelRun()
    {
        ResetAllRunPlayers();
        ClearRuntimeState();
    }

    public static void PauseTimer()
    {
        if (!active)
            return;

        timerPaused = true;
    }

    public static int CompleteRunAndStoreTime()
    {
        if (!active)
            return -1;

        timerPaused = true;

        BossRushDifficulty completedDifficulty = selectedDifficulty;
        float completedTime = elapsedSeconds;
        int rank = BossRushTimesProgress.RegisterTime(completedDifficulty, completedTime);

        UnlockCompletionRewardSkinForDifficulty(completedDifficulty);
        UnlockTimeTargetRewardSkinIfNeeded(completedDifficulty, completedTime);
        UnlockNightmareIfNeeded(completedDifficulty);

        hasLastCompletedRun = true;
        lastCompletedDifficulty = completedDifficulty;
        lastCompletedTime = completedTime;
        lastCompletedRank = rank;

        ResetAllRunPlayers();
        ClearRuntimeState(keepLastCompletedRun: true);
        return rank;
    }

    public static void ClearLastCompletedRun()
    {
        hasLastCompletedRun = false;
        lastCompletedTime = 0f;
        lastCompletedRank = -1;
    }

    public static void AddElapsed(float deltaSeconds)
    {
        if (!active || timerPaused)
            return;

        if (deltaSeconds <= 0f)
            return;

        elapsedSeconds += deltaSeconds;
    }

    public static string GetFormattedElapsed()
    {
        if (elapsedSeconds < 0f)
            elapsedSeconds = 0f;

        int totalCentiseconds = Mathf.FloorToInt(elapsedSeconds * 100f);
        int minutes = totalCentiseconds / 6000;
        int seconds = (totalCentiseconds / 100) % 60;
        int centiseconds = totalCentiseconds % 100;

        return $"{minutes:00}:{seconds:00}.{centiseconds:00}";
    }

    public static string GetCurrentStageSceneName()
    {
        if (stageOrder.Length == 0)
            return string.Empty;

        currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stageOrder.Length - 1);
        return stageOrder[currentStageIndex];
    }

    public static bool TryAdvanceToNextStage(out string nextSceneName)
    {
        nextSceneName = null;

        if (!active)
            return false;

        int nextIndex = currentStageIndex + 1;
        if (nextIndex >= stageOrder.Length)
            return false;

        currentStageIndex = nextIndex;
        nextSceneName = stageOrder[currentStageIndex];
        return true;
    }

    public static bool IsBossRushScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        for (int i = 0; i < stageOrder.Length; i++)
        {
            if (string.Equals(stageOrder[i], sceneName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static void NotifySceneLoaded(string sceneName)
    {
        if (!active || string.IsNullOrWhiteSpace(sceneName))
            return;

        for (int i = 0; i < stageOrder.Length; i++)
        {
            if (string.Equals(stageOrder[i], sceneName, StringComparison.Ordinal))
            {
                currentStageIndex = i;
                timerPaused = false;
                return;
            }
        }
    }

    public static bool IsRunPlayer(int playerId)
    {
        if (!GameSession.IsValidPlayerId(playerId))
            return false;

        return (runPlayerMask & PlayerIdToMask(playerId)) != 0;
    }

    public static void GetRunPlayerIds(System.Collections.Generic.List<int> results)
    {
        if (results == null)
            return;

        results.Clear();

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (IsRunPlayer(playerId))
                results.Add(playerId);
        }
    }

    public static bool ShouldSpawnPlayer(int playerId)
    {
        playerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        if (!active)
            return true;

        if (!IsRunPlayer(playerId))
            return false;

        return !eliminatedPlayers[playerId];
    }

    public static bool IsPlayerEliminated(int playerId)
    {
        playerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        if (!IsRunPlayer(playerId))
            return true;

        return eliminatedPlayers[playerId];
    }

    public static void MarkPlayerEliminated(int playerId)
    {
        if (!active)
            return;

        playerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        if (!IsRunPlayer(playerId))
            return;

        eliminatedPlayers[playerId] = true;

        var state = PlayerPersistentStats.Get(playerId);
        if (state != null)
            state.Life = 0;
    }

    public static void CapturePlayerSurvivalStateFromScene()
    {
        if (!active)
            return;

        if (RunPlayerCount <= 1)
            return;

        bool[] aliveFlags = new bool[GameSession.MaxPlayerId + 1];

        var identities = UnityEngine.Object.FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include);

        for (int i = 0; i < identities.Length; i++)
        {
            var identity = identities[i];
            if (identity == null)
                continue;

            int playerId = Mathf.Clamp(identity.playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
            if (!IsRunPlayer(playerId))
                continue;

            MovementController movement = null;

            if (!identity.TryGetComponent(out movement))
                movement = identity.GetComponentInChildren<MovementController>(true);

            bool alive =
                movement != null &&
                movement.gameObject.activeInHierarchy &&
                !movement.isDead;

            if (alive)
                aliveFlags[playerId] = true;
        }

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (IsRunPlayer(playerId) && !aliveFlags[playerId])
                MarkPlayerEliminated(playerId);
        }
    }

    static void ApplySelectedLoadoutToRunPlayers()
    {
        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (!IsRunPlayer(playerId))
                continue;

            var state = PlayerPersistentStats.Get(playerId);
            if (state == null)
                continue;

            BomberCharacter preservedCharacter = state.Character;
            BomberSkin preservedSkin = state.Skin;

            state.Life = 1;
            state.BombAmount = 1;
            state.ExplosionRadius = 1;
            state.SpeedInternal = PlayerPersistentStats.BaseSpeedNormal;

            state.CanKickBombs = false;
            state.CanPunchBombs = false;
            state.HasPowerGlove = false;
            state.CanPassBombs = false;
            state.CanPassDestructibles = false;
            state.HasPierceBombs = false;
            state.HasControlBombs = false;
            state.HasPowerBomb = false;
            state.HasRubberBombs = false;
            state.HasMagnetBomb = false;
            state.HasFullFire = false;
            state.MountedLouie = MountedType.None;
            state.QueuedEggs.Clear();

            selectedPreset?.ApplyTo(state);

            state.Character = preservedCharacter;
            state.Skin = preservedSkin;
        }
    }

    static void UnlockCompletionRewardSkinForDifficulty(BossRushDifficulty difficulty)
    {
        BomberSkin? rewardSkin = GetCompletionRewardSkinForDifficulty(difficulty);
        if (!rewardSkin.HasValue)
            return;

        bool unlocked = UnlockProgress.Unlock(rewardSkin.Value);

        if (unlocked)
        {
            for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
                PlayerPersistentStats.ClampSelectedSkinIfLocked(playerId);
        }
    }

    static void UnlockTimeTargetRewardSkinIfNeeded(BossRushDifficulty difficulty, float completedTime)
    {
        if (!BossRushTimesProgress.MeetsUnlockTarget(difficulty, completedTime))
            return;

        BomberSkin? rewardSkin = GetTimeTargetRewardSkinForDifficulty(difficulty);
        if (!rewardSkin.HasValue)
            return;

        bool unlocked = UnlockProgress.Unlock(rewardSkin.Value);

        if (unlocked)
        {
            for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
                PlayerPersistentStats.ClampSelectedSkinIfLocked(playerId);
        }
    }

    static void UnlockNightmareIfNeeded(BossRushDifficulty difficulty)
    {
        if (difficulty != BossRushDifficulty.HARD)
            return;

        bool unlockedNow = BossRushDifficultyUnlocks.UnlockNightmare();
        if (!unlockedNow)
            return;

        UnlockToastPresenter.ShowNightmareUnlocked();
    }

    static BomberSkin? GetCompletionRewardSkinForDifficulty(BossRushDifficulty difficulty)
    {
        return difficulty switch
        {
            BossRushDifficulty.EASY => (BomberSkin?)BomberSkin.Palette18,
            BossRushDifficulty.NORMAL => (BomberSkin?)BomberSkin.Palette14,
            BossRushDifficulty.HARD => (BomberSkin?)BomberSkin.Palette10,
            BossRushDifficulty.NIGHTMARE => (BomberSkin?)BomberSkin.Palette21,
            _ => null,
        };
    }

    static BomberSkin? GetTimeTargetRewardSkinForDifficulty(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.EASY:
                return BomberSkin.Palette11;

            case BossRushDifficulty.NORMAL:
                return BomberSkin.Palette12;

            case BossRushDifficulty.HARD:
                return BomberSkin.Palette8;

            default:
                return null;
        }
    }

    static void ClearEliminatedPlayers()
    {
        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
            eliminatedPlayers[playerId] = false;
    }

    static void ClearRuntimeState(bool keepLastCompletedRun = false)
    {
        active = false;
        timerPaused = false;
        currentStageIndex = 0;
        selectedPreset = null;
        elapsedSeconds = 0f;
        runPlayerMask = GameSession.CreateMaskFromCount(1);

        ClearEliminatedPlayers();

        if (!keepLastCompletedRun)
        {
            hasLastCompletedRun = false;
            lastCompletedTime = 0f;
            lastCompletedRank = -1;
        }
    }

    static void ResetAllRunPlayers()
    {
        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (!IsRunPlayer(playerId))
                continue;

            var state = PlayerPersistentStats.Get(playerId);
            if (state == null)
                continue;

            BomberCharacter preservedCharacter = state.Character;
            BomberSkin preservedSkin = state.Skin;

            state.Life = 1;
            state.BombAmount = 1;
            state.ExplosionRadius = 1;
            state.SpeedInternal = PlayerPersistentStats.BaseSpeedNormal;

            state.CanKickBombs = false;
            state.CanPunchBombs = false;
            state.HasPowerGlove = false;
            state.CanPassBombs = false;
            state.CanPassDestructibles = false;
            state.HasPierceBombs = false;
            state.HasControlBombs = false;
            state.HasPowerBomb = false;
            state.HasRubberBombs = false;
            state.HasMagnetBomb = false;
            state.HasFullFire = false;
            state.MountedLouie = MountedType.None;
            state.QueuedEggs.Clear();

            state.Character = preservedCharacter;
            state.Skin = preservedSkin;
        }
    }

    static int SanitizeRunPlayerMask(int mask)
    {
        int sanitizedMask = mask & ((1 << GameSession.MaxPlayerId) - 1);

        if (sanitizedMask == 0)
            sanitizedMask = GameSession.CreateMaskFromCount(1);

        return sanitizedMask;
    }

    static int PlayerIdToMask(int playerId)
    {
        int clampedPlayerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        return 1 << (clampedPlayerId - 1);
    }
}
