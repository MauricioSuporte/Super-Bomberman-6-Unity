using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode11ImpulseRopeController : MonoBehaviour, IIndestructibleKickedBombHandler
{
    const string BattleMode11SceneName = "BattleMode_11";

    [System.Serializable]
    public sealed class RopeTileAnimation
    {
        public TileBase tile;
        public TileBase[] animationTiles;
    }

    [Header("SFX")]
    [SerializeField] private AudioClip ropeBounceSfx;
    [SerializeField, Range(0f, 1f)] private float ropeBounceSfxVolume = 1f;
    [SerializeField] private AudioClip playerImpulseSfx;
    [SerializeField, Range(0f, 1f)] private float playerImpulseSfxVolume = 1f;

    [Header("Rope Deformation")]
    [SerializeField, Min(0.01f)] private float ropeDeformationSeconds = 0.25f;
    [SerializeField] private RopeTileAnimation[] ropeTileAnimations;

    [Header("Player Impulse")]
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField, Min(0.01f)] private float playerHoldToPreparingSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float playerPreparingSeconds = 0.5f;
    [SerializeField, Min(1)] private int playerImpulseTiles = 8;
    [SerializeField, Min(0.05f)] private float playerImpulseSeconds = 0.65f;
    [SerializeField, Min(0f)] private float preparingMaxOffsetTiles = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float bombCollisionBoxSize = 0.6f;
    [SerializeField] private LayerMask bombCollisionMask;

    readonly Dictionary<Vector3Int, RunningTileSwap> runningSwaps = new();
    readonly Dictionary<MovementController, PlayerRopeHoldState> playerHoldStates = new();
    readonly HashSet<MovementController> playersPreparingImpulse = new();
    readonly HashSet<MovementController> playersBeingImpulsed = new();
    readonly Collider2D[] bombHitBuffer = new Collider2D[8];

    sealed class RunningTileSwap
    {
        public Coroutine routine;
        public TileBase originalTile;
        public Tilemap tilemap;
    }

    sealed class PlayerRopeHoldState
    {
        public Vector3Int ropeCell;
        public Vector2 impactDirection;
        public float heldSeconds;
    }

    sealed class PlayerDashVisualState
    {
        public AnimatedSpriteRenderer up;
        public AnimatedSpriteRenderer down;
        public AnimatedSpriteRenderer left;
    }

    sealed class PlayerPreparingVisualState
    {
        public AnimatedSpriteRenderer up;
        public AnimatedSpriteRenderer down;
        public AnimatedSpriteRenderer left;
        public Vector3 upOriginalLocalPosition;
        public Vector3 downOriginalLocalPosition;
        public Vector3 leftOriginalLocalPosition;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapOnInitialScene()
    {
        EnsureForActiveScene();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    static void EnsureForActiveScene()
    {
        if (!Application.isPlaying)
            return;

        if (!IsBattleMode11Active())
            return;

        if (FindAnyObjectByType<BattleMode11ImpulseRopeController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode11ImpulseRopeController));
        host.AddComponent<BattleMode11ImpulseRopeController>();
    }

    void Awake()
    {
        if (!IsBattleMode11Active())
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        ResolveCollisionMasks();
    }

    void Update()
    {
        if (!IsBattleMode11Active())
            return;

        ResolveReferences();
        TickPlayerImpulseTriggers();
    }

    public bool TryHandleKickedBombBlocked(
        Bomb bomb,
        Vector2 currentWorldPos,
        Vector2 blockedWorldPos,
        Vector2 kickDirection,
        Tilemap indestructibleTilemap,
        Vector3Int blockedCell,
        TileBase blockedTile,
        out AudioClip bounceSfx,
        out float bounceSfxVolume)
    {
        bounceSfx = null;
        bounceSfxVolume = 1f;

        if (!IsBattleMode11Active() || bomb == null || blockedTile == null)
            return false;

        Vector2 dir = ToCardinal(kickDirection);
        if (!IsImpulseRopeImpact(blockedCell, dir))
            return false;

        StartRopeDeformation(indestructibleTilemap, blockedCell, dir);

        bounceSfx = ropeBounceSfx;
        bounceSfxVolume = ropeBounceSfxVolume;
        return true;
    }

    void StartRopeDeformation(Tilemap tilemap, Vector3Int hitCell, Vector2 impactDirection)
    {
        if (tilemap == null)
            return;

        if (!TryGetImpulseRopeSegment(hitCell, impactDirection, out Vector3Int startCell, out Vector3Int endCell))
            return;

        Vector3Int step = GetSegmentStep(startCell, endCell);
        if (step == Vector3Int.zero)
            return;

        Vector3Int cell = startCell;
        while (true)
        {
            TileBase originalTile = tilemap.GetTile(cell);
            if (!TryGetAnimationTiles(originalTile, out TileBase[] animationTiles))
                goto NEXT_CELL;

            StartTileSwap(tilemap, cell, originalTile, animationTiles);

        NEXT_CELL:
            if (cell == endCell)
                break;

            cell += step;
        }
    }

    void StartTileSwap(Tilemap tilemap, Vector3Int cell, TileBase originalTile, TileBase[] animationTiles)
    {
        if (runningSwaps.TryGetValue(cell, out RunningTileSwap running))
        {
            if (running.routine != null)
                StopCoroutine(running.routine);

            if (running.tilemap != null)
            {
                running.tilemap.SetTile(cell, running.originalTile);
                running.tilemap.RefreshTile(cell);
            }

            runningSwaps.Remove(cell);
        }

        RunningTileSwap swap = new()
        {
            originalTile = originalTile,
            tilemap = tilemap
        };

        swap.routine = StartCoroutine(TileSwapRoutine(cell, animationTiles, swap));
        runningSwaps[cell] = swap;
    }

    IEnumerator TileSwapRoutine(Vector3Int cell, TileBase[] animationTiles, RunningTileSwap swap)
    {
        float duration = Mathf.Max(0.01f, ropeDeformationSeconds);
        float elapsed = 0f;
        int lastFrame = -1;

        while (elapsed < duration)
        {
            if (swap.tilemap == null)
                yield break;

            float progress = Mathf.Clamp01(elapsed / duration);
            int frame = Mathf.Clamp(Mathf.FloorToInt(progress * animationTiles.Length), 0, animationTiles.Length - 1);

            if (frame != lastFrame)
            {
                swap.tilemap.SetTile(cell, animationTiles[frame]);
                swap.tilemap.RefreshTile(cell);
                lastFrame = frame;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (swap.tilemap != null)
        {
            swap.tilemap.SetTile(cell, swap.originalTile);
            swap.tilemap.RefreshTile(cell);
        }

        runningSwaps.Remove(cell);
    }

    bool TryGetAnimationTiles(TileBase tile, out TileBase[] animationTiles)
    {
        animationTiles = null;

        if (tile == null || ropeTileAnimations == null)
            return false;

        for (int i = 0; i < ropeTileAnimations.Length; i++)
        {
            RopeTileAnimation entry = ropeTileAnimations[i];
            if (entry == null || entry.tile != tile || entry.animationTiles == null || entry.animationTiles.Length == 0)
                continue;

            animationTiles = entry.animationTiles;
            return true;
        }

        return false;
    }

    void TickPlayerImpulseTriggers()
    {
        var input = PlayerInputManager.Instance;
        if (input == null || indestructibleTilemap == null)
        {
            playerHoldStates.Clear();
            return;
        }

        MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        HashSet<MovementController> seen = new();

        for (int i = 0; i < players.Length; i++)
        {
            MovementController player = players[i];
            if (player == null)
                continue;

            seen.Add(player);

            if (!CanStartPlayerImpulse(player))
            {
                playerHoldStates.Remove(player);
                continue;
            }

            Vector2 inputDirection = GetHeldCardinalDirection(input, player.PlayerId);
            if (inputDirection == Vector2.zero)
            {
                playerHoldStates.Remove(player);
                continue;
            }

            if (!TryGetPressedImpulseRope(player, inputDirection, out Vector3Int ropeCell))
            {
                playerHoldStates.Remove(player);
                continue;
            }

            if (!playerHoldStates.TryGetValue(player, out PlayerRopeHoldState state))
            {
                state = new PlayerRopeHoldState();
                playerHoldStates[player] = state;
            }

            if (state.ropeCell != ropeCell || state.impactDirection != inputDirection)
            {
                state.ropeCell = ropeCell;
                state.impactDirection = inputDirection;
                state.heldSeconds = 0f;
            }

            state.heldSeconds += Time.deltaTime;
            if (state.heldSeconds < playerHoldToPreparingSeconds)
                continue;

            playerHoldStates.Remove(player);
            StartCoroutine(PlayerPreparingImpulseRoutine(player, ropeCell, inputDirection));
        }

        PruneMissingPlayerHoldStates(seen);
    }

    bool CanStartPlayerImpulse(MovementController player)
    {
        return player != null &&
               !player.isDead &&
               !player.IsEndingStage &&
               !player.InputLocked &&
               !player.ExternalMovementOverride &&
               !playersPreparingImpulse.Contains(player) &&
               !playersBeingImpulsed.Contains(player);
    }

    bool TryGetPressedImpulseRope(MovementController player, Vector2 inputDirection, out Vector3Int ropeCell)
    {
        ropeCell = default;

        if (player == null || indestructibleTilemap == null || inputDirection == Vector2.zero)
            return false;

        float tileSize = Mathf.Max(0.0001f, player.tileSize);
        Vector2 position = player.Rigidbody != null ? player.Rigidbody.position : (Vector2)player.transform.position;
        Vector2 checkWorld = SnapToGrid(position, tileSize) + inputDirection * tileSize;
        ropeCell = indestructibleTilemap.WorldToCell(checkWorld);

        if (!IsImpulseRopeImpact(ropeCell, inputDirection))
            return false;

        return indestructibleTilemap.GetTile(ropeCell) != null;
    }

    IEnumerator PlayerPreparingImpulseRoutine(MovementController player, Vector3Int ropeCell, Vector2 impactDirection)
    {
        if (player == null || playersPreparingImpulse.Contains(player) || playersBeingImpulsed.Contains(player))
            yield break;

        playersPreparingImpulse.Add(player);
        Vector2 launchDirection = -impactDirection;
        float tileSize = Mathf.Max(0.0001f, player.tileSize);

        player.SetInputLocked(true, forceIdle: true, idleFacing: launchDirection);
        player.SetExternalMovementOverride(true);
        player.SetExternalMovementAllowsHazardDamage(true);

        MountMovementController mountMovement = player.GetComponentInChildren<MountMovementController>(true);

        PlayerPreparingVisualState preparingVisual = GetPlayerPreparingVisualState(player);
        player.SetExternalVisualSuppressed(true);
        ShowPlayerPreparingVisual(preparingVisual, launchDirection);

        float prepareDuration = Mathf.Max(0.01f, playerPreparingSeconds);
        float prepareElapsed = 0f;
        while (prepareElapsed < prepareDuration)
        {
            if (player == null || player.isDead)
                break;

            prepareElapsed += Time.deltaTime;
            UpdatePlayerPreparingOffset(preparingVisual, impactDirection, tileSize, prepareElapsed / prepareDuration);

            yield return null;
        }

        HidePlayerPreparingVisual(preparingVisual);
        playersPreparingImpulse.Remove(player);
        player.SetExternalMovementAllowsHazardDamage(false);

        if (player == null || player.isDead)
        {
            RestoreDeadPlayerAfterInterruptedImpulse(player, mountMovement);
            yield break;
        }

        StartRopeDeformation(indestructibleTilemap, ropeCell, impactDirection);
        PlayPlayerImpulseSfx(player);
        yield return PlayerImpulseRoutine(player, impactDirection, launchDirection, mountMovement);
    }

    void PlayPlayerImpulseSfx(MovementController player)
    {
        if (player == null || playerImpulseSfx == null)
            return;

        AudioSource source = player.GetComponent<AudioSource>();
        if (source == null)
            source = player.GetComponentInChildren<AudioSource>(true);

        if (source != null)
            source.PlayOneShot(playerImpulseSfx, playerImpulseSfxVolume);
    }

    IEnumerator PlayerImpulseRoutine(
        MovementController player,
        Vector2 impactDirection,
        Vector2 launchDirection,
        MountMovementController mountMovement)
    {
        playersBeingImpulsed.Add(player);

        float tileSize = Mathf.Max(0.0001f, player.tileSize);
        float duration = Mathf.Max(0.05f, playerImpulseSeconds);

        Rigidbody2D rb = player.Rigidbody;
        Vector2 start = rb != null ? rb.position : (Vector2)player.transform.position;
        start = SnapToGrid(start, tileSize);

        Vector2 target = start + launchDirection * (playerImpulseTiles * tileSize);
        Vector2 lastSafe = start;

        player.SetExplosionInvulnerable(true);

        CharacterHealth[] healths = player.GetComponentsInChildren<CharacterHealth>(true);
        float invulnerabilitySeconds = duration + 0.1f;
        for (int i = 0; i < healths.Length; i++)
        {
            if (healths[i] != null)
                healths[i].StartTemporaryInvulnerability(invulnerabilitySeconds, withBlink: false);
        }

        if (mountMovement != null)
            mountMovement.SetExplosionInvulnerable(true);

        PlayerDashVisualState dashVisual = GetPlayerDashVisualState(player);
        player.SetExternalVisualSuppressed(true);
        ShowPlayerDashVisual(dashVisual, launchDirection);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = start;
        }

        player.transform.position = start;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (player == null || player.isDead)
                break;

            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector2 next = Vector2.Lerp(start, target, eased);
            next = QuantizeToPixelGrid(next);

            if (HasBombAt(next, tileSize))
            {
                MovePlayerTo(player, rb, lastSafe);
                break;
            }

            MovePlayerTo(player, rb, next);
            lastSafe = next;

            yield return new WaitForFixedUpdate();
        }

        if (player != null)
        {
            Vector2 final = rb != null ? rb.position : (Vector2)player.transform.position;
            final = SnapToGrid(final, tileSize);
            MovePlayerTo(player, rb, final);

            HidePlayerDashVisual(dashVisual);
            RestorePlayerAfterImpulse(player, launchDirection, mountMovement);
        }

        playersBeingImpulsed.Remove(player);
    }

    void RestorePlayerAfterImpulse(MovementController player, Vector2 launchDirection, MountMovementController mountMovement)
    {
        if (player == null)
            return;

        player.SetExternalVisualSuppressed(false);
        player.SetExternalMovementAllowsHazardDamage(false);
        player.SetExplosionInvulnerable(false);
        player.SetExternalMovementOverride(false);
        player.SetInputLocked(false);
        player.ForceIdleFacing(launchDirection, "BattleMode11ImpulseRope");

        if (mountMovement != null)
            mountMovement.SetExplosionInvulnerable(false);
    }

    void RestoreDeadPlayerAfterInterruptedImpulse(MovementController player, MountMovementController mountMovement)
    {
        if (player == null)
            return;

        player.SetExternalVisualSuppressed(false);
        player.SetExternalMovementAllowsHazardDamage(false);
        player.SetExplosionInvulnerable(false);
        player.SetExternalMovementOverride(false);

        if (mountMovement != null)
            mountMovement.SetExplosionInvulnerable(false);
    }

    static PlayerDashVisualState GetPlayerDashVisualState(MovementController player)
    {
        return new PlayerDashVisualState
        {
            up = FindAnimatedChild(player.transform, "DashUp"),
            down = FindAnimatedChild(player.transform, "DashDown"),
            left = FindAnimatedChild(player.transform, "DashLeft")
        };
    }

    static PlayerPreparingVisualState GetPlayerPreparingVisualState(MovementController player)
    {
        PlayerPreparingVisualState state = new()
        {
            up = FindAnimatedChild(player.transform, "PreparingUp"),
            down = FindAnimatedChild(player.transform, "PreparingDown"),
            left = FindAnimatedChild(player.transform, "PreparingLeft")
        };

        state.upOriginalLocalPosition = GetLocalPosition(state.up);
        state.downOriginalLocalPosition = GetLocalPosition(state.down);
        state.leftOriginalLocalPosition = GetLocalPosition(state.left);

        return state;
    }

    static AnimatedSpriteRenderer FindAnimatedChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName && child.TryGetComponent(out AnimatedSpriteRenderer renderer))
                return renderer;
        }

        return null;
    }

    static void ShowPlayerDashVisual(PlayerDashVisualState state, Vector2 direction)
    {
        HidePlayerDashVisual(state);

        AnimatedSpriteRenderer target = PickDashRenderer(state, direction, out bool flipX);
        if (target == null)
            return;

        target.enabled = true;
        target.idle = false;
        target.loop = true;
        target.CurrentFrame = 0;
        target.RefreshFrame();

        if (target.TryGetComponent(out SpriteRenderer sr) && sr != null)
        {
            sr.enabled = true;
            sr.flipX = flipX;
        }
    }

    static AnimatedSpriteRenderer PickDashRenderer(PlayerDashVisualState state, Vector2 direction, out bool flipX)
    {
        flipX = false;

        if (state == null)
            return null;

        if (direction == Vector2.up)
            return state.up;

        if (direction == Vector2.down)
            return state.down;

        if (direction == Vector2.right)
        {
            flipX = true;
            return state.left;
        }

        return state.left;
    }

    static void HidePlayerDashVisual(PlayerDashVisualState state)
    {
        if (state == null)
            return;

        SetDashRendererVisible(state.up, false, flipX: false);
        SetDashRendererVisible(state.down, false, flipX: false);
        SetDashRendererVisible(state.left, false, flipX: false);
    }

    static void SetDashRendererVisible(AnimatedSpriteRenderer renderer, bool visible, bool flipX)
    {
        if (renderer == null)
            return;

        renderer.enabled = visible;

        if (renderer.TryGetComponent(out SpriteRenderer sr) && sr != null)
        {
            sr.enabled = visible;
            sr.flipX = flipX;
        }
    }

    static void ShowPlayerPreparingVisual(PlayerPreparingVisualState state, Vector2 direction)
    {
        HidePlayerPreparingVisual(state);

        AnimatedSpriteRenderer target = PickPreparingRenderer(state, direction, out bool flipX);
        if (target == null)
            return;

        target.enabled = true;
        target.idle = false;
        target.loop = true;
        target.CurrentFrame = 0;
        target.RefreshFrame();

        if (target.TryGetComponent(out SpriteRenderer sr) && sr != null)
        {
            sr.enabled = true;
            sr.flipX = flipX;
        }
    }

    static AnimatedSpriteRenderer PickPreparingRenderer(PlayerPreparingVisualState state, Vector2 direction, out bool flipX)
    {
        flipX = false;

        if (state == null)
            return null;

        if (direction == Vector2.up)
            return state.up;

        if (direction == Vector2.down)
            return state.down;

        if (direction == Vector2.right)
        {
            flipX = true;
            return state.left;
        }

        return state.left;
    }

    static void HidePlayerPreparingVisual(PlayerPreparingVisualState state)
    {
        if (state == null)
            return;

        RestorePreparingLocalPositions(state);
        SetDashRendererVisible(state.up, false, flipX: false);
        SetDashRendererVisible(state.down, false, flipX: false);
        SetDashRendererVisible(state.left, false, flipX: false);
    }

    void UpdatePlayerPreparingOffset(
        PlayerPreparingVisualState preparingVisual,
        Vector2 offsetDirection,
        float tileSize,
        float normalizedProgress)
    {
        if (preparingVisual == null)
            return;

        AnimatedSpriteRenderer target = PickPreparingRenderer(preparingVisual, -offsetDirection, out _);
        if (target == null)
            return;

        float progress = Mathf.Clamp01(normalizedProgress);
        float distance = Mathf.Max(0f, preparingMaxOffsetTiles) * Mathf.Max(0.0001f, tileSize) * progress;

        Vector3 original = GetPreparingOriginalLocalPosition(preparingVisual, target);
        target.transform.localPosition = original + (Vector3)(offsetDirection * distance);
    }

    static Vector3 GetPreparingOriginalLocalPosition(PlayerPreparingVisualState state, AnimatedSpriteRenderer renderer)
    {
        if (renderer == state.up)
            return state.upOriginalLocalPosition;

        if (renderer == state.down)
            return state.downOriginalLocalPosition;

        return state.leftOriginalLocalPosition;
    }

    static void RestorePreparingLocalPositions(PlayerPreparingVisualState state)
    {
        RestoreLocalPosition(state.up, state.upOriginalLocalPosition);
        RestoreLocalPosition(state.down, state.downOriginalLocalPosition);
        RestoreLocalPosition(state.left, state.leftOriginalLocalPosition);
    }

    static Vector3 GetLocalPosition(AnimatedSpriteRenderer renderer)
        => renderer != null ? renderer.transform.localPosition : Vector3.zero;

    static void RestoreLocalPosition(AnimatedSpriteRenderer renderer, Vector3 localPosition)
    {
        if (renderer != null)
            renderer.transform.localPosition = localPosition;
    }

    void PruneMissingPlayerHoldStates(HashSet<MovementController> seen)
    {
        if (playerHoldStates.Count == 0)
            return;

        List<MovementController> remove = null;
        foreach (var pair in playerHoldStates)
        {
            if (pair.Key != null && seen.Contains(pair.Key))
                continue;

            remove ??= new List<MovementController>();
            remove.Add(pair.Key);
        }

        if (remove == null)
            return;

        for (int i = 0; i < remove.Count; i++)
            playerHoldStates.Remove(remove[i]);
    }

    bool HasBombAt(Vector2 worldPos, float tileSize)
    {
        int mask = bombCollisionMask.value != 0 ? bombCollisionMask.value : LayerMask.GetMask("Bomb");
        if (mask == 0)
            return false;

        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = true
        };

        int count = Physics2D.OverlapBox(
            worldPos,
            Vector2.one * Mathf.Max(0.01f, tileSize * bombCollisionBoxSize),
            0f,
            filter,
            bombHitBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = bombHitBuffer[i];
            bombHitBuffer[i] = null;

            if (hit == null)
                continue;

            GameObject go = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (go != null && go.TryGetComponent<Bomb>(out _))
                return true;
        }

        return false;
    }

    void ResolveReferences()
    {
        if (indestructibleTilemap != null)
            return;

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.indestructibleTilemap != null)
        {
            indestructibleTilemap = gm.indestructibleTilemap;
            return;
        }

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap != null && tilemap.name.ToLowerInvariant().Contains("indestruct"))
            {
                indestructibleTilemap = tilemap;
                return;
            }
        }
    }

    void ResolveCollisionMasks()
    {
        if (bombCollisionMask.value == 0)
            bombCollisionMask = LayerMask.GetMask("Bomb");
    }

    static Vector2 GetHeldCardinalDirection(PlayerInputManager input, int playerId)
    {
        bool up = input.Get(playerId, PlayerAction.MoveUp);
        bool down = input.Get(playerId, PlayerAction.MoveDown);
        bool left = input.Get(playerId, PlayerAction.MoveLeft);
        bool right = input.Get(playerId, PlayerAction.MoveRight);

        if (up && !down)
            return Vector2.up;

        if (down && !up)
            return Vector2.down;

        if (left && !right)
            return Vector2.left;

        if (right && !left)
            return Vector2.right;

        return Vector2.zero;
    }

    static void MovePlayerTo(MovementController player, Rigidbody2D rb, Vector2 worldPos)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.MovePosition(worldPos);
        }

        player.transform.position = worldPos;
    }

    static Vector2 SnapToGrid(Vector2 worldPos, float tileSize)
    {
        worldPos.x = Mathf.Round(worldPos.x / tileSize) * tileSize;
        worldPos.y = Mathf.Round(worldPos.y / tileSize) * tileSize;
        return worldPos;
    }

    static Vector2 QuantizeToPixelGrid(Vector2 worldPos)
    {
        const float pixelsPerUnit = 16f;
        worldPos.x = Mathf.Round(worldPos.x * pixelsPerUnit) / pixelsPerUnit;
        worldPos.y = Mathf.Round(worldPos.y * pixelsPerUnit) / pixelsPerUnit;
        return worldPos;
    }

    static bool IsImpulseRopeImpact(Vector3Int cell, Vector2 direction)
    {
        if (direction == Vector2.up)
            return cell.y == 5 && cell.x >= -7 && cell.x <= 5;

        if (direction == Vector2.right)
            return cell.x == 6 && cell.y >= -6 && cell.y <= 4;

        if (direction == Vector2.down)
            return cell.y == -7 && cell.x >= -7 && cell.x <= 5;

        if (direction == Vector2.left)
            return cell.x == -8 && cell.y >= -6 && cell.y <= 4;

        return false;
    }

    static bool TryGetImpulseRopeSegment(
        Vector3Int hitCell,
        Vector2 direction,
        out Vector3Int startCell,
        out Vector3Int endCell)
    {
        startCell = default;
        endCell = default;

        if (direction == Vector2.up && hitCell.y == 5 && hitCell.x >= -7 && hitCell.x <= 5)
        {
            startCell = new Vector3Int(-7, 5, 0);
            endCell = new Vector3Int(5, 5, 0);
            return true;
        }

        if (direction == Vector2.right && hitCell.x == 6 && hitCell.y >= -6 && hitCell.y <= 4)
        {
            startCell = new Vector3Int(6, 4, 0);
            endCell = new Vector3Int(6, -6, 0);
            return true;
        }

        if (direction == Vector2.down && hitCell.y == -7 && hitCell.x >= -7 && hitCell.x <= 5)
        {
            startCell = new Vector3Int(5, -7, 0);
            endCell = new Vector3Int(-7, -7, 0);
            return true;
        }

        if (direction == Vector2.left && hitCell.x == -8 && hitCell.y >= -6 && hitCell.y <= 4)
        {
            startCell = new Vector3Int(-8, -6, 0);
            endCell = new Vector3Int(-8, 4, 0);
            return true;
        }

        return false;
    }

    static Vector3Int GetSegmentStep(Vector3Int startCell, Vector3Int endCell)
    {
        Vector3Int delta = endCell - startCell;

        if (delta.x != 0)
            return new Vector3Int(delta.x > 0 ? 1 : -1, 0, 0);

        if (delta.y != 0)
            return new Vector3Int(0, delta.y > 0 ? 1 : -1, 0);

        return Vector3Int.zero;
    }

    static Vector2 ToCardinal(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x > 0f ? Vector2.right : Vector2.left;

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    static bool IsBattleMode11Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode11SceneName, System.StringComparison.Ordinal);
}
