using UnityEngine;
using System.Text;

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

    [Header("Debug")]
    [SerializeField] private bool enableCorneredLogs = true;
    [SerializeField] private bool logEveryProbe = true;
    [SerializeField] private bool logOnlyStateChanges = false;

    private MovementController movement;
    private AudioSource audioSource;

    private bool isPlaying;
    private bool hasBombInBlock;
    private float lastSfxTime = -999f;
    private float lastInputTime;

    private AnimatedSpriteRenderer activeCorneredRenderer;

    private bool lastEvaluatedShouldPlay;
    private bool lastEvaluatedBombBlocked;
    private string lastDecisionReason = string.Empty;

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

    private void OnEnable()
    {
        lastInputTime = Time.time;
        StopCornered();
        LogCornered("OnEnable -> reset");
    }

    private void OnDisable()
    {
        LogCornered("OnDisable -> StopCornered");
        StopCornered();
    }

    private void Update()
    {
        if (movement == null)
            return;

        if (movement.InputLocked || movement.isDead || movement.IsEndingStage || GamePauseController.IsPaused)
        {
            if (ShouldLogStateChange(false, false, "abort:locked/dead/pause"))
            {
                LogCornered(
                    $"Update abort -> locked={movement.InputLocked} dead={movement.isDead} " +
                    $"ending={movement.IsEndingStage} paused={GamePauseController.IsPaused}");
            }

            if (isPlaying)
                StopCornered();

            lastInputTime = Time.time;
            return;
        }

        if (HasAnyPlayerInput())
        {
            if (ShouldLogStateChange(false, false, "abort:input"))
                LogCornered("Update abort -> player input detected");

            lastInputTime = Time.time;

            if (isPlaying)
                StopCornered();

            return;
        }

        bool shouldPlay = IsCornered(out bool bombBlocked, out string detail);

        if (ShouldLogStateChange(shouldPlay, bombBlocked, detail))
        {
            LogCornered(
                $"Evaluate -> shouldPlay={shouldPlay} bombBlocked={bombBlocked} " +
                $"isPlaying={isPlaying} detail={detail}");
        }

        if (!shouldPlay)
        {
            if (isPlaying)
                StopCornered();

            return;
        }

        float silentTime = Time.time - lastInputTime;
        if (silentTime < inputSuppressSeconds)
        {
            if (ShouldLogStateChange(false, bombBlocked, $"abort:inputSuppress ({silentTime:0.###} < {inputSuppressSeconds:0.###})"))
            {
                LogCornered(
                    $"Evaluate abort -> input suppression active. silentTime={silentTime:0.###} " +
                    $"required={inputSuppressSeconds:0.###}");
            }

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

    private bool IsCornered(out bool foundBomb, out string detail)
    {
        foundBomb = false;

        Vector2 origin =
            movement.Rigidbody != null
                ? movement.Rigidbody.position
                : (Vector2)transform.position;

        StringBuilder sb = new StringBuilder(256);
        sb.Append($"origin={Fmt(origin)}");

        if (HasDestructibleOnSameTileAsPlayer(origin))
        {
            sb.Append(" | abort=same-tile-destructible");
            detail = sb.ToString();
            return false;
        }

        for (int i = 0; i < cardinalDirs.Length; i++)
        {
            Vector2 dir = cardinalDirs[i];
            Vector2 probePos = GetProbePosition(origin, dir);

            bool blocked = TryFindBlockingThing(
                origin,
                probePos,
                dir,
                cardinalDirNames[i],
                out bool isBomb,
                out string blockReason);

            sb.Append($" | {cardinalDirNames[i]}: blocked={blocked} bomb={isBomb} reason={blockReason}");

            if (logEveryProbe)
            {
                LogCornered(
                    $"Probe {cardinalDirNames[i]} -> origin={Fmt(origin)} probe={Fmt(probePos)} " +
                    $"blocked={blocked} isBomb={isBomb} reason={blockReason}");
            }

            if (!blocked)
            {
                detail = sb.ToString();
                return false;
            }

            if (isBomb)
                foundBomb = true;
        }

        sb.Append($" | foundBomb={foundBomb}");
        detail = sb.ToString();
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
        out bool isBomb,
        out string reason)
    {
        isBomb = false;

        string bombReason;
        bool bombBlocked = IsBombBlockingForCornered(playerOrigin, probePos, dirName, out bombReason);

        if (bombBlocked)
        {
            isBomb = true;
            reason = bombReason;
            return true;
        }

        Vector2 size = GetProbeSize(dir);
        Collider2D[] hits = Physics2D.OverlapBoxAll(probePos, size, 0f, blockMask);

        if (hits == null || hits.Length == 0)
        {
            reason = $"bombCheck={bombReason} | no-hits probe={Fmt(probePos)} size={Fmt(size)}";
            return false;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append($"bombCheck={bombReason} | probe={Fmt(probePos)} size={Fmt(size)} hits={hits.Length}");

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                sb.Append($" | hit[{i}]=null(ignore)");
                continue;
            }

            GameObject go = hit.gameObject;
            sb.Append(
                $" | hit[{i}]={go.name} layer={LayerMask.LayerToName(go.layer)} " +
                $"tag={go.tag} trigger={hit.isTrigger} center={Fmt(hit.bounds.center)} ext={Fmt(hit.bounds.extents)}");

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

            if (go.layer == LayerMask.NameToLayer("Bomb"))
            {
                sb.Append("(bomb-ignore-handled-separately)");
                continue;
            }

            if (IsValidCorneredBlock(hit, out string validReason))
            {
                sb.Append($"(VALID:{validReason})");
                reason = sb.ToString();
                return true;
            }

            sb.Append($"(invalid:{validReason})");
        }

        reason = sb.ToString();
        return false;
    }

    private bool IsBombBlockingForCornered(
        Vector2 playerOrigin,
        Vector2 probePos,
        string dirName,
        out string reason)
    {
        StringBuilder sb = new StringBuilder();

        int count = 0;
        Vector2 probeSize = GetProbeSize(dirName == "Left" || dirName == "Right" ? Vector2.right : Vector2.up);

        foreach (var bomb in Bomb.ActiveBombs)
        {
            count++;

            if (bomb == null)
            {
                sb.Append(" | bomb=NULL");
                continue;
            }

            if (bomb.HasExploded)
            {
                sb.Append($" | {bomb.name}:exploded-skip");
                continue;
            }

            bomb.TryGetComponent<Collider2D>(out var bombCol);

            Vector2 colliderCenter = bombCol != null
                ? (Vector2)bombCol.bounds.center
                : (Vector2)bomb.transform.position;

            bool probeHitsCollider = DoesProbeOverlapBombCollider(probePos, probeSize, bombCol);
            bool currentOverlapsTile = IsInsideBombTileFootprint(playerOrigin, colliderCenter);

            sb.Append(
                $" | bomb={bomb.name}" +
                $" solid={bomb.IsSolid}" +
                $" probeHitsCollider={probeHitsCollider}" +
                $" currentTileOverlap={currentOverlapsTile}" +
                $" colliderCenter={Fmt(colliderCenter)}");

            if (bomb.IsSolid)
            {
                if (probeHitsCollider)
                {
                    reason =
                        $"solid-bomb-block dir={dirName} bomb={bomb.name} " +
                        $"probe={Fmt(probePos)} probeSize={Fmt(probeSize)} colliderCenter={Fmt(colliderCenter)}";
                    return true;
                }

                continue;
            }

            if (probeHitsCollider && !currentOverlapsTile)
            {
                reason =
                    $"trigger-bomb-block dir={dirName} bomb={bomb.name} " +
                    $"probe={Fmt(probePos)} probeSize={Fmt(probeSize)} colliderCenter={Fmt(colliderCenter)} " +
                    $"currentTileOverlap={currentOverlapsTile}";
                return true;
            }
        }

        reason =
            $"no-bomb-block dir={dirName} activeBombs={count} player={Fmt(playerOrigin)} probe={Fmt(probePos)} probeSize={Fmt(probeSize)}{sb}";
        return false;
    }

    private bool IsValidCorneredBlock(Collider2D hit, out string reason)
    {
        if (hit == null)
        {
            reason = "hit-null";
            return false;
        }

        GameObject go = hit.gameObject;

        if (go.CompareTag(destructiblesTag))
        {
            reason = $"tag={destructiblesTag}";
            return true;
        }

        Transform t = go.transform;
        while (t != null)
        {
            if (t.name == indestructiblesName)
            {
                reason = $"ancestor={indestructiblesName}";
                return true;
            }

            t = t.parent;
        }

        reason = "not-destructible-or-indestructible";
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

            if (go.CompareTag(destructiblesTag))
            {
                LogCornered($"SameTileDestructible -> hit={go.name} origin={Fmt(origin)}");
                return true;
            }
        }

        return false;
    }

    private AnimatedSpriteRenderer GetCorneredRenderer()
    {
        if (movement == null)
            return corneredLoopRenderer;

        if (!movement.IsMounted)
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
        {
            LogCornered("StartCornered abort -> hasBombInBlock=false");
            return;
        }

        activeCorneredRenderer = GetCorneredRenderer();

        bool hasVisual = activeCorneredRenderer != null;

        isPlaying = true;

        LogCornered(
            $"StartCornered -> mounted={movement.IsMounted} hasVisual={hasVisual} " +
            $"renderer={(activeCorneredRenderer != null ? activeCorneredRenderer.name : "<null>")}");

        if (movement.IsMounted)
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
            if (movement != null && movement.IsMounted)
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

        LogCornered("StopCornered -> leaving cornered state");

        if (movement != null && movement.IsMounted)
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

    private bool ShouldLogStateChange(bool shouldPlay, bool bombBlocked, string reason)
    {
        if (!enableCorneredLogs)
            return false;

        if (!logOnlyStateChanges)
        {
            lastEvaluatedShouldPlay = shouldPlay;
            lastEvaluatedBombBlocked = bombBlocked;
            lastDecisionReason = reason;
            return true;
        }

        bool changed =
            lastEvaluatedShouldPlay != shouldPlay ||
            lastEvaluatedBombBlocked != bombBlocked ||
            lastDecisionReason != reason;

        lastEvaluatedShouldPlay = shouldPlay;
        lastEvaluatedBombBlocked = bombBlocked;
        lastDecisionReason = reason;

        return changed;
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

    private void LogCornered(string message)
    {
        if (!enableCorneredLogs)
            return;

        Debug.Log($"[CorneredAnimation][{name}] {message}", this);
    }

    private static string Fmt(Vector3 v)
    {
        return $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
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

        Gizmos.color = Color.yellow;

        for (int i = 0; i < cardinalDirs.Length; i++)
        {
            Vector2 probePos = GetProbePosition(origin, cardinalDirs[i]);
            Vector2 size = GetProbeSize(cardinalDirs[i]);
            Gizmos.DrawWireCube(probePos, size);
        }
    }
#endif
}