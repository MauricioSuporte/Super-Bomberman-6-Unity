using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AudioSource))]
public sealed class EndStageGate : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip enterSfx;

    [Tooltip("Som tocado quando o portão começa a abrir")]
    public AudioClip openGateSfx;

    [Header("Music")]
    public AudioClip endStageMusic;

    [Header("Gate Tilemap")]
    public Tilemap gateTilemap;

    [Header("Closed Gate Tiles")]
    public TileBase closed00;
    public TileBase closed10;
    public TileBase closed20;
    public TileBase closed01;
    public TileBase closed11;
    public TileBase closed21;

    [Header("Mid Gate Tiles (Optional)")]
    public TileBase mid00;
    public TileBase mid10;
    public TileBase mid20;
    public TileBase mid01;
    public TileBase mid11;
    public TileBase mid21;

    [Header("Open Gate Tiles")]
    public TileBase open00;
    public TileBase open10;
    public TileBase open20;
    public TileBase open01;
    public TileBase open11;
    public TileBase open21;

    [Header("Open Transition")]
    [Min(0f)]
    public float midStepDelay = 0.18f;

    [Header("Entry (Open)")]
    public TileBase entryOpenTile;

    [Header("Trigger")]
    public BoxCollider2D gateTrigger;

    public Vector2 triggerSize = new(0.1f, 0.1f);

    [Header("Open Delay")]
    public float openDelay = 1f;

    bool isActivated;
    bool isUnlocked;

    GameManager gameManager;
    AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (gateTrigger == null)
            gateTrigger = GetComponent<BoxCollider2D>();

        if (gateTrigger != null)
        {
            gateTrigger.isTrigger = true;
            gateTrigger.enabled = false;
        }
    }

    private void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
        {
            gameManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;
            StartCoroutine(InitialEnemyCheckNextFrame());
        }
    }

    IEnumerator InitialEnemyCheckNextFrame()
    {
        yield return null;

        if (gameManager != null && gameManager.EnemiesAlive <= 0)
            HandleAllEnemiesDefeated();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
    }

    private void HandleAllEnemiesDefeated()
    {
        if (isUnlocked)
            return;

        isUnlocked = true;
        StartCoroutine(OpenGateRoutine());
    }

    private IEnumerator OpenGateRoutine()
    {
        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        if (openGateSfx != null && audioSource != null)
            audioSource.PlayOneShot(openGateSfx);

        if (HasMidTiles())
        {
            ReplaceTilesClosedToMid();
            gateTilemap.RefreshAllTiles();

            if (midStepDelay > 0f)
                yield return new WaitForSeconds(midStepDelay);
        }

        ReplaceTilesToOpen();
        gateTilemap.RefreshAllTiles();

        PositionTriggerOnEntryTileFound();

        if (gateTrigger != null)
            gateTrigger.enabled = true;
    }

    private bool HasMidTiles()
    {
        return mid00 != null || mid10 != null || mid20 != null ||
               mid01 != null || mid11 != null || mid21 != null;
    }

    private void ReplaceTilesClosedToMid()
    {
        var bounds = gateTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var current = gateTilemap.GetTile(cell);

                if (current == null)
                    continue;

                var replacement = GetClosedToMid(current);
                if (replacement != null)
                    gateTilemap.SetTile(cell, replacement);
            }
    }

    private TileBase GetClosedToMid(TileBase current)
    {
        if (current == null)
            return null;

        if (closed00 != null && current == closed00) return mid00 ?? open00;
        if (closed10 != null && current == closed10) return mid10 ?? open10;
        if (closed20 != null && current == closed20) return mid20 ?? open20;

        if (closed01 != null && current == closed01) return mid01 ?? open01;
        if (closed11 != null && current == closed11) return mid11 ?? open11;
        if (closed21 != null && current == closed21) return mid21 ?? open21;

        return null;
    }

    private void ReplaceTilesToOpen()
    {
        var bounds = gateTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var current = gateTilemap.GetTile(cell);

                if (current == null)
                    continue;

                var replacement = GetToOpen(current);
                if (replacement != null)
                    gateTilemap.SetTile(cell, replacement);
            }
    }

    private TileBase GetToOpen(TileBase current)
    {
        if (current == null)
            return null;

        if (closed00 != null && current == closed00) return open00;
        if (closed10 != null && current == closed10) return open10;
        if (closed20 != null && current == closed20) return open20;

        if (closed01 != null && current == closed01) return open01;
        if (closed11 != null && current == closed11) return open11;
        if (closed21 != null && current == closed21) return open21;

        if (mid00 != null && current == mid00) return open00;
        if (mid10 != null && current == mid10) return open10;
        if (mid20 != null && current == mid20) return open20;

        if (mid01 != null && current == mid01) return open01;
        if (mid11 != null && current == mid11) return open11;
        if (mid21 != null && current == mid21) return open21;

        return null;
    }

    private void PositionTriggerOnEntryTileFound()
    {
        if (gateTilemap == null || gateTrigger == null || entryOpenTile == null)
            return;

        var bounds = gateTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                var current = gateTilemap.GetTile(cell);

                if (current != entryOpenTile)
                    continue;

                var worldCenter = gateTilemap.GetCellCenterWorld(cell);

                var dx = worldCenter.x - transform.position.x;
                var dy = worldCenter.y - transform.position.y;

                gateTrigger.offset = new Vector2(dx, dy);
                gateTrigger.size = triggerSize;

                return;
            }

        gateTrigger.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isUnlocked || isActivated)
            return;

        if (!other || !other.CompareTag("Player"))
            return;

        var triggerMovement = other.GetComponent<MovementController>();
        if (triggerMovement == null || triggerMovement.isDead || triggerMovement.IsEndingStage)
            return;

        isActivated = true;

        Vector2 portalCenter = new(
            Mathf.Round(transform.position.x),
            Mathf.Round(transform.position.y)
        );

        var players = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null) continue;
            if (!m.CompareTag("Player")) continue;
            if (!m.gameObject.activeInHierarchy) continue;
            if (m.isDead || m.IsEndingStage) continue;

            var bombController = m.GetComponent<BombController>();
            PlayerPersistentStats.SaveFrom(m, bombController);

            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            bool snapThisOne = m == triggerMovement;
            m.PlayEndStageSequence(portalCenter, snapThisOne);
        }

        var audio = other.GetComponent<AudioSource>();
        if (audio != null && enterSfx != null)
            audio.PlayOneShot(enterSfx);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (endStageMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f, false);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(3f);

        if (gameManager != null)
            gameManager.EndStage();
    }
}
