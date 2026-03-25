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
    private bool lastShouldPlay;
    private string lastBlockSummary = string.Empty;
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

    private readonly string[] cardinalNames =
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

        bool shouldPlay = IsCornered(out bool bombBlocked, out string summary);

        lastShouldPlay = shouldPlay;
        lastBlockSummary = summary;

        if (!shouldPlay)
        {
            if (isPlaying)
                StopCornered();

            return;
        }

        if ((Time.time - lastInputTime) < inputSuppressSeconds)
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

    private bool IsCornered(out bool foundBomb, out string summary)
    {
        foundBomb = false;

        Vector2 origin =
            movement.Rigidbody != null
                ? movement.Rigidbody.position
                : (Vector2)transform.position;

        float tileSize = Mathf.Max(0.01f, movement.tileSize);
        float probeDistance = tileSize * probeDistanceMultiplier;
        Vector2Int playerTile = WorldToTile(origin);

        System.Text.StringBuilder sb = new System.Text.StringBuilder(160);
        sb.Append($"origin={origin} playerTile={playerTile} tileSize={tileSize:0.###} probeDistance={probeDistance:0.###}");

        if (HasBombOnSameTileAsPlayer(origin))
        {
            sb.Append(" | abort=same-tile-bomb");
            summary = sb.ToString();
            return false;
        }

        for (int i = 0; i < cardinalDirs.Length; i++)
        {
            Vector2 dir = cardinalDirs[i];
            Vector2 probePos = origin + (dir * probeDistance);

            bool blocked = TryFindBlockingThing(cardinalNames[i], origin, probePos, out bool isBomb, out string blockReason);

            sb.Append($" | {cardinalNames[i]}: blocked={blocked}, bomb={isBomb}, info={blockReason}");

            if (!blocked)
            {
                summary = sb.ToString();
                return false;
            }

            if (isBomb)
                foundBomb = true;
        }

        sb.Append($" | foundBomb={foundBomb}");
        summary = sb.ToString();
        return foundBomb;
    }

    private bool TryFindBlockingThing(string directionName, Vector2 playerOrigin, Vector2 probePos, out bool isBomb, out string reason)
    {
        isBomb = false;
        reason = "no hits";

        float tileSize = Mathf.Max(0.01f, movement.tileSize);
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(probePos, size, 0f, blockMask);
        if (hits == null || hits.Length == 0)
        {
            reason = $"OverlapBoxAll vazio em {probePos} size={size}";
            return false;
        }

        Vector2Int playerTile = WorldToTile(playerOrigin);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"probe={probePos} size={size} hits={hits.Length} playerTile={playerTile}");

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];

            if (hit == null)
            {
                sb.Append(" | hit=NULL(ignore)");
                continue;
            }

            GameObject go = hit.gameObject;
            Vector2 hitCenter = hit.bounds.center;
            Vector2Int hitTile = WorldToTile(hitCenter);

            sb.Append($" | hit[{i}]={go.name} tag={go.tag} layer={LayerMask.LayerToName(go.layer)} trigger={hit.isTrigger} hitTile={hitTile}");

            if (go == gameObject)
            {
                sb.Append("(self-ignore)");
                continue;
            }

            if (hit.isTrigger)
            {
                sb.Append("(trigger-ignore)");
                continue;
            }

            if (go.layer == LayerMask.NameToLayer("Bomb") && hitTile == playerTile)
            {
                sb.Append("(same-tile-bomb-ignore)");
                continue;
            }

            if (IsValidCorneredBlock(hit, out bool blockIsBomb, out string validationReason))
            {
                isBomb = blockIsBomb;
                sb.Append($"(VALID bomb={blockIsBomb} reason={validationReason})");
                reason = sb.ToString();
                return true;
            }

            sb.Append($"(invalid reason={validationReason})");
        }

        reason = sb.ToString();
        return false;
    }

    private bool IsValidCorneredBlock(Collider2D hit, out bool isBomb, out string validationReason)
    {
        isBomb = false;
        validationReason = "unknown";

        if (hit == null)
        {
            validationReason = "hit null";
            return false;
        }

        GameObject go = hit.gameObject;

        if (go.layer == LayerMask.NameToLayer("Bomb"))
        {
            isBomb = true;
            validationReason = "layer Bomb";
            return true;
        }

        if (go.CompareTag(destructiblesTag))
        {
            validationReason = $"tag {destructiblesTag}";
            return true;
        }

        Transform t = go.transform;
        while (t != null)
        {
            if (t.name == indestructiblesName)
            {
                validationReason = $"ancestor name {indestructiblesName}";
                return true;
            }

            t = t.parent;
        }

        validationReason = "não é Bomb layer, nem Destructibles tag, nem filho de Indestructibles";
        return false;
    }

    private Vector2Int WorldToTile(Vector2 worldPos)
    {
        float tileSize = Mathf.Max(0.01f, movement != null ? movement.tileSize : 1f);

        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / tileSize),
            Mathf.RoundToInt(worldPos.y / tileSize));
    }

    private bool IsSameTile(Vector2 a, Vector2 b)
    {
        return WorldToTile(a) == WorldToTile(b);
    }

    private bool HasBombOnSameTileAsPlayer(Vector2 origin)
    {
        float tileSize = Mathf.Max(0.01f, movement.tileSize);
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, size, 0f, blockMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            GameObject go = hit.gameObject;
            if (go == gameObject)
                continue;

            if (go.layer != LayerMask.NameToLayer("Bomb"))
                continue;

            if (IsSameTile(origin, hit.bounds.center))
                return true;
        }

        return false;
    }

    private AnimatedSpriteRenderer GetCorneredRenderer()
    {
        if (movement == null)
            return corneredLoopRenderer;

        if (!movement.IsMountedOnLouie)
            return corneredLoopRenderer;

        var mountVisual = GetComponentInChildren<MountVisualController>(true);
        if (mountVisual == null)
            return corneredLoopRenderer;

        if (mountVisual.louieCornered != null)
            return mountVisual.louieCornered;

        return null;
    }

    private void StartCornered()
    {
        if (isPlaying)
            return;

        if (!hasBombInBlock)
            return;

        activeCorneredRenderer = GetCorneredRenderer();

        bool hasVisual = activeCorneredRenderer != null;

        isPlaying = true;

        if (movement.IsMountedOnLouie)
        {
            if (hasVisual)
            {
                movement.SetVisualOverrideActive(true);
                movement.SetInactivityMountedDownOverride(true);

                var mountVisual = GetComponentInChildren<MountVisualController>(true);
                if (mountVisual != null)
                    mountVisual.SetCornered(true);
            }
            else
            {
                movement.SetInactivityMountedDownOverride(false);
            }
        }
        else
        {
            if (hasVisual)
            {
                movement.SetVisualOverrideActive(true);
                movement.SetInactivityMountedDownOverride(false);

                activeCorneredRenderer.loop = true;
                activeCorneredRenderer.idle = false;

                SetCorneredEnabled(true);

                if (refreshFrameOnEnter)
                    activeCorneredRenderer.RefreshFrame();
            }
            else
            {
                movement.SetInactivityMountedDownOverride(false);
            }
        }

        PlayCorneredSfx();
    }

    private void StopCornered()
    {
        if (!isPlaying)
        {
            if (movement != null && movement.IsMountedOnLouie)
            {
                var mountVisual = GetComponentInChildren<MountVisualController>(true);
                if (mountVisual != null)
                    mountVisual.SetCornered(false);

                movement.SetInactivityMountedDownOverride(false);
            }
            else
            {
                SetCorneredEnabled(false);

                if (movement != null)
                    movement.SetInactivityMountedDownOverride(false);
            }

            movement?.SetVisualOverrideActive(false);
            activeCorneredRenderer = null;
            return;
        }

        if (movement != null && movement.IsMountedOnLouie)
        {
            var mountVisual = GetComponentInChildren<MountVisualController>(true);
            if (mountVisual != null)
                mountVisual.SetCornered(false);

            movement.SetInactivityMountedDownOverride(false);
        }
        else
        {
            SetCorneredEnabled(false);

            if (movement != null)
                movement.SetInactivityMountedDownOverride(false);
        }

        movement?.SetVisualOverrideActive(false);

        isPlaying = false;
        hasBombInBlock = false;
        activeCorneredRenderer = null;
    }

    private void SetCorneredEnabled(bool on)
    {
        if (activeCorneredRenderer == null)
            return;

        activeCorneredRenderer.enabled = on;

        if (activeCorneredRenderer.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    private void PlayCorneredSfx()
    {
        if (audioSource == null)
            return;

        if (corneredSfx == null)
            return;

        float now = Time.time;

        if (now - lastSfxTime < corneredSfxCooldown)
            return;

        lastSfxTime = now;
        audioSource.PlayOneShot(corneredSfx, corneredSfxVolume);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (movement == null)
            movement = GetComponent<MovementController>();

        if (movement == null)
            return;

        Vector2 origin =
            movement.Rigidbody != null
                ? movement.Rigidbody.position
                : (Vector2)transform.position;

        float tileSize = Mathf.Max(0.01f, movement.tileSize);
        float probeDistance = tileSize * probeDistanceMultiplier;
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Gizmos.color = Color.yellow;

        for (int i = 0; i < cardinalDirs.Length; i++)
        {
            Vector2 probePos = origin + (cardinalDirs[i] * probeDistance);
            Gizmos.DrawWireCube(probePos, size);
        }
    }
#endif
}