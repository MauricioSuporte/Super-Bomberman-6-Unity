using UnityEngine;

[DefaultExecutionOrder(-45)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public sealed class CorneredAnimation : MonoBehaviour
{
    [Header("Cornered Visual")]
    [SerializeField] private AnimatedSpriteRenderer corneredLoopRenderer;
    [SerializeField] private bool refreshFrameOnEnter = true;

    [Header("Blocking Detection")]
    [SerializeField, Min(0.1f)] private float probeDistanceMultiplier = 0.5f;
    [SerializeField] private LayerMask blockMask;
    [SerializeField] private string destructiblesTag = "Destructibles";
    [SerializeField] private string indestructiblesName = "Indestructibles";

    [Header("SFX")]
    [SerializeField] private AudioClip corneredSfx;
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

        if (corneredSfx == null)
            corneredSfx = Resources.Load<AudioClip>("Sounds/Cornered");

        lastInputTime = Time.time;

        SetCorneredEnabled(false);
    }

    private void OnValidate()
    {
        blockMask = LayerMask.GetMask("Stage", "Bomb");
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
        if (movement == null)
            return;

        if (movement.InputLocked || movement.isDead || movement.IsEndingStage || GamePauseController.IsPaused)
        {
            if (isPlaying)
                StopCornered();

            lastInputTime = Time.time;
            return;
        }

        if (HasAnyPlayerInput())
        {
            lastInputTime = Time.time;

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

        float silentTime = Time.time - lastInputTime;
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

    private bool HasAnyPlayerInput()
    {
        if (!CompareTag("Player"))
            return false;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        int id = movement.PlayerId;

        return
            input.Get(id, PlayerAction.MoveUp) ||
            input.Get(id, PlayerAction.MoveDown) ||
            input.Get(id, PlayerAction.MoveLeft) ||
            input.Get(id, PlayerAction.MoveRight) ||
            input.Get(id, PlayerAction.Start) ||
            input.Get(id, PlayerAction.ActionA) ||
            input.Get(id, PlayerAction.ActionB) ||
            input.Get(id, PlayerAction.ActionC) ||
            input.Get(id, PlayerAction.ActionL) ||
            input.Get(id, PlayerAction.ActionR);
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
        Collider2D[] hits = Physics2D.OverlapBoxAll(probePos, size, 0f, blockMask);

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null) continue;

            var go = hit.gameObject;

            if (go == gameObject) continue;
            if (hit.isTrigger) continue;
            if (go.layer == LayerMask.NameToLayer("Bomb")) continue;

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

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, size, 0f, blockMask);

        if (hits == null)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit.isTrigger) continue;

            var go = hit.gameObject;
            if (go == gameObject) continue;

            if (go.CompareTag(destructiblesTag))
                return true;
        }

        return false;
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
        if (audioSource == null || corneredSfx == null)
            return;

        float now = Time.time;

        if (now - lastSfxTime < corneredSfxCooldown)
            return;

        lastSfxTime = now;
        audioSource.PlayOneShot(corneredSfx, corneredSfxVolume);
    }
}