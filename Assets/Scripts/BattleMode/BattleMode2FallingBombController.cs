using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode2FallingBombController : MonoBehaviour
{
    const string BattleMode2SceneName = "BattleMode_2";
    const string TargetSpriteResourcesPath = "Sprites/MadBomber/target";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float initialDelaySeconds = 10f;
    [SerializeField, Min(0f)] private float targetVisibleSeconds = 2f;

    [Header("Arena Cells")]
    [SerializeField] private Vector2Int minCell = new(-7, -6);
    [SerializeField] private Vector2Int maxCell = new(5, 4);
    [SerializeField, Min(0f)] private float spawnHeightAboveTopCell = 3f;

    [Header("Bomb")]
    [SerializeField, Min(0.1f)] private float bombFuseSeconds = 2f;
    [SerializeField, Min(1)] private int bombFireLevel = 1;
    [SerializeField] private GameObject bombPrefabOverride;

    [Header("Target Visual")]
    [SerializeField] private Sprite targetSprite;
    [SerializeField] private string targetSortingLayerName = "Default";
    [SerializeField] private int targetSortingOrder = 20;
    [SerializeField, Range(0f, 1f)] private float targetMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float targetMaxAlpha = 0.75f;
    [SerializeField, Min(0.01f)] private float targetBlinkSpeed = 6f;

    BombController bombOwner;
    BattleSuddenDeathController suddenDeathController;
    Tilemap groundTilemap;
    Tilemap indestructibleTilemap;
    Tilemap destructibleTilemap;
    SpriteRenderer targetRenderer;
    Coroutine loopRoutine;
    bool targetWarningActive;
    Vector2 targetWarningWorldPosition;
    float targetWarningSecondsRemaining;

    public bool TryGetActiveTargetWarning(
        out Vector2 worldPosition,
        out int explosionRadius,
        out float secondsUntilExplosion)
    {
        worldPosition = targetWarningWorldPosition;
        explosionRadius = Mathf.Max(1, bombFireLevel);
        secondsUntilExplosion =
            Mathf.Max(0f, targetWarningSecondsRemaining) +
            Mathf.Max(0.01f, bombFuseSeconds);
        return targetWarningActive;
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

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, BattleMode2SceneName, System.StringComparison.Ordinal))
            return;

        if (FindAnyObjectByType<BattleMode2FallingBombController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode2FallingBombController));
        host.AddComponent<BattleMode2FallingBombController>();
    }

    void Awake()
    {
        if (!IsBattleMode2Active())
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        EnsureTargetRenderer();
    }

    void OnEnable()
    {
        if (loopRoutine == null && IsBattleMode2Active())
            loopRoutine = StartCoroutine(FallingBombLoop());
    }

    void OnDisable()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        HideTarget();
    }

    void OnDestroy()
    {
        if (targetRenderer != null)
            Destroy(targetRenderer.gameObject);
    }

    IEnumerator FallingBombLoop()
    {
        yield return WaitGameplaySeconds(initialDelaySeconds);

        while (IsBattleMode2Active() && !HasSuddenDeathStarted())
        {
            ResolveReferences();

            if (bombOwner == null)
            {
                yield return null;
                continue;
            }

            Vector3Int targetCell = PickTargetCell();
            Vector2 targetWorld = GetCellCenterWorld(targetCell);

            yield return ShowTarget(targetWorld, targetVisibleSeconds);

            if (HasSuddenDeathStarted())
                break;

            Bomb launchedBomb = LaunchBomb(targetWorld);
            if (launchedBomb == null)
            {
                yield return null;
                continue;
            }

            while (launchedBomb != null && !launchedBomb.HasExploded && !HasSuddenDeathStarted())
                yield return null;
        }

        HideTarget();
        loopRoutine = null;
    }

    IEnumerator WaitGameplaySeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && IsBattleMode2Active() && !HasSuddenDeathStarted())
        {
            if (!GamePauseController.IsPaused)
                elapsed += Time.deltaTime;

            yield return null;
        }
    }

    IEnumerator ShowTarget(Vector2 worldPosition, float seconds)
    {
        EnsureTargetRenderer();

        targetWarningActive = true;
        targetWarningWorldPosition = worldPosition;
        targetWarningSecondsRemaining = Mathf.Max(0f, seconds);

        if (targetRenderer != null)
        {
            targetRenderer.transform.position = new Vector3(
                worldPosition.x,
                worldPosition.y,
                targetRenderer.transform.position.z);
            targetRenderer.enabled = true;
        }

        float elapsed = 0f;
        while (elapsed < seconds && IsBattleMode2Active() && !HasSuddenDeathStarted())
        {
            if (!GamePauseController.IsPaused)
            {
                elapsed += Time.deltaTime;
                targetWarningSecondsRemaining = Mathf.Max(0f, seconds - elapsed);

                if (targetRenderer != null)
                {
                    float t = Mathf.PingPong(Time.time * targetBlinkSpeed, 1f);
                    Color c = targetRenderer.color;
                    c.a = Mathf.Lerp(targetMinAlpha, targetMaxAlpha, t);
                    targetRenderer.color = c;
                }
            }

            yield return null;
        }

        HideTarget();
    }

    Bomb LaunchBomb(Vector2 targetWorld)
    {
        GameObject bombPrefab = ResolveBombPrefab();
        if (bombPrefab == null || bombOwner == null)
            return null;

        MovementController ownerMovement = bombOwner.GetComponent<MovementController>();
        float tileSize = ownerMovement != null ? Mathf.Max(0.01f, ownerMovement.tileSize) : 1f;
        int launchStartY = maxCell.y + 1;
        int distanceTiles = Mathf.Max(1, launchStartY - Mathf.RoundToInt(targetWorld.y));
        Vector2 logicalStart = targetWorld + (Vector2.up * distanceTiles * tileSize);

        GameObject bombObject = Instantiate(bombPrefab, logicalStart, Quaternion.identity);
        if (bombObject == null)
            return null;

        if (!bombObject.TryGetComponent(out Bomb bomb))
            bomb = bombObject.AddComponent<Bomb>();

        bomb.IsPowerBomb = false;
        bomb.IsControlBomb = false;
        bomb.IsPierceBomb = false;
        bomb.IsRubberBomb = false;
        bomb.IsRevengeBomb = false;
        bomb.ExplosionRadiusOverride = Mathf.Max(1, bombFireLevel);
        bomb.SetStageBoundsTilemap(indestructibleTilemap != null ? indestructibleTilemap : groundTilemap);
        bomb.SetFuseSeconds(bombFuseSeconds);
        bomb.Initialize(bombOwner);

        if (bombObject.TryGetComponent(out Collider2D bombCollider))
            bombCollider.isTrigger = true;

        if (!bombObject.TryGetComponent(out BombAtGroundTileNotifier notifier))
            notifier = bombObject.AddComponent<BombAtGroundTileNotifier>();

        notifier.Initialize(bombOwner);

        bomb.BeginFuse();

        LayerMask obstacleMask = ownerMovement != null
            ? ownerMovement.obstacleMask | LayerMask.GetMask("Enemy", "Bomb", "Player")
            : LayerMask.GetMask("Stage", "Enemy", "Bomb", "Player");

        bool launched = bomb.StartPunch(
            Vector2.down,
            tileSize,
            distanceTiles,
            obstacleMask,
            bombOwner.destructibleTiles,
            visualStartYOffset: spawnHeightAboveTopCell,
            logicalOriginOverride: logicalStart);

        if (!launched)
        {
            Destroy(bombObject);
            return null;
        }

        return bomb;
    }

    Vector3Int PickTargetCell()
    {
        for (int i = 0; i < 24; i++)
        {
            Vector3Int candidate = new(
                Random.Range(minCell.x, maxCell.x + 1),
                Random.Range(minCell.y, maxCell.y + 1),
                0);

            if (IsPlayableCell(candidate))
                return candidate;
        }

        return new Vector3Int(
            Random.Range(minCell.x, maxCell.x + 1),
            Random.Range(minCell.y, maxCell.y + 1),
            0);
    }

    bool IsPlayableCell(Vector3Int cell)
    {
        if (groundTilemap != null && !groundTilemap.HasTile(cell))
            return false;

        if (indestructibleTilemap != null && indestructibleTilemap.HasTile(cell))
            return false;

        if (destructibleTilemap != null && destructibleTilemap.HasTile(cell))
            return false;

        return true;
    }

    Vector2 GetCellCenterWorld(Vector3Int cell)
    {
        Tilemap tilemap = groundTilemap != null ? groundTilemap : indestructibleTilemap;
        if (tilemap == null)
            return new Vector2(cell.x, cell.y);

        Vector3 world = tilemap.GetCellCenterWorld(cell);
        return new Vector2(world.x, world.y);
    }

    void ResolveReferences()
    {
        if (GameManager.Instance != null)
        {
            groundTilemap = GameManager.Instance.groundTilemap;
            indestructibleTilemap = GameManager.Instance.indestructibleTilemap;
            destructibleTilemap = GameManager.Instance.destructibleTilemap;
        }

        if (suddenDeathController == null)
            suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();

        if (bombOwner == null)
        {
            BombController[] controllers = FindObjectsByType<BombController>(FindObjectsInactive.Include);
            for (int i = 0; i < controllers.Length; i++)
            {
                BombController controller = controllers[i];
                if (controller != null && controller.CompareTag("Player"))
                {
                    bombOwner = controller;
                    break;
                }
            }

            if (bombOwner == null && controllers.Length > 0)
                bombOwner = controllers[0];
        }
    }

    GameObject ResolveBombPrefab()
    {
        if (bombPrefabOverride != null)
            return bombPrefabOverride;

        ResolveReferences();
        return bombOwner != null ? bombOwner.bombPrefab : null;
    }

    void EnsureTargetRenderer()
    {
        if (targetRenderer != null)
            return;

        if (targetSprite == null)
            targetSprite = Resources.Load<Sprite>(TargetSpriteResourcesPath);

        if (targetSprite == null)
            return;

        var targetObject = new GameObject("BattleMode2FallingBombTarget");
        targetObject.transform.SetParent(transform, false);
        targetRenderer = targetObject.AddComponent<SpriteRenderer>();
        targetRenderer.sprite = targetSprite;
        targetRenderer.sortingLayerName = targetSortingLayerName;
        targetRenderer.sortingOrder = targetSortingOrder;
        targetRenderer.enabled = false;

        Color c = Color.white;
        c.a = targetMaxAlpha;
        targetRenderer.color = c;
    }

    void HideTarget()
    {
        targetWarningActive = false;
        targetWarningSecondsRemaining = 0f;

        if (targetRenderer == null)
            return;

        targetRenderer.enabled = false;
    }

    bool HasSuddenDeathStarted()
    {
        if (suddenDeathController != null && suddenDeathController.SuddenDeathStarted)
            return true;

        if (BattleModeRules.Instance == null ||
            (!BattleModeRules.Instance.EnableSuddenDeath &&
             !BattleModeRules.Instance.UseReducedSuddenDeath))
            return false;

        if (GameManager.Instance == null || !GameManager.Instance.HasBattleTimeLimit)
            return false;

        return GameManager.Instance.BattleTimeRemainingSeconds <= 50f;
    }

    static bool IsBattleMode2Active()
    {
        return string.Equals(
            SceneManager.GetActiveScene().name,
            BattleMode2SceneName,
            System.StringComparison.Ordinal);
    }
}
