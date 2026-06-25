using UnityEngine;

[DefaultExecutionOrder(-45)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public sealed class CorneredAnimation : MonoBehaviour
{
    private static readonly string[] BlockedSfxResourcesPaths =
    {
        "Sounds/blocked",
        "Sounds/blocked2"
    };

    [Header("Cornered Visual")]
    [SerializeField] private AnimatedSpriteRenderer corneredLoopRenderer;
    [SerializeField] private bool refreshFrameOnEnter = true;
    [SerializeField, Min(0.02f)] private float corneredCheckInterval = 0.1f;

    [Header("Blocking Detection")]
    [SerializeField, Min(0.1f)] private float probeDistanceMultiplier = 0.5f;
    [SerializeField] private LayerMask blockMask;
    [SerializeField] private string destructiblesTag = "Destructibles";
    [SerializeField] private string indestructiblesName = "Indestructibles";

    [Header("SFX")]
    [SerializeField] private AudioClip[] blockedSfxClips;
    [SerializeField, Range(0f, 1f)] private float corneredSfxVolume = 1f;
    [SerializeField, Min(0f)] private float corneredSfxCooldown = 3f;

    [Header("Input Suppression")]
    [SerializeField, Min(0f)] private float inputSuppressSeconds = 0.25f;

    private MovementController movement;
    private AudioSource audioSource;

    private bool isPlaying;
    private bool hasBombInBlock;
    private float lastSfxTime = -999f;
    private float lastInputTime;

    private AnimatedSpriteRenderer activeCorneredRenderer;
    private float nextCorneredCheckTime;
    private int bombLayer;
    private ContactFilter2D blockFilter;
    private readonly Collider2D[] overlapResults = new Collider2D[16];

    private readonly Vector2[] cardinalDirs =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    private readonly string[] cardinalDirNames =
    {
        "Up",
        "Down",
        "Left",
        "Right"
    };

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        if (blockMask.value == 0)
            blockMask = LayerMask.GetMask("Stage", "Bomb");

        bombLayer = LayerMask.NameToLayer("Bomb");
        RebuildBlockFilter();

        LoadBlockedSfxClips();

        lastInputTime = Time.time;

        SetCorneredEnabled(false);
    }

    private void OnValidate()
    {
        blockMask = LayerMask.GetMask("Stage", "Bomb");
        bombLayer = LayerMask.NameToLayer("Bomb");
        RebuildBlockFilter();
    }

    private void OnEnable()
    {
        lastInputTime = Time.time;
        StopCornered();
    }

    private void OnDisable()
    {
        StopCornered();
    }

    private void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.CorneredAnimationUpdate.Auto();

        if (movement == null)
            return;

        if (movement.InputLocked || movement.isDead || movement.IsEndingStage || GamePauseController.IsPaused)
        {
            if (isPlaying)
                StopCornered();

            lastInputTime = Time.time;
            return;
        }

        if (Bomb.ActiveBombs.Count == 0)
        {
            if (isPlaying)
                StopCornered();

            return;
        }

        if (HasAnyMovementIntent())
        {
            lastInputTime = Time.time;

            if (isPlaying)
                StopCornered();

            return;
        }

        float silentTime = Time.time - lastInputTime;
        if (!isPlaying && silentTime < inputSuppressSeconds)
            return;

        if (Time.time < nextCorneredCheckTime)
            return;

        nextCorneredCheckTime = Time.time + corneredCheckInterval;

        Vector2 origin =
            movement.Rigidbody != null
                ? movement.Rigidbody.position
                : (Vector2)transform.position;

        if (!HasNearbyActiveBomb(origin))
        {
            if (isPlaying)
                StopCornered();

            return;
        }

        bool shouldPlay = IsCornered(out bool bombBlocked);

        if (!shouldPlay)
        {
            if (isPlaying)
                StopCornered();

            return;
        }

        if (silentTime < inputSuppressSeconds)
        {
            if (isPlaying)
                StopCornered();

            return;
        }

        hasBombInBlock = bombBlocked;

        if (!isPlaying)
            StartCornered();
    }

    private bool HasAnyMovementIntent()
    {
        if (HasAnyPlayerInput())
            return true;

        if (movement.Direction.sqrMagnitude > 0.0001f)
            return true;

        if (movement is MovementControllerAI movementAI)
        {
            if (movementAI.aiDirection.sqrMagnitude > 0.0001f)
                return true;
        }

        return false;
    }

    private bool HasAnyPlayerInput()
    {
        if (!CompareTag("Player"))
            return false;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        return input.HasAnyHeldInput(movement.PlayerId);
    }

    private bool IsCornered(out bool foundBomb)
    {
        foundBomb = false;

        Vector2 origin =
            movement.Rigidbody != null
                ? movement.Rigidbody.position
                : (Vector2)transform.position;

        if (HasDestructibleOnSameTileAsPlayer(origin))
            return false;

        for (int i = 0; i < cardinalDirs.Length; i++)
        {
            Vector2 dir = cardinalDirs[i];
            Vector2 probePos = GetProbePosition(origin, dir);

            bool blocked = TryFindBlockingThing(
                origin,
                probePos,
                dir,
                cardinalDirNames[i],
                out bool isBomb);

            if (!blocked)
                return false;

            if (isBomb)
                foundBomb = true;
        }

        return foundBomb;
    }

    private bool HasNearbyActiveBomb(Vector2 playerOrigin)
    {
        float reach = Mathf.Max(0.01f, movement.tileSize) * 1.5f;

        foreach (var bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2 offset = (Vector2)bomb.transform.position - playerOrigin;
            if (Mathf.Abs(offset.x) <= reach && Mathf.Abs(offset.y) <= reach)
                return true;
        }

        return false;
    }

    private Vector2 GetProbePosition(Vector2 origin, Vector2 dir)
    {
        float tileSize = Mathf.Max(0.01f, movement.tileSize);
        float probeDistance = tileSize * probeDistanceMultiplier;
        return origin + (dir * probeDistance);
    }

    private Vector2 GetProbeSize(Vector2 dir)
    {
        float tileSize = Mathf.Max(0.01f, movement.tileSize);

        return Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(tileSize * 0.6f, tileSize * 0.2f)
            : new Vector2(tileSize * 0.2f, tileSize * 0.6f);
    }

    private bool TryFindBlockingThing(
        Vector2 playerOrigin,
        Vector2 probePos,
        Vector2 dir,
        string dirName,
        out bool isBomb)
    {
        isBomb = false;

        if (IsBombBlockingForCornered(playerOrigin, probePos, dirName))
        {
            isBomb = true;
            return true;
        }

        Vector2 size = GetProbeSize(dir);
        int hitCount = GetOverlapCount(probePos, size);

        if (hitCount <= 0)
            return false;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = overlapResults[i];
            overlapResults[i] = null;
            if (hit == null) continue;

            var go = hit.gameObject;

            if (go == gameObject) continue;
            if (hit.isTrigger) continue;
            if (go.layer == bombLayer) continue;

            if (IsValidCorneredBlock(hit))
                return true;
        }

        return false;
    }

    private bool IsBombBlockingForCornered(Vector2 playerOrigin, Vector2 probePos, string dirName)
    {
        Vector2 probeSize = GetProbeSize(
            dirName == "Left" || dirName == "Right" ? Vector2.right : Vector2.up);

        foreach (var bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            bomb.TryGetComponent<Collider2D>(out var bombCol);

            Vector2 colliderCenter = bombCol != null
                ? (Vector2)bombCol.bounds.center
                : (Vector2)bomb.transform.position;

            bool probeHits = DoesProbeOverlapBombCollider(probePos, probeSize, bombCol);
            bool currentOverlap = IsInsideBombTileFootprint(playerOrigin, colliderCenter);

            if (bomb.IsSolid)
            {
                if (probeHits)
                    return true;

                continue;
            }

            if (probeHits && !currentOverlap)
                return true;
        }

        return false;
    }

    private bool IsValidCorneredBlock(Collider2D hit)
    {
        if (hit == null)
            return false;

        GameObject go = hit.gameObject;

        if (go.CompareTag(destructiblesTag))
            return true;

        Transform t = go.transform;
        while (t != null)
        {
            if (t.name == indestructiblesName)
                return true;

            t = t.parent;
        }

        return false;
    }

    private bool IsInsideBombTileFootprint(Vector2 worldPos, Vector2 bombPos)
    {
        float halfTile = movement.tileSize * 0.5f + 0.0001f;

        return Mathf.Abs(worldPos.x - bombPos.x) <= halfTile
            && Mathf.Abs(worldPos.y - bombPos.y) <= halfTile;
    }

    private bool HasDestructibleOnSameTileAsPlayer(Vector2 origin)
    {
        float tileSize = Mathf.Max(0.01f, movement.tileSize);
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        int hitCount = GetOverlapCount(origin, size);

        if (hitCount <= 0)
            return false;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = overlapResults[i];
            overlapResults[i] = null;
            if (hit == null || hit.isTrigger) continue;

            var go = hit.gameObject;
            if (go == gameObject) continue;

            if (go.CompareTag(destructiblesTag))
                return true;
        }

        return false;
    }

    private void RebuildBlockFilter()
    {
        blockFilter = new ContactFilter2D { useLayerMask = true };
        blockFilter.SetLayerMask(blockMask);
        blockFilter.useTriggers = true;
    }

    private int GetOverlapCount(Vector2 position, Vector2 size)
    {
        return Physics2D.OverlapBox(position, size, 0f, blockFilter, overlapResults);
    }

    private bool DoesProbeOverlapBombCollider(Vector2 probePos, Vector2 probeSize, Collider2D bombCol)
    {
        if (bombCol == null)
            return false;

        Bounds b = bombCol.bounds;

        float probeMinX = probePos.x - (probeSize.x * 0.5f);
        float probeMaxX = probePos.x + (probeSize.x * 0.5f);
        float probeMinY = probePos.y - (probeSize.y * 0.5f);
        float probeMaxY = probePos.y + (probeSize.y * 0.5f);

        return !(probeMaxX < b.min.x ||
                 probeMinX > b.max.x ||
                 probeMaxY < b.min.y ||
                 probeMinY > b.max.y);
    }

    private AnimatedSpriteRenderer GetCorneredRenderer()
    {
        if (!movement.IsMounted)
            return corneredLoopRenderer;

        var mountVisual = GetComponentInChildren<MountVisualController>(true);
        return mountVisual != null ? mountVisual.louieCornered : corneredLoopRenderer;
    }

    private void StartCornered()
    {
        if (isPlaying)
            return;

        if (!hasBombInBlock)
            return;

        activeCorneredRenderer = GetCorneredRenderer();

        isPlaying = true;

        movement.SetSuppressInactivityAnimation(true);

        if (movement.IsMounted)
        {
            movement.SetVisualOverrideActive(true);
            movement.SetInactivityMountedDownOverride(true);

            var mountVisual = GetComponentInChildren<MountVisualController>(true);
            if (mountVisual != null)
                mountVisual.SetCornered(true);
        }
        else if (activeCorneredRenderer != null)
        {
            movement.SetVisualOverrideActive(true);
            movement.SetInactivityMountedDownOverride(false);

            activeCorneredRenderer.loop = true;
            activeCorneredRenderer.idle = false;

            SetCorneredEnabled(true);

            if (refreshFrameOnEnter)
                activeCorneredRenderer.RefreshFrame();
        }

        PlayCorneredSfx();
    }

    private void StopCornered()
    {
        if (movement != null && movement.IsMounted)
        {
            var mountVisual = GetComponentInChildren<MountVisualController>(true);
            if (mountVisual != null)
                mountVisual.SetCornered(false);
        }

        SetCorneredEnabled(false);

        movement?.SetVisualOverrideActive(false);
        movement?.SetInactivityMountedDownOverride(false);
        movement?.SetSuppressInactivityAnimation(false);

        isPlaying = false;
        hasBombInBlock = false;
        activeCorneredRenderer = null;
    }

    private void SetCorneredEnabled(bool on)
    {
        if (activeCorneredRenderer == null)
            return;

        activeCorneredRenderer.enabled = on;

        if (activeCorneredRenderer.TryGetComponent(out SpriteRenderer sr))
            sr.enabled = on;
    }

    private void PlayCorneredSfx()
    {
        if (audioSource == null)
            return;

        AudioClip clip = GetBlockedSfxClip();
        if (clip == null)
            return;

        float now = Time.time;

        if (now - lastSfxTime < corneredSfxCooldown)
            return;

        lastSfxTime = now;
        GameAudioSettings.PlaySfx(audioSource, clip, corneredSfxVolume);
    }

    private void LoadBlockedSfxClips()
    {
        if (blockedSfxClips == null || blockedSfxClips.Length != BlockedSfxResourcesPaths.Length)
            blockedSfxClips = new AudioClip[BlockedSfxResourcesPaths.Length];

        for (int i = 0; i < BlockedSfxResourcesPaths.Length; i++)
        {
            if (blockedSfxClips[i] == null)
                blockedSfxClips[i] = Resources.Load<AudioClip>(BlockedSfxResourcesPaths[i]);
        }
    }

    private AudioClip GetBlockedSfxClip()
    {
        LoadBlockedSfxClips();

        if (blockedSfxClips == null || blockedSfxClips.Length == 0)
            return null;

        int startIndex = Random.Range(0, blockedSfxClips.Length);
        for (int i = 0; i < blockedSfxClips.Length; i++)
        {
            int index = (startIndex + i) % blockedSfxClips.Length;
            AudioClip clip = blockedSfxClips[index];
            if (clip != null)
                return clip;
        }

        return null;
    }
}
