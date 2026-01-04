using UnityEngine;

public static class PlayerPersistentStats
{
    public const int MaxBombAmount = 8;
    public const int MaxExplosionRadius = 8;

    public const int SpeedStep = 32;
    public const int BaseSpeedNormal = 224;

    public const int MaxSpeedUps = 8;
    public const int MaxSpeedInternal = BaseSpeedNormal + (MaxSpeedUps * SpeedStep);
    public const int MinSpeedInternal = BaseSpeedNormal;

    public const int SpeedDivisor = 64;

    public sealed class PlayerState
    {
        public int Life = 1;

        public int BombAmount = 8;
        public int ExplosionRadius = 8;

        public int SpeedInternal = BaseSpeedNormal + (5 * SpeedStep);

        public bool CanKickBombs = true;
        public bool CanPunchBombs = true;
        public bool CanPassBombs = false;
        public bool CanPassDestructibles = true;
        public bool HasPierceBombs = false;
        public bool HasControlBombs = true;
        public bool HasFullFire = false;

        public MountedLouieType MountedLouie = MountedLouieType.None;
        public BomberSkin Skin = BomberSkin.White;
    }

    static readonly PlayerState[] _p = new PlayerState[4]
    {
        new(),
        new(),
        new(),
        new()
    };

    static bool goldenUnlockedSession;
    static bool sessionBooted;

    public static bool GoldenUnlocked => goldenUnlockedSession;

    public static PlayerState Get(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);
        return _p[playerId - 1];
    }

    public static void EnsureSessionBooted()
    {
        if (sessionBooted)
            return;

        goldenUnlockedSession = false;

        for (int i = 1; i <= 4; i++)
        {
            LoadSelectedSkinInternal(i);
            ClampSelectedSkinIfLocked(i);
        }

        sessionBooted = true;
    }

    public static void BootSession()
    {
        sessionBooted = false;
        EnsureSessionBooted();
    }

    public static void UnlockGolden()
    {
        goldenUnlockedSession = true;
    }

    public static bool IsSkinUnlocked(BomberSkin skin)
    {
        if (skin == BomberSkin.Golden)
            return goldenUnlockedSession;

        return true;
    }

    static string PrefSelectedSkin(int playerId) => $"P{playerId}_SKIN_SELECTED";

    public static void SaveSelectedSkin(int playerId)
    {
        var s = Get(playerId);

        if (s.Skin == BomberSkin.Golden)
            return;

        PlayerPrefs.SetInt(PrefSelectedSkin(playerId), (int)s.Skin);
    }

    static void LoadSelectedSkinInternal(int playerId)
    {
        var s = Get(playerId);
        var key = PrefSelectedSkin(playerId);

        if (!PlayerPrefs.HasKey(key))
            return;

        int raw = PlayerPrefs.GetInt(key);
        s.Skin = (BomberSkin)raw;

        if (s.Skin == BomberSkin.Golden)
            s.Skin = BomberSkin.White;
    }

    public static void LoadSelectedSkin(int playerId)
    {
        EnsureSessionBooted();
        LoadSelectedSkinInternal(playerId);
        ClampSelectedSkinIfLocked(playerId);
    }

    public static void ClampSelectedSkinIfLocked(int playerId)
    {
        var s = Get(playerId);

        if (s.Skin == BomberSkin.Golden && !goldenUnlockedSession)
        {
            s.Skin = BomberSkin.White;
            SaveSelectedSkin(playerId);
        }
    }

    public static void ResetToDefaultsAll()
    {
        for (int i = 1; i <= 4; i++)
            ResetToDefaults(i);

        goldenUnlockedSession = false;
        sessionBooted = true;
    }

    public static void ResetToDefaults(int playerId)
    {
        var s = Get(playerId);

        s.BombAmount = 1;
        s.ExplosionRadius = 1;
        s.SpeedInternal = BaseSpeedNormal;

        s.Life = 1;

        s.CanKickBombs = false;
        s.CanPunchBombs = false;
        s.CanPassBombs = false;
        s.CanPassDestructibles = false;
        s.HasPierceBombs = false;
        s.HasControlBombs = false;
        s.HasFullFire = false;

        s.MountedLouie = MountedLouieType.None;

        s.Skin = BomberSkin.White;
        SaveSelectedSkin(playerId);
    }

    public static float InternalSpeedToTilesPerSecond(int internalSpeed)
    {
        return internalSpeed / (float)SpeedDivisor;
    }

    public static int ClampSpeedInternal(int internalSpeed)
    {
        return Mathf.Clamp(internalSpeed, MinSpeedInternal, MaxSpeedInternal);
    }

    static int GetPlayerIdFrom(Component c)
    {
        if (c == null) return 1;

        var id = c.GetComponent<PlayerIdentity>();
        if (id != null) return Mathf.Clamp(id.playerId, 1, 4);

        var parentId = c.GetComponentInParent<PlayerIdentity>(true);
        if (parentId != null) return Mathf.Clamp(parentId.playerId, 1, 4);

        return 1;
    }

    public static void LoadInto(MovementController movement, BombController bomb)
    {
        int playerId = 1;

        if (movement != null) playerId = GetPlayerIdFrom(movement);
        else if (bomb != null) playerId = GetPlayerIdFrom(bomb);

        LoadInto(playerId, movement, bomb);
    }

    public static void LoadInto(int playerId, MovementController movement, BombController bomb)
    {
        var s = Get(playerId);

        s.BombAmount = Mathf.Min(s.BombAmount, MaxBombAmount);
        s.ExplosionRadius = Mathf.Min(s.ExplosionRadius, MaxExplosionRadius);

        s.SpeedInternal = ClampSpeedInternal(s.SpeedInternal);
        s.Life = Mathf.Max(1, s.Life);

        if (movement != null)
            movement.ApplySpeedInternal(s.SpeedInternal);

        if (bomb != null)
        {
            bomb.SetPlayerId(playerId);
            bomb.bombAmout = s.BombAmount;
            bomb.explosionRadius = s.ExplosionRadius;
        }

        if (movement != null && movement.CompareTag("Player"))
        {
            if (movement.TryGetComponent<CharacterHealth>(out var health))
                health.life = Mathf.Max(1, s.Life);

            if (!movement.TryGetComponent<AbilitySystem>(out var abilitySystem))
                abilitySystem = movement.gameObject.AddComponent<AbilitySystem>();

            abilitySystem.RebuildCache();

            if (s.CanKickBombs) abilitySystem.Enable(BombKickAbility.AbilityId);
            else abilitySystem.Disable(BombKickAbility.AbilityId);

            if (s.CanPunchBombs) abilitySystem.Enable(BombPunchAbility.AbilityId);
            else abilitySystem.Disable(BombPunchAbility.AbilityId);

            if (s.HasFullFire) abilitySystem.Enable(FullFireAbility.AbilityId);
            else abilitySystem.Disable(FullFireAbility.AbilityId);

            if (s.CanPassBombs) abilitySystem.Enable(BombPassAbility.AbilityId);
            else abilitySystem.Disable(BombPassAbility.AbilityId);

            if (s.CanPassDestructibles) abilitySystem.Enable(DestructiblePassAbility.AbilityId);
            else abilitySystem.Disable(DestructiblePassAbility.AbilityId);

            if (s.HasControlBombs)
            {
                abilitySystem.Enable(ControlBombAbility.AbilityId);
                abilitySystem.Disable(PierceBombAbility.AbilityId);
            }
            else if (s.HasPierceBombs)
            {
                abilitySystem.Enable(PierceBombAbility.AbilityId);
                abilitySystem.Disable(ControlBombAbility.AbilityId);
            }
            else
            {
                abilitySystem.Disable(PierceBombAbility.AbilityId);
                abilitySystem.Disable(ControlBombAbility.AbilityId);
            }

            if (movement.TryGetComponent<PlayerLouieCompanion>(out var louieCompanion))
            {
                switch (s.MountedLouie)
                {
                    case MountedLouieType.Blue: louieCompanion.RestoreMountedBlueLouie(); break;
                    case MountedLouieType.Black: louieCompanion.RestoreMountedBlackLouie(); break;
                    case MountedLouieType.Purple: louieCompanion.RestoreMountedPurpleLouie(); break;
                    case MountedLouieType.Green: louieCompanion.RestoreMountedGreenLouie(); break;
                    case MountedLouieType.Yellow: louieCompanion.RestoreMountedYellowLouie(); break;
                    case MountedLouieType.Pink: louieCompanion.RestoreMountedPinkLouie(); break;
                    case MountedLouieType.Red: louieCompanion.RestoreMountedRedLouie(); break;
                }
            }
        }
    }

    public static void SaveFrom(MovementController movement, BombController bomb)
    {
        int playerId = 1;

        if (movement != null) playerId = GetPlayerIdFrom(movement);
        else if (bomb != null) playerId = GetPlayerIdFrom(bomb);

        SaveFrom(playerId, movement, bomb);
    }

    public static void SaveFrom(int playerId, MovementController movement, BombController bomb)
    {
        var s = Get(playerId);

        if (movement != null && movement.CompareTag("Player"))
        {
            s.SpeedInternal = ClampSpeedInternal(movement.SpeedInternal);

            if (movement.TryGetComponent<CharacterHealth>(out var health))
                s.Life = Mathf.Max(1, health.life);

            if (movement.TryGetComponent(out AbilitySystem abilitySystem) && abilitySystem != null)
                abilitySystem.RebuildCache();

            var kick = abilitySystem != null ? abilitySystem.Get<BombKickAbility>(BombKickAbility.AbilityId) : null;
            s.CanKickBombs = kick != null && kick.IsEnabled;

            var punch = abilitySystem != null ? abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId) : null;
            s.CanPunchBombs = punch != null && punch.IsEnabled;

            var pierce = abilitySystem != null ? abilitySystem.Get<PierceBombAbility>(PierceBombAbility.AbilityId) : null;
            s.HasPierceBombs = pierce != null && pierce.IsEnabled;

            var control = abilitySystem != null ? abilitySystem.Get<ControlBombAbility>(ControlBombAbility.AbilityId) : null;
            s.HasControlBombs = control != null && control.IsEnabled;

            var fullFire = abilitySystem != null ? abilitySystem.Get<FullFireAbility>(FullFireAbility.AbilityId) : null;
            s.HasFullFire = fullFire != null && fullFire.IsEnabled;

            var passBomb = abilitySystem != null ? abilitySystem.Get<BombPassAbility>(BombPassAbility.AbilityId) : null;
            s.CanPassBombs = passBomb != null && passBomb.IsEnabled;

            var passDestructibles = abilitySystem != null ? abilitySystem.Get<DestructiblePassAbility>(DestructiblePassAbility.AbilityId) : null;
            s.CanPassDestructibles = passDestructibles != null && passDestructibles.IsEnabled;

            if (s.HasControlBombs) s.HasPierceBombs = false;
            else if (s.HasPierceBombs) s.HasControlBombs = false;

            s.MountedLouie = MountedLouieType.None;

            if (movement.TryGetComponent<PlayerLouieCompanion>(out var louieCompanion))
                s.MountedLouie = louieCompanion.GetMountedLouieType();
        }

        if (bomb != null && bomb.CompareTag("Player"))
        {
            s.BombAmount = Mathf.Min(bomb.bombAmout, MaxBombAmount);
            s.ExplosionRadius = Mathf.Min(bomb.explosionRadius, MaxExplosionRadius);
        }
    }
}
