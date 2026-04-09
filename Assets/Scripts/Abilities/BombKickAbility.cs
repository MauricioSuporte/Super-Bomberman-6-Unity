using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(MovementController))]
public class BombKickAbility : MonoBehaviour, IMovementAbility
{
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
            StopKickedBombsNow();
    }

    public void Enable()
    {
        enabledAbility = true;

        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            abilitySystem.Disable(BombPassAbility.AbilityId);
    }

    public void Disable()
    {
        enabledAbility = false;
        kickedByMe.Clear();
        _bombPlantDirection.Clear();
        _bombEarlyKickUnlocked.Clear();
        _lastOwnerDirection = Vector2.zero;
    }

    public bool TryHandleBlockedHit(Collider2D hit, Vector2 direction, float tileSize, LayerMask obstacleMask)
    {
        if (!enabledAbility) return false;
        if (hit == null) return false;
        if (hit.gameObject.layer != LayerMask.NameToLayer("Bomb")) return false;

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null) return false;
        if (bomb.IsBeingKicked) return false;
        if (!bomb.CanBeKicked && !bomb.CanBeKickedEarly) return false;

        direction = direction.normalized;

        if (!bomb.IsSolid && _bombPlantDirection.TryGetValue(bomb, out var plantDir))
        {
            plantDir = plantDir.normalized;

            if (!_bombEarlyKickUnlocked.Contains(bomb))
            {
                if (Vector2.Dot(plantDir, direction) < -0.9f)
                    _bombEarlyKickUnlocked.Add(bomb);
                else
                    return false;
            }
        }

        LayerMask bombObstacles = obstacleMask | LayerMask.GetMask("Enemy");

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

        if (!kicked)
            return false;

        kickedByMe.Add(bomb);
        _bombPlantDirection.Remove(bomb);
        _bombEarlyKickUnlocked.Remove(bomb);

        PlayKick_InterruptPrevious(cachedKickClip, 1f);

        return true;
    }

    private void StopKickedBombsNow()
    {
        if (kickedByMe.Count == 0)
            return;

        bool stoppedAny = false;

        var toRemove = ListPool<Bomb>.Get();

        foreach (var b in kickedByMe)
        {
            if (b == null)
            {
                toRemove.Add(b);
                continue;
            }

            if (!b.IsBeingKicked)
            {
                toRemove.Add(b);
                continue;
            }

            b.StopKickAndSnapToGrid(movement != null ? movement.tileSize : 1f);
            stoppedAny = true;
            toRemove.Add(b);
        }

        for (int i = 0; i < toRemove.Count; i++)
            kickedByMe.Remove(toRemove[i]);

        ListPool<Bomb>.Release(toRemove);

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

            for (int i = 0; i < toRemove.Count; i++)
                kickedByMe.Remove(toRemove[i]);

            ListPool<Bomb>.Release(toRemove);
        }

        var deadBombs = ListPool<Bomb>.Get();

        foreach (var b in _bombPlantDirection.Keys)
        {
            if (b == null || b.HasExploded || b.IsSolid || b.IsBeingKicked)
                deadBombs.Add(b);
        }

        for (int i = 0; i < deadBombs.Count; i++)
        {
            _bombPlantDirection.Remove(deadBombs[i]);
            _bombEarlyKickUnlocked.Remove(deadBombs[i]);
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
            return;

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
    }

    public void NotifyOwnerDirectionChanged(Vector2 newDirection)
    {
        if (newDirection == Vector2.zero)
            return;

        newDirection = newDirection.normalized;

        if (_lastOwnerDirection == newDirection)
            return;

        _lastOwnerDirection = newDirection;

        if (_bombPlantDirection.Count == 0)
            return;

        var bombsToUnlock = ListPool<Bomb>.Get();

        foreach (var kv in _bombPlantDirection)
        {
            var bomb = kv.Key;
            var plantDir = kv.Value.normalized;

            if (bomb == null || bomb.HasExploded || bomb.IsSolid || bomb.IsBeingKicked)
                continue;

            if (Vector2.Dot(plantDir, newDirection) < -0.9f)
                bombsToUnlock.Add(bomb);
        }

        for (int i = 0; i < bombsToUnlock.Count; i++)
            _bombEarlyKickUnlocked.Add(bombsToUnlock[i]);

        ListPool<Bomb>.Release(bombsToUnlock);
    }
}