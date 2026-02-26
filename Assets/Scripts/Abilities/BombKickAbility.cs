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

    private static float kickSfxBlockedUntil = 0f;
    private static readonly object kickSfxGate = new();

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
            return false;

        if (hit == null)
            return false;

        if (hit.gameObject.layer != LayerMask.NameToLayer("Bomb"))
            return false;

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null || bomb.IsBeingKicked)
            return false;

        if (!bomb.CanBeKicked)
            return false;

        LayerMask bombObstacles = obstacleMask | LayerMask.GetMask("Enemy");

        bool kicked = bomb.StartKick(
            direction,
            tileSize,
            bombObstacles,
            bombController != null ? bombController.destructibleTiles : null
        );

        if (!kicked)
            return false;

        kickedByMe.Add(bomb);

        TryPlayKickSfx_NoOverlap(cachedKickClip, 1f);

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
            PlayKickStop_Always(cachedKickStopClip, 1f);
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

    private void TryPlayKickSfx_NoOverlap(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        float clipLen = clip.length > 0.001f ? clip.length : 0.10f;

        bool canPlay;

        lock (kickSfxGate)
        {
            float now = Time.time;
            canPlay = now >= kickSfxBlockedUntil;

            if (canPlay)
                kickSfxBlockedUntil = now + clipLen;
        }

        if (!canPlay)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    private void PlayKickStop_Always(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        lock (kickSfxGate)
            kickSfxBlockedUntil = 0f;

        audioSource.Stop();
        audioSource.PlayOneShot(clip, volume);
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