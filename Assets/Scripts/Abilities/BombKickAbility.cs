using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(MovementController))]
public class BombKickAbility : MonoBehaviour, IMovementAbility
{
    [Header("Debug Bomb Kick")]
    private readonly bool debugBombKick = false;
    private readonly bool debugBombKickVerbose = false;

    public const string AbilityId = "BombKick";

    private const string KickClipResourcesPath = "Sounds/KickBomb";
    private static AudioClip cachedKickClip;

    private const string KickStopClipResourcesPath = "Sounds/kickstop";
    private static AudioClip cachedKickStopClip;

    private static readonly object kickSfxGate = new();
    private static AudioSource kickSfxOwner;

    [SerializeField] private bool enabledAbility;

    private AudioSource audioSource;
    private BombController bombController;
    private MovementController movement;

    private readonly HashSet<Bomb> kickedByMe = new();
    private readonly Dictionary<Bomb, Vector2> _bombPlantDirection = new();
    private readonly HashSet<Bomb> _bombEarlyKickUnlocked = new();
    private Vector2 _lastOwnerDirection = Vector2.zero;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private static string VecToStr(Vector2 v)
    {
        return $"({v.x:F2}, {v.y:F2})";
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
        movement = GetComponent<MovementController>();

        if (cachedKickClip == null)
            cachedKickClip = Resources.Load<AudioClip>(KickClipResourcesPath);

        if (cachedKickStopClip == null)
            cachedKickStopClip = Resources.Load<AudioClip>(KickStopClipResourcesPath);
    }

    private void Update()
    {
        if (!enabledAbility)
            return;

        if (movement == null || !movement.CompareTag("Player"))
            return;

        if (movement.InputLocked || GamePauseController.IsPaused || movement.isDead)
            return;

        PruneKickedSet();

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        if (input.GetDown(movement.PlayerId, PlayerAction.ActionR))
        {
            LogBombKick($"ActionR pressed -> StopKickedBombsNow kickedByMe:{kickedByMe.Count}");
            StopKickedBombsNow();
        }
    }

    public void Enable()
    {
        enabledAbility = true;

        LogBombKick("Enable BombKickAbility");

        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            abilitySystem.Disable(BombPassAbility.AbilityId);
    }

    public void Disable()
    {
        LogBombKick(
            $"Disable BombKickAbility clearing states kickedByMe:{kickedByMe.Count} " +
            $"plantDirCount:{_bombPlantDirection.Count} unlockedCount:{_bombEarlyKickUnlocked.Count}");

        enabledAbility = false;
        kickedByMe.Clear();
        _bombPlantDirection.Clear();
        _bombEarlyKickUnlocked.Clear();
        _lastOwnerDirection = Vector2.zero;
    }

    public bool TryHandleBlockedHit(Collider2D hit, Vector2 direction, float tileSize, LayerMask obstacleMask)
    {
        LogBombKick(
            $"TryHandleBlockedHit START hit:{(hit != null ? hit.name : "null")} " +
            $"dir:{VecToStr(direction)} tileSize:{tileSize:F2} obstacleMask:{obstacleMask.value}",
            verbose: true);

        if (!enabledAbility)
        {
            LogBombKick("TryHandleBlockedHit -> false (ability disabled)");
            return false;
        }

        if (hit == null)
        {
            LogBombKick("TryHandleBlockedHit -> false (hit null)");
            return false;
        }

        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (hit.gameObject.layer != bombLayer)
        {
            LogBombKick(
                $"TryHandleBlockedHit -> false (hit layer is not Bomb) " +
                $"hit:{hit.name} layer:{LayerMask.LayerToName(hit.gameObject.layer)}",
                verbose: true);
            return false;
        }

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null)
        {
            LogBombKick($"TryHandleBlockedHit -> false (no Bomb component on {hit.name})");
            return false;
        }

        if (bomb.IsBeingKicked)
        {
            LogBombKick(
                $"TryHandleBlockedHit -> false (bomb already being kicked) " +
                $"bomb:{bomb.name}");
            return false;
        }

        if (!bomb.CanBeKicked && !bomb.CanBeKickedEarly)
        {
            LogBombKick(
                $"TryHandleBlockedHit -> false (bomb cannot be kicked) " +
                $"bomb:{bomb.name} canBeKicked:{bomb.CanBeKicked} canBeKickedEarly:{bomb.CanBeKickedEarly} " +
                $"isSolid:{bomb.IsSolid}");
            return false;
        }

        direction = direction.normalized;

        LogBombKick(
            $"TryHandleBlockedHit bomb:{bomb.name} normalizedDir:{VecToStr(direction)} " +
            $"bombPos:{VecToStr(bomb.GetLogicalPosition())} " +
            $"isSolid:{bomb.IsSolid} canBeKicked:{bomb.CanBeKicked} canBeKickedEarly:{bomb.CanBeKickedEarly} " +
            $"isBeingKicked:{bomb.IsBeingKicked} ownerIsMe:{(bomb.Owner == bombController)}");

        if (!bomb.IsSolid)
        {
            bool hasPlantDir = _bombPlantDirection.TryGetValue(bomb, out var plantDir);

            LogBombKick(
                $"EarlyKick check bomb:{bomb.name} hasPlantDir:{hasPlantDir} " +
                $"alreadyUnlocked:{_bombEarlyKickUnlocked.Contains(bomb)}",
                verbose: true);

            if (hasPlantDir)
            {
                plantDir = plantDir.normalized;
                float dot = Vector2.Dot(plantDir, direction);

                LogBombKick(
                    $"EarlyKick compare bomb:{bomb.name} plantDir:{VecToStr(plantDir)} " +
                    $"inputDir:{VecToStr(direction)} dot:{dot:F3}",
                    verbose: false);

                if (!_bombEarlyKickUnlocked.Contains(bomb))
                {
                    if (dot < -0.9f)
                    {
                        _bombEarlyKickUnlocked.Add(bomb);
                        LogBombKick(
                            $"EarlyKick UNLOCKED bomb:{bomb.name} plantDir:{VecToStr(plantDir)} " +
                            $"inputDir:{VecToStr(direction)} dot:{dot:F3}");
                    }
                    else
                    {
                        LogBombKick(
                            $"TryHandleBlockedHit -> false (early kick locked by direction) " +
                            $"bomb:{bomb.name} plantDir:{VecToStr(plantDir)} inputDir:{VecToStr(direction)} dot:{dot:F3}");
                        return false;
                    }
                }
                else
                {
                    LogBombKick(
                        $"EarlyKick already unlocked bomb:{bomb.name}",
                        verbose: true);
                }
            }
            else
            {
                LogBombKick(
                    $"TryHandleBlockedHit non-solid bomb without plant direction. " +
                    $"bomb:{bomb.name} -> proceeding without early-kick direction gate",
                    verbose: false);
            }
        }

        LayerMask bombObstacles = obstacleMask | LayerMask.GetMask("Enemy");

        LogBombKick(
            $"StartKick CALL bomb:{bomb.name} dir:{VecToStr(direction)} tileSize:{tileSize:F2} " +
            $"bombObstacles:{bombObstacles.value} destructibleTilesNull:{(bombController == null || bombController.destructibleTiles == null)}",
            verbose: true);

        bool kicked = bomb.StartKick(
            direction,
            tileSize,
            bombObstacles,
            bombController != null ? bombController.destructibleTiles : null,
            LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Louie"),
            0.60f,
            0.90f,
            false
        );

        LogBombKick(
            $"StartKick RESULT bomb:{bomb.name} kicked:{kicked} " +
            $"isSolidNow:{bomb.IsSolid} isBeingKickedNow:{bomb.IsBeingKicked}");

        if (!kicked)
        {
            LogBombKick(
                $"TryHandleBlockedHit -> false (StartKick returned false) bomb:{bomb.name}");
            return false;
        }

        kickedByMe.Add(bomb);
        _bombPlantDirection.Remove(bomb);
        _bombEarlyKickUnlocked.Remove(bomb);

        LogBombKick(
            $"Kick SUCCESS bomb:{bomb.name} kickedByMe:{kickedByMe.Count} " +
            $"plantDirCount:{_bombPlantDirection.Count} unlockedCount:{_bombEarlyKickUnlocked.Count}");

        PlayKick_InterruptPrevious(cachedKickClip, 1f);

        return true;
    }

    private void StopKickedBombsNow()
    {
        if (kickedByMe.Count == 0)
        {
            LogBombKick("StopKickedBombsNow skipped (kickedByMe empty)", verbose: true);
            return;
        }

        bool stoppedAny = false;

        var toRemove = ListPool<Bomb>.Get();

        foreach (var b in kickedByMe)
        {
            if (b == null)
            {
                LogBombKick("StopKickedBombsNow found null bomb in kickedByMe", verbose: true);
                toRemove.Add(b);
                continue;
            }

            if (!b.IsBeingKicked)
            {
                LogBombKick($"StopKickedBombsNow removing non-moving bomb:{b.name}", verbose: true);
                toRemove.Add(b);
                continue;
            }

            LogBombKick($"StopKickedBombsNow stopping bomb:{b.name}");
            b.StopKickAndSnapToGrid(movement != null ? movement.tileSize : 1f);
            stoppedAny = true;
            toRemove.Add(b);
        }

        for (int i = 0; i < toRemove.Count; i++)
            kickedByMe.Remove(toRemove[i]);

        ListPool<Bomb>.Release(toRemove);

        LogBombKick($"StopKickedBombsNow finished stoppedAny:{stoppedAny} remaining:{kickedByMe.Count}");

        if (stoppedAny)
            PlayKickStop_InterruptPrevious(cachedKickStopClip, 1f);
    }

    private void PruneKickedSet()
    {
        if (kickedByMe.Count > 0)
        {
            var toRemove = ListPool<Bomb>.Get();

            foreach (var b in kickedByMe)
            {
                if (b == null || !b.IsBeingKicked)
                    toRemove.Add(b);
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    var bomb = toRemove[i];
                    LogBombKick(
                        $"PruneKickedSet removing bomb:{(bomb != null ? bomb.name : "null")} " +
                        $"reason:{(bomb == null ? "null" : "not being kicked")}",
                        verbose: true);
                    kickedByMe.Remove(bomb);
                }
            }

            ListPool<Bomb>.Release(toRemove);
        }

        var deadBombs = ListPool<Bomb>.Get();

        foreach (var b in _bombPlantDirection.Keys)
        {
            if (b == null || b.HasExploded || b.IsSolid || b.IsBeingKicked)
                deadBombs.Add(b);
        }

        if (deadBombs.Count > 0)
        {
            for (int i = 0; i < deadBombs.Count; i++)
            {
                var bomb = deadBombs[i];
                LogBombKick(
                    $"PruneKickedSet clearing early-kick tracking bomb:{(bomb != null ? bomb.name : "null")} " +
                    $"null:{(bomb == null)} exploded:{(bomb != null && bomb.HasExploded)} " +
                    $"solid:{(bomb != null && bomb.IsSolid)} beingKicked:{(bomb != null && bomb.IsBeingKicked)}",
                    verbose: true);

                _bombPlantDirection.Remove(bomb);
                _bombEarlyKickUnlocked.Remove(bomb);
            }
        }

        ListPool<Bomb>.Release(deadBombs);
    }

    private void PlayKick_InterruptPrevious(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        lock (kickSfxGate)
        {
            if (kickSfxOwner != null && kickSfxOwner != audioSource)
                kickSfxOwner.Stop();

            kickSfxOwner = audioSource;
            audioSource.Stop();
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void PlayKickStop_InterruptPrevious(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        lock (kickSfxGate)
        {
            if (kickSfxOwner != null && kickSfxOwner != audioSource)
                kickSfxOwner.Stop();

            kickSfxOwner = audioSource;
            audioSource.Stop();
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new();

        public static List<T> Get()
        {
            if (pool.Count > 0)
            {
                var l = pool.Pop();
                l.Clear();
                return l;
            }
            return new List<T>(8);
        }

        public static void Release(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            pool.Push(list);
        }
    }

    public void NotifyBombPlanted(Bomb bomb, Vector2 movementDirectionAtPlant)
    {
        if (bomb == null)
        {
            LogBombKick("NotifyBombPlanted skipped (bomb null)");
            return;
        }

        Vector2 originalDirection = movementDirectionAtPlant;

        if (movementDirectionAtPlant == Vector2.zero)
        {
            if (movement != null && movement.FacingDirection != Vector2.zero)
                movementDirectionAtPlant = movement.FacingDirection.normalized;
            else
                movementDirectionAtPlant = Vector2.down;
        }
        else
        {
            movementDirectionAtPlant = movementDirectionAtPlant.normalized;
        }

        _bombPlantDirection[bomb] = movementDirectionAtPlant;
        _bombEarlyKickUnlocked.Remove(bomb);
        _lastOwnerDirection = movementDirectionAtPlant;

        LogBombKick(
            $"NotifyBombPlanted bomb:{bomb.name} bombPos:{VecToStr(bomb.GetLogicalPosition())} " +
            $"originalDir:{VecToStr(originalDirection)} storedDir:{VecToStr(movementDirectionAtPlant)} " +
            $"plantDirCount:{_bombPlantDirection.Count}");
    }

    public void NotifyOwnerDirectionChanged(Vector2 newDirection)
    {
        if (newDirection == Vector2.zero)
        {
            LogBombKick("NotifyOwnerDirectionChanged skipped (zero direction)", verbose: true);
            return;
        }

        newDirection = newDirection.normalized;

        if (_lastOwnerDirection == newDirection)
        {
            LogBombKick(
                $"NotifyOwnerDirectionChanged skipped (same direction) dir:{VecToStr(newDirection)}",
                verbose: true);
            return;
        }

        Vector2 previousDirection = _lastOwnerDirection;
        _lastOwnerDirection = newDirection;

        LogBombKick(
            $"NotifyOwnerDirectionChanged prev:{VecToStr(previousDirection)} new:{VecToStr(newDirection)} " +
            $"trackedBombs:{_bombPlantDirection.Count}",
            verbose: true);

        if (_bombPlantDirection.Count == 0)
            return;

        var bombsToUnlock = ListPool<Bomb>.Get();

        foreach (var kv in _bombPlantDirection)
        {
            var bomb = kv.Key;
            var plantDir = kv.Value.normalized;

            if (bomb == null || bomb.HasExploded || bomb.IsSolid || bomb.IsBeingKicked)
            {
                LogBombKick(
                    $"NotifyOwnerDirectionChanged skip tracked bomb:{(bomb != null ? bomb.name : "null")} " +
                    $"null:{(bomb == null)} exploded:{(bomb != null && bomb.HasExploded)} " +
                    $"solid:{(bomb != null && bomb.IsSolid)} beingKicked:{(bomb != null && bomb.IsBeingKicked)}",
                    verbose: true);
                continue;
            }

            float dot = Vector2.Dot(plantDir, newDirection);

            LogBombKick(
                $"NotifyOwnerDirectionChanged compare bomb:{bomb.name} " +
                $"plantDir:{VecToStr(plantDir)} newDir:{VecToStr(newDirection)} dot:{dot:F3}",
                verbose: true);

            if (dot < -0.9f)
                bombsToUnlock.Add(bomb);
        }

        for (int i = 0; i < bombsToUnlock.Count; i++)
        {
            _bombEarlyKickUnlocked.Add(bombsToUnlock[i]);
            LogBombKick($"NotifyOwnerDirectionChanged UNLOCK early kick bomb:{bombsToUnlock[i].name}");
        }

        ListPool<Bomb>.Release(bombsToUnlock);
    }

    private void LogBombKick(string message, bool verbose = false)
    {
        if (!debugBombKick)
            return;

        if (verbose && !debugBombKickVerbose)
            return;

        Debug.Log($"[BombKick][{name}] {message}", this);
    }
}