using System.Collections.Generic;
using UnityEngine;

public static class PlayerPersistentStats
{
    public const int MaxBombAmount = 8;
    public const int MaxExplosionRadius = 10;

    public const int SpeedStep = 32;
    public const int BaseSpeedNormal = 224;

    public const int MaxSpeedUps = 8;
    public const int MaxSpeedInternal = BaseSpeedNormal + (MaxSpeedUps * SpeedStep);
    public const int MinSpeedInternal = BaseSpeedNormal;

    public const int SpeedDivisor = 64;

    public sealed class PlayerState
    {
        public int Life = 9;

        public int BombAmount = 9;
        public int ExplosionRadius = 9;

        public int SpeedInternal = MaxSpeedInternal;

        public bool CanKickBombs = true;
        public bool CanPunchBombs = true;
        public bool CanPassBombs = false;
        public bool CanPassDestructibles = true;
        public bool HasPierceBombs = false;
        public bool HasControlBombs = true;
        public bool HasFullFire = false;

        public MountedType MountedLouie = MountedType.Mole;
        public BomberSkin Skin = BomberSkin.White;

        public readonly List<ItemPickup.ItemType> QueuedEggs = new(8);

        public PlayerState()
        {
            QueuedEggs.Clear();
            //QueuedEggs.Add(ItemPickup.ItemType.BlackLouieEgg);
            //QueuedEggs.Add(ItemPickup.ItemType.PurpleLouieEgg);
        }
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

        s.MountedLouie = MountedType.None;
        s.QueuedEggs.Clear();

        s.Skin = BomberSkin.White;
        SaveSelectedSkin(playerId);
    }

    static void ResetTemporaryPowerups(PlayerState s)
    {
        if (s == null) return;

        s.CanKickBombs = false;
        s.CanPunchBombs = false;
        s.CanPassBombs = false;
        s.CanPassDestructibles = false;

        s.HasPierceBombs = false;
        s.HasControlBombs = false;
        s.HasFullFire = false;

        s.MountedLouie = MountedType.None;

        s.QueuedEggs?.Clear();
    }

    public static void ResetTemporaryPowerups(int playerId)
    {
        var s = Get(playerId);
        ResetTemporaryPowerups(s);
    }

    public static void StageResetTemporaryPowerupsOnDeath(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);
        BeginStage();

        var s = _stage[playerId - 1];
        ResetTemporaryPowerups(s);
    }

    public static float InternalSpeedToTilesPerSecond(int internalSpeed)
        => internalSpeed / (float)SpeedDivisor;

    public static int ClampSpeedInternal(int internalSpeed)
        => Mathf.Clamp(internalSpeed, MinSpeedInternal, MaxSpeedInternal);

    static int GetPlayerIdFrom(Component c)
    {
        if (c == null) return 1;

        if (c.TryGetComponent<PlayerIdentity>(out var id)) return Mathf.Clamp(id.playerId, 1, 4);

        var parentId = c.GetComponentInParent<PlayerIdentity>(true);
        if (parentId != null) return Mathf.Clamp(parentId.playerId, 1, 4);

        return 1;
    }

    static MountEggQueue GetOrCreateEggQueue(GameObject playerGo, MovementController movement)
    {
        if (playerGo == null)
            return null;

        if (!playerGo.TryGetComponent<MountEggQueue>(out var q) || q == null)
            q = playerGo.AddComponent<MountEggQueue>();

        if (movement != null)
            q.BindOwner(movement);

        return q;
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

            if (movement.TryGetComponent<PlayerMountCompanion>(out var louieCompanion))
            {
                switch (s.MountedLouie)
                {
                    case MountedType.Blue: louieCompanion.RestoreMountedBlueLouie(); break;
                    case MountedType.Black: louieCompanion.RestoreMountedBlackLouie(); break;
                    case MountedType.Purple: louieCompanion.RestoreMountedPurpleLouie(); break;
                    case MountedType.Green: louieCompanion.RestoreMountedGreenLouie(); break;
                    case MountedType.Yellow: louieCompanion.RestoreMountedYellowLouie(); break;
                    case MountedType.Pink: louieCompanion.RestoreMountedPinkLouie(); break;
                    case MountedType.Red: louieCompanion.RestoreMountedRedLouie(); break;
                    case MountedType.Mole: louieCompanion.RestoreMountedMole(); break;
                }
            }

            if (s.QueuedEggs != null && s.QueuedEggs.Count > 0)
            {
                var q = GetOrCreateEggQueue(movement.gameObject, movement);
                if (q != null)
                    q.RestoreQueuedEggTypesOldestToNewest(s.QueuedEggs, idleSpriteFallback: null);
            }
            else
            {
                if (movement.TryGetComponent<MountEggQueue>(out var q) && q != null)
                    q.RestoreQueuedEggTypesOldestToNewest(null, null);
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

            AbilitySystem abilitySystem = null;
            if (movement.TryGetComponent(out AbilitySystem a) && a != null)
            {
                abilitySystem = a;
                abilitySystem.RebuildCache();
            }

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

            s.MountedLouie = MountedType.None;

            if (movement.TryGetComponent<PlayerMountCompanion>(out var louieCompanion))
                s.MountedLouie = louieCompanion.GetMountedLouieType();

            if (movement.TryGetComponent<MountEggQueue>(out var q) && q != null)
                q.GetQueuedEggTypesOldestToNewest(s.QueuedEggs);
            else
                s.QueuedEggs.Clear();
        }

        if (bomb != null && bomb.CompareTag("Player"))
        {
            s.BombAmount = Mathf.Min(bomb.bombAmout, MaxBombAmount);
            s.ExplosionRadius = Mathf.Min(bomb.explosionRadius, MaxExplosionRadius);
        }
    }

    public static void SavePermanentFrom(int playerId, MovementController movement, BombController bomb, CharacterHealth health)
    {
        var s = Get(playerId);

        if (movement != null)
            s.SpeedInternal = ClampSpeedInternal(movement.SpeedInternal);

        if (bomb != null)
        {
            s.BombAmount = Mathf.Min(bomb.bombAmout, MaxBombAmount);
            s.ExplosionRadius = Mathf.Min(bomb.explosionRadius, MaxExplosionRadius);
        }

        if (health != null)
            s.Life = Mathf.Max(1, health.life);

        if (movement != null && movement.TryGetComponent<MountEggQueue>(out var q) && q != null)
            q.GetQueuedEggTypesOldestToNewest(s.QueuedEggs);
    }

    static readonly PlayerState[] _stage = new PlayerState[4]
    {
        new(),
        new(),
        new(),
        new()
    };

    static bool stageActive;

    static void CopyState(PlayerState from, PlayerState to)
    {
        if (from == null || to == null) return;

        to.Life = from.Life;

        to.BombAmount = from.BombAmount;
        to.ExplosionRadius = from.ExplosionRadius;

        to.SpeedInternal = from.SpeedInternal;

        to.CanKickBombs = from.CanKickBombs;
        to.CanPunchBombs = from.CanPunchBombs;
        to.CanPassBombs = from.CanPassBombs;
        to.CanPassDestructibles = from.CanPassDestructibles;
        to.HasPierceBombs = from.HasPierceBombs;
        to.HasControlBombs = from.HasControlBombs;
        to.HasFullFire = from.HasFullFire;

        to.MountedLouie = from.MountedLouie;
        to.Skin = from.Skin;

        to.QueuedEggs.Clear();
        if (from.QueuedEggs != null && from.QueuedEggs.Count > 0)
            to.QueuedEggs.AddRange(from.QueuedEggs);
    }

    public static void BeginStage()
    {
        if (stageActive)
            return;

        for (int i = 1; i <= 4; i++)
        {
            var baseState = Get(i);
            var st = _stage[i - 1];
            CopyState(baseState, st);
        }

        stageActive = true;
    }

    public static void DiscardStage()
    {
        stageActive = false;
    }

    public static void CommitStage()
    {
        if (!stageActive)
            return;

        for (int i = 1; i <= 4; i++)
        {
            var baseState = Get(i);
            var st = _stage[i - 1];
            CopyState(st, baseState);
        }

        stageActive = false;
    }

    static MountedType EggToLouie(ItemPickup.ItemType t)
    {
        return t switch
        {
            ItemPickup.ItemType.BlueLouieEgg => MountedType.Blue,
            ItemPickup.ItemType.BlackLouieEgg => MountedType.Black,
            ItemPickup.ItemType.PurpleLouieEgg => MountedType.Purple,
            ItemPickup.ItemType.GreenLouieEgg => MountedType.Green,
            ItemPickup.ItemType.YellowLouieEgg => MountedType.Yellow,
            ItemPickup.ItemType.PinkLouieEgg => MountedType.Pink,
            ItemPickup.ItemType.RedLouieEgg => MountedType.Red,
            _ => MountedType.None
        };
    }

    static bool IsEgg(ItemPickup.ItemType t)
    {
        return t == ItemPickup.ItemType.BlueLouieEgg
            || t == ItemPickup.ItemType.BlackLouieEgg
            || t == ItemPickup.ItemType.PurpleLouieEgg
            || t == ItemPickup.ItemType.GreenLouieEgg
            || t == ItemPickup.ItemType.YellowLouieEgg
            || t == ItemPickup.ItemType.PinkLouieEgg
            || t == ItemPickup.ItemType.RedLouieEgg;
    }

    public static void StageApplyPickup(int playerId, ItemPickup.ItemType type)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);
        BeginStage();

        var s = _stage[playerId - 1];

        switch (type)
        {
            case ItemPickup.ItemType.ExtraBomb:
                s.BombAmount = Mathf.Min(s.BombAmount + 1, MaxBombAmount);
                break;

            case ItemPickup.ItemType.BlastRadius:
                s.ExplosionRadius = Mathf.Min(s.ExplosionRadius + 1, MaxExplosionRadius);
                break;

            case ItemPickup.ItemType.SpeedIncrese:
                s.SpeedInternal = ClampSpeedInternal(s.SpeedInternal + SpeedStep);
                break;

            case ItemPickup.ItemType.BombKick:
                s.CanKickBombs = true;
                break;

            case ItemPickup.ItemType.BombPunch:
                s.CanPunchBombs = true;
                break;

            case ItemPickup.ItemType.FullFire:
                s.HasFullFire = true;
                break;

            case ItemPickup.ItemType.BombPass:
                s.CanPassBombs = true;
                break;

            case ItemPickup.ItemType.DestructiblePass:
                s.CanPassDestructibles = true;
                break;

            case ItemPickup.ItemType.PierceBomb:
                s.HasPierceBombs = true;
                s.HasControlBombs = false;
                break;

            case ItemPickup.ItemType.ControlBomb:
                s.HasControlBombs = true;
                s.HasPierceBombs = false;
                break;

            case ItemPickup.ItemType.Heart:
                s.Life = Mathf.Max(1, s.Life + 1);
                break;

            default:
                if (IsEgg(type))
                {
                    var louie = EggToLouie(type);

                    if (s.MountedLouie != MountedType.None)
                    {
                        if (s.QueuedEggs.Count < 8)
                            s.QueuedEggs.Add(type);
                    }
                    else
                    {
                        s.MountedLouie = louie;
                    }
                }
                break;
        }
    }

    public static void StageCaptureFromRuntime(MovementController movement, BombController bomb)
    {
        if (movement == null && bomb == null)
            return;

        int playerId = 1;
        if (movement != null) playerId = GetPlayerIdFrom(movement);
        else if (bomb != null) playerId = GetPlayerIdFrom(bomb);

        BeginStage();

        var s = _stage[playerId - 1];

        if (movement != null && movement.CompareTag("Player"))
        {
            s.SpeedInternal = ClampSpeedInternal(movement.SpeedInternal);

            if (movement.TryGetComponent<CharacterHealth>(out var health))
                s.Life = Mathf.Max(1, health.life);

            AbilitySystem abilitySystem = null;
            if (movement.TryGetComponent(out AbilitySystem a) && a != null)
            {
                abilitySystem = a;
                abilitySystem.RebuildCache();
            }

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

            s.MountedLouie = MountedType.None;
            if (movement.TryGetComponent<PlayerMountCompanion>(out var louieCompanion))
                s.MountedLouie = louieCompanion.GetMountedLouieType();

            if (movement.TryGetComponent<MountEggQueue>(out var q) && q != null)
                q.GetQueuedEggTypesOldestToNewest(s.QueuedEggs);
            else
                s.QueuedEggs.Clear();
        }

        if (bomb != null && bomb.CompareTag("Player"))
        {
            s.BombAmount = Mathf.Min(bomb.bombAmout, MaxBombAmount);
            s.ExplosionRadius = Mathf.Min(bomb.explosionRadius, MaxExplosionRadius);
        }
    }

    public static void ResetSessionForReturnToTitle()
    {
        ResetToDefaultsAll();
        BootSession();
        PlayerPrefs.Save();
    }
}
