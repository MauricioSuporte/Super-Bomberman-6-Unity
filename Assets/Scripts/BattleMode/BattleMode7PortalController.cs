using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode7PortalController : MonoBehaviour
{
    const string BattleMode7SceneName = "BattleMode_7";
    const string DefaultEnterSfxResourcesPath = "Sounds/start";

    [Header("Portal Cells")]
    [SerializeField]
    private Vector2Int[] portalCells =
    {
        new(-5, 2),
        new(3, 2),
        new(3, -4),
        new(-5, -4),
    };

    [Header("Teleport")]
    [SerializeField, Min(0.01f)] private float teleportSeconds = 0.25f;
    [SerializeField, Min(0f)] private float retriggerGraceSeconds = 0.05f;
    [SerializeField] private bool snapToDestinationCenter = true;

    [Header("SFX")]
    [SerializeField] private AudioClip enterSfx;
    [SerializeField, Range(0f, 1f)] private float enterSfxVolume = 1f;

    Tilemap groundTilemap;
    AudioSource audioSource;

    readonly HashSet<MovementController> activeTeleporters = new();
    readonly Dictionary<MovementController, Vector3Int> waitingForPortalExit = new();
    readonly Dictionary<MovementController, TeleportState> activeStates = new();

    sealed class TeleportState
    {
        public bool prevInputLocked;
        public bool prevPlayerExplosionInvulnerable;
        public bool prevMountExplosionInvulnerable;
        public bool prevBombEnabled;
        public bool hadBombController;
        public bool prevColliderEnabled;
        public BombController bombController;
        public Collider2D playerCollider;
        public MountMovementController mountMovement;
        public CharacterHealth[] healths;
        public PlayerMountCompanion mountCompanion;
        public MountEggQueue eggQueue;
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
        if (!string.Equals(activeScene.name, BattleMode7SceneName, System.StringComparison.Ordinal))
            return;

        if (FindAnyObjectByType<BattleMode7PortalController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode7PortalController));
        host.AddComponent<BattleMode7PortalController>();
    }

    void Awake()
    {
        if (!IsBattleMode7Active())
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        EnsureAudioSource();
        LoadDefaultSfxIfNeeded();
    }

    void OnDisable()
    {
        foreach (var pair in activeStates)
            RestoreTeleportState(pair.Key, pair.Value);

        activeStates.Clear();
        activeTeleporters.Clear();
        waitingForPortalExit.Clear();
    }

    void Update()
    {
        if (!IsBattleMode7Active())
            return;

        ResolveReferences();

        if (portalCells == null || portalCells.Length < 2)
            return;

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
            TryHandlePlayer(players[i]);
    }

    void TryHandlePlayer(MovementController mover)
    {
        if (mover == null || mover.Rigidbody == null || !mover.CompareTag("Player"))
            return;

        if (mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
            return;

        Vector3Int currentCell = WorldToCell(mover.Rigidbody.position);

        if (waitingForPortalExit.TryGetValue(mover, out Vector3Int blockedCell))
        {
            if (currentCell == blockedCell)
                return;

            waitingForPortalExit.Remove(mover);
        }

        if (activeTeleporters.Contains(mover))
            return;

        int portalIndex = GetPortalIndex(currentCell);
        if (portalIndex < 0)
            return;

        int destinationIndex = GetDiagonalDestinationIndex(portalIndex);
        StartCoroutine(TeleportRoutine(mover, portalIndex, destinationIndex));
    }

    IEnumerator TeleportRoutine(MovementController mover, int sourceIndex, int destinationIndex)
    {
        if (mover == null || mover.Rigidbody == null)
            yield break;

        activeTeleporters.Add(mover);

        Vector3Int sourceCell = ToCell(portalCells[sourceIndex]);
        Vector3Int destinationCell = ToCell(portalCells[destinationIndex]);
        Vector2 source = GetCellCenter(sourceCell);
        Vector2 destination = GetCellCenter(destinationCell);

        TeleportState state = CaptureAndApplyTeleportState(mover);
        activeStates[mover] = state;

        PlayEnterSfx();

        try
        {
            float duration = Mathf.Max(0.01f, teleportSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (mover == null || mover.Rigidbody == null)
                    yield break;

                if (GamePauseController.IsPaused)
                {
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 position = Vector2.Lerp(source, destination, SmoothTeleportT(t));

                mover.Rigidbody.position = position;
                mover.Rigidbody.linearVelocity = Vector2.zero;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (mover != null)
            {
                Vector2 finalPosition = snapToDestinationCenter ? destination : mover.Rigidbody.position;
                mover.SnapToWorldPoint(finalPosition, roundToGrid: false);

                if (state.eggQueue != null)
                    state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
            }
        }
        finally
        {
            RestoreTeleportState(mover, state);
            activeStates.Remove(mover);
            StartCoroutine(ReleaseTeleporterAfterGrace(mover, destinationCell));
        }
    }

    TeleportState CaptureAndApplyTeleportState(MovementController mover)
    {
        var state = new TeleportState
        {
            prevInputLocked = mover.InputLocked,
            prevPlayerExplosionInvulnerable = mover.explosionInvulnerable,
            playerCollider = mover.GetComponent<Collider2D>(),
            mountMovement = mover.GetComponentInChildren<MountMovementController>(true),
            healths = mover.GetComponentsInChildren<CharacterHealth>(true),
            mountCompanion = mover.GetComponent<PlayerMountCompanion>(),
            eggQueue = mover.GetComponentInChildren<MountEggQueue>(true),
        };

        state.prevColliderEnabled = state.playerCollider != null && state.playerCollider.enabled;
        state.prevMountExplosionInvulnerable = state.mountMovement != null && state.mountMovement.explosionInvulnerable;
        state.hadBombController = mover.TryGetComponent(out state.bombController) && state.bombController != null;
        state.prevBombEnabled = state.hadBombController && state.bombController.enabled;

        mover.SetInputLocked(true, forceIdle: false);
        mover.SetExternalMovementOverride(true);
        mover.SetVisualOverrideActive(true);
        mover.SetAllSpritesVisible(false);
        mover.SetExplosionInvulnerable(true);

        if (state.bombController != null)
            state.bombController.enabled = false;

        if (state.playerCollider != null)
            state.playerCollider.enabled = false;

        if (state.mountMovement != null)
            state.mountMovement.SetExplosionInvulnerable(true);

        if (state.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(false);

        if (state.eggQueue != null)
            state.eggQueue.ForceVisible(false);

        SetHealthInvulnerability(state.healths, true);

        return state;
    }

    void RestoreTeleportState(MovementController mover, TeleportState state)
    {
        if (state == null)
            return;

        if (mover != null)
        {
            mover.SetExternalMovementOverride(false);
            mover.SetInputLocked(state.prevInputLocked, forceIdle: false);
            mover.SetVisualOverrideActive(false);
            mover.EnableExclusiveFromState();
            mover.SetExplosionInvulnerable(state.prevPlayerExplosionInvulnerable);

            if (mover.Rigidbody != null)
                mover.Rigidbody.linearVelocity = Vector2.zero;
        }

        if (state.mountMovement != null)
            state.mountMovement.SetExplosionInvulnerable(state.prevMountExplosionInvulnerable);

        if (state.bombController != null)
            state.bombController.enabled = state.prevBombEnabled;

        if (state.playerCollider != null)
            state.playerCollider.enabled = state.prevColliderEnabled;

        if (state.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(true);

        if (state.eggQueue != null)
        {
            state.eggQueue.ForceVisible(true);
            state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
        }

        SetHealthInvulnerability(state.healths, false);
    }

    IEnumerator ReleaseTeleporterAfterGrace(MovementController mover, Vector3Int destinationCell)
    {
        if (retriggerGraceSeconds > 0f)
            yield return new WaitForSeconds(retriggerGraceSeconds);

        activeTeleporters.Remove(mover);

        if (mover != null)
            waitingForPortalExit[mover] = destinationCell;
    }

    void ResolveReferences()
    {
        if (groundTilemap == null)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                groundTilemap = gm.groundTilemap;
        }

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("ground");

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("Ground");
    }

    void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    void LoadDefaultSfxIfNeeded()
    {
        if (enterSfx == null)
            enterSfx = Resources.Load<AudioClip>(DefaultEnterSfxResourcesPath);
    }

    void PlayEnterSfx()
    {
        EnsureAudioSource();
        LoadDefaultSfxIfNeeded();

        if (enterSfx != null && audioSource != null)
            audioSource.PlayOneShot(enterSfx, enterSfxVolume);
    }

    int GetPortalIndex(Vector3Int cell)
    {
        for (int i = 0; i < portalCells.Length; i++)
        {
            if (ToCell(portalCells[i]) == cell)
                return i;
        }

        return -1;
    }

    Vector3Int WorldToCell(Vector2 worldPos)
    {
        if (groundTilemap != null)
            return groundTilemap.WorldToCell(worldPos);

        return new Vector3Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), 0);
    }

    Vector2 GetCellCenter(Vector3Int cell)
    {
        if (groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);

        return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }

    static void SetHealthInvulnerability(CharacterHealth[] healths, bool enabled)
    {
        if (healths == null)
            return;

        for (int i = 0; i < healths.Length; i++)
        {
            if (healths[i] != null)
                healths[i].SetExternalInvulnerability(enabled);
        }
    }

    static Vector3Int ToCell(Vector2Int cell)
        => new(cell.x, cell.y, 0);

    int GetDiagonalDestinationIndex(int portalIndex)
    {
        if (portalCells == null || portalCells.Length != 4)
            return (portalIndex + 1) % portalCells.Length;

        return portalIndex < 2 ? portalIndex + 2 : portalIndex - 2;
    }

    static float SmoothTeleportT(float t)
        => t * t * (3f - 2f * t);

    static bool IsBattleMode7Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode7SceneName, System.StringComparison.Ordinal);

    static Tilemap FindTilemapByName(string tilemapName)
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] != null && string.Equals(tilemaps[i].name, tilemapName, System.StringComparison.OrdinalIgnoreCase))
                return tilemaps[i];
        }

        return null;
    }
}
