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

    public static int Life = 1;

    public static int BombAmount = 8;
    public static int ExplosionRadius = 8;

    //public static int SpeedInternal = BaseSpeedNormal;
    public static int SpeedInternal = BaseSpeedNormal + (5 * SpeedStep);

    public static bool CanKickBombs = true;
    public static bool CanPunchBombs = true;
    public static bool CanPassBombs = false;
    public static bool CanPassDestructibles = true;
    public static bool HasPierceBombs = true;
    public static bool HasControlBombs = false;
    public static bool HasFullFire = false;

    public static MountedLouieType MountedLouie = MountedLouieType.Yellow;
    public static BomberSkin Skin = BomberSkin.Yellow;

    const string PrefGoldenUnlocked = "SKIN_GOLDEN_UNLOCKED";
    const string PrefSelectedSkin = "SKIN_SELECTED";

    public static float InternalSpeedToTilesPerSecond(int internalSpeed)
    {
        return internalSpeed / (float)SpeedDivisor;
    }

    public static int ClampSpeedInternal(int internalSpeed)
    {
        return Mathf.Clamp(internalSpeed, MinSpeedInternal, MaxSpeedInternal);
    }

    public static void LoadInto(MovementController movement, BombController bomb)
    {
        BombAmount = Mathf.Min(BombAmount, MaxBombAmount);
        ExplosionRadius = Mathf.Min(ExplosionRadius, MaxExplosionRadius);

        SpeedInternal = ClampSpeedInternal(SpeedInternal);
        Life = Mathf.Max(1, Life);

        if (movement != null)
            movement.ApplySpeedInternal(SpeedInternal);

        if (bomb != null)
        {
            bomb.bombAmout = BombAmount;
            bomb.explosionRadius = ExplosionRadius;
        }

        if (movement != null && movement.CompareTag("Player"))
        {
            if (movement.TryGetComponent<CharacterHealth>(out var health))
                health.life = Mathf.Max(1, Life);

            if (!movement.TryGetComponent<AbilitySystem>(out var abilitySystem))
                abilitySystem = movement.gameObject.AddComponent<AbilitySystem>();

            abilitySystem.RebuildCache();

            if (CanKickBombs) abilitySystem.Enable(BombKickAbility.AbilityId);
            else abilitySystem.Disable(BombKickAbility.AbilityId);

            if (CanPunchBombs) abilitySystem.Enable(BombPunchAbility.AbilityId);
            else abilitySystem.Disable(BombPunchAbility.AbilityId);

            if (HasFullFire) abilitySystem.Enable(FullFireAbility.AbilityId);
            else abilitySystem.Disable(FullFireAbility.AbilityId);

            if (CanPassBombs) abilitySystem.Enable(BombPassAbility.AbilityId);
            else abilitySystem.Disable(BombPassAbility.AbilityId);

            if (CanPassDestructibles) abilitySystem.Enable(DestructiblePassAbility.AbilityId);
            else abilitySystem.Disable(DestructiblePassAbility.AbilityId);

            if (HasControlBombs)
            {
                abilitySystem.Enable(ControlBombAbility.AbilityId);
                abilitySystem.Disable(PierceBombAbility.AbilityId);
            }
            else if (HasPierceBombs)
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
                switch (MountedLouie)
                {
                    case MountedLouieType.Blue:
                        louieCompanion.RestoreMountedBlueLouie();
                        break;

                    case MountedLouieType.Black:
                        louieCompanion.RestoreMountedBlackLouie();
                        break;

                    case MountedLouieType.Purple:
                        louieCompanion.RestoreMountedPurpleLouie();
                        break;

                    case MountedLouieType.Green:
                        louieCompanion.RestoreMountedGreenLouie();
                        break;

                    case MountedLouieType.Yellow:
                        louieCompanion.RestoreMountedYellowLouie();
                        break;

                    case MountedLouieType.Pink:
                        louieCompanion.RestoreMountedPinkLouie();
                        break;

                    case MountedLouieType.Red:
                        louieCompanion.RestoreMountedRedLouie();
                        break;
                }
            }
        }
    }

    public static void SaveFrom(MovementController movement, BombController bomb)
    {
        if (movement != null && movement.CompareTag("Player"))
        {
            SpeedInternal = ClampSpeedInternal(movement.SpeedInternal);

            if (movement.TryGetComponent<CharacterHealth>(out var health))
                Life = Mathf.Max(1, health.life);

            if (movement.TryGetComponent(out AbilitySystem abilitySystem))
                abilitySystem.RebuildCache();

            var kick = abilitySystem != null ? abilitySystem.Get<BombKickAbility>(BombKickAbility.AbilityId) : null;
            CanKickBombs = kick != null && kick.IsEnabled;

            var punch = abilitySystem != null ? abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId) : null;
            CanPunchBombs = punch != null && punch.IsEnabled;

            var pierce = abilitySystem != null ? abilitySystem.Get<PierceBombAbility>(PierceBombAbility.AbilityId) : null;
            HasPierceBombs = pierce != null && pierce.IsEnabled;

            var control = abilitySystem != null ? abilitySystem.Get<ControlBombAbility>(ControlBombAbility.AbilityId) : null;
            HasControlBombs = control != null && control.IsEnabled;

            var fullFire = abilitySystem != null ? abilitySystem.Get<FullFireAbility>(FullFireAbility.AbilityId) : null;
            HasFullFire = fullFire != null && fullFire.IsEnabled;

            var passBomb = abilitySystem != null ? abilitySystem.Get<BombPassAbility>(BombPassAbility.AbilityId) : null;
            CanPassBombs = passBomb != null && passBomb.IsEnabled;

            var passDestructibles = abilitySystem != null ? abilitySystem.Get<DestructiblePassAbility>(DestructiblePassAbility.AbilityId) : null;
            CanPassDestructibles = passDestructibles != null && passDestructibles.IsEnabled;

            if (HasControlBombs)
                HasPierceBombs = false;
            else if (HasPierceBombs)
                HasControlBombs = false;

            MountedLouie = MountedLouieType.None;

            if (movement.TryGetComponent<PlayerLouieCompanion>(out var louieCompanion))
                MountedLouie = louieCompanion.GetMountedLouieType();
        }

        if (bomb != null && bomb.CompareTag("Player"))
        {
            BombAmount = Mathf.Min(bomb.bombAmout, MaxBombAmount);
            ExplosionRadius = Mathf.Min(bomb.explosionRadius, MaxExplosionRadius);
        }
    }

    public static void ResetToDefaults()
    {
        BombAmount = 1;
        ExplosionRadius = 1;
        SpeedInternal = BaseSpeedNormal;

        Life = 1;

        CanKickBombs = false;
        CanPunchBombs = false;
        CanPassBombs = false;
        CanPassDestructibles = false;
        HasPierceBombs = false;
        HasControlBombs = false;
        HasFullFire = false;

        MountedLouie = MountedLouieType.None;
    }

    public static bool GoldenUnlocked
    {
        get => PlayerPrefs.GetInt(PrefGoldenUnlocked, 0) == 1;
        set { PlayerPrefs.SetInt(PrefGoldenUnlocked, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool IsSkinUnlocked(BomberSkin skin)
    {
        if (skin == BomberSkin.Golden)
            return GoldenUnlocked;

        return true;
    }

    public static void SaveSelectedSkin()
    {
        PlayerPrefs.SetInt(PrefSelectedSkin, (int)Skin);
        PlayerPrefs.Save();
    }

    public static void LoadSelectedSkin()
    {
        if (PlayerPrefs.HasKey(PrefSelectedSkin))
            Skin = (BomberSkin)PlayerPrefs.GetInt(PrefSelectedSkin);
    }
}
