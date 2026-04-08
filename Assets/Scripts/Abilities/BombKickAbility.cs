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
    }

    public bool TryHandleBlockedHit(Collider2D hit, Vector2 direction, float tileSize, LayerMask obstacleMask)
    {
        if (!enabledAbility)
        {
            Debug.Log("[BombKickAbility] blocked: ability disabled", this);
            return false;
        }

        if (hit == null)
        {
            Debug.Log("[BombKickAbility] blocked: hit null", this);
            return false;
        }

        if (hit.gameObject.layer != LayerMask.NameToLayer("Bomb"))
        {
            Debug.Log($"[BombKickAbility] blocked: hit layer is not Bomb, layer={hit.gameObject.layer}", this);
            return false;
        }

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null)
        {
            Debug.Log("[BombKickAbility] blocked: collider has no Bomb component", this);
            return false;
        }

        if (bomb.IsBeingKicked)
        {
            Debug.Log($"[BombKickAbility] blocked: bomb already being kicked bombPos={bomb.GetLogicalPosition()}", this);
            return false;
        }

        Debug.Log(
            $"[BombKickAbility] trying kick bombPos={bomb.GetLogicalPosition()} dir={direction} tileSize={tileSize} " +
            $"solid={bomb.IsSolid} canBeKicked={bomb.CanBeKicked} canBeKickedEarly={bomb.CanBeKickedEarly}",
            this);

        if (!bomb.CanBeKicked && !bomb.CanBeKickedEarly)
        {
            Debug.Log("[BombKickAbility] blocked: bomb rejected by CanBeKicked/CanBeKickedEarly", this);
            return false;
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

        Debug.Log(
            $"[BombKickAbility] StartKick result={kicked} bombPos={bomb.GetLogicalPosition()} " +
            $"solid={bomb.IsSolid} canBeKicked={bomb.CanBeKicked} canBeKickedEarly={bomb.CanBeKickedEarly}",
            this);

        if (!kicked)
            return false;

        kickedByMe.Add(bomb);

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
        if (kickedByMe.Count == 0)
            return;

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
}