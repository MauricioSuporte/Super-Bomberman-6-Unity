using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MechaBossSequence : MonoBehaviour
{
    private static readonly WaitForSeconds _waitForSeconds1 = new(1f);
    private static readonly WaitForSeconds _waitForSeconds2 = new(2f);

    public MovementController whiteMecha;
    public MovementController blackMecha;
    public MovementController goldenMecha;

    public MovementController player;

    [Header("Music")]
    public AudioClip bossCheeringMusic;

    [Header("Gate")]
    public Tilemap indestructibleTilemap;
    public Vector3Int gateCell;
    public float gateStepDelay = 0.1f;

    [Header("Stands (Crowd / Empty)")]
    public Tilemap standsTilemap;
    public TileBase[] crowdTiles;
    public TileBase[] emptyTiles;

    [Header("Item Spawner")]
    public Tilemap groundTilemap;
    public Tilemap destructibleTilemap;
    public ItemPickup[] itemPrefabs;

    public int spawnMinX = -7;
    public int spawnMaxX = 5;
    public int spawnMinY = -6;
    public int spawnMaxY = 2;

    public float minSpawnInterval = 20f;
    public float maxSpawnInterval = 40f;
    public int maxTriesPerSpawn = 50;

    MovementController[] mechas;
    GameManager gameManager;
    BombController playerBomb;

    bool initialized;
    bool sequenceStarted;
    bool finalSequenceStarted;
    bool itemLoopStarted;
    bool itemSpawnEnabled = true;

    TileBase gateCenterTile;
    TileBase gateLeftTile;
    TileBase gateRightTile;

    Vector3Int gateLeftCell;
    Vector3Int gateRightCell;

    readonly Dictionary<Vector3Int, int> changedStandCells = new();
    readonly List<Vector3Int> spawnableCells = new();

    void Awake()
    {
        mechas = new[] { whiteMecha, blackMecha, goldenMecha };
        gameManager = FindFirstObjectByType<GameManager>();

        foreach (var m in mechas)
        {
            if (m == null) continue;
            m.Died += OnMechaDied;
        }

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<MovementController>();
        }

        if (player != null)
            playerBomb = player.GetComponent<BombController>();

        if (indestructibleTilemap != null)
        {
            gateCenterTile = indestructibleTilemap.GetTile(gateCell);

            gateLeftCell = new Vector3Int(gateCell.x - 1, gateCell.y, gateCell.z);
            gateRightCell = new Vector3Int(gateCell.x + 1, gateCell.y, gateCell.z);

            gateLeftTile = indestructibleTilemap.GetTile(gateLeftCell);
            gateRightTile = indestructibleTilemap.GetTile(gateRightCell);
        }
    }

    void Start()
    {
        initialized = true;

        LockPlayer(true);

        for (int i = 0; i < mechas.Length; i++)
            if (mechas[i] != null)
                mechas[i].gameObject.SetActive(false);

        if (groundTilemap != null && itemPrefabs != null && itemPrefabs.Length > 0)
            RebuildSpawnableCells();
    }

    void OnEnable()
    {
        if (initialized && !sequenceStarted)
        {
            sequenceStarted = true;
            StartCoroutine(SpawnFirstMechaAfterStageStart());
        }
    }

    IEnumerator SpawnFirstMechaAfterStageStart()
    {
        if (StageIntroTransition.Instance != null)
            while (StageIntroTransition.Instance.IntroRunning)
                yield return null;

        while (GamePauseController.IsPaused)
            yield return null;

        LockPlayer(true);

        yield return _waitForSeconds2;

        StartMechaIntro(0);
    }

    void StartMechaIntro(int index)
    {
        if (index < 0 || index >= mechas.Length) return;
        if (mechas[index] == null) return;

        StartCoroutine(MechaIntroRoutine(mechas[index]));
    }

    IEnumerator MechaIntroRoutine(MovementController mecha)
    {
        SetItemSpawnEnabled(false);
        LockPlayer(true);

        yield return StartCoroutine(OpenGateRoutine());

        bool isGolden = mecha == goldenMecha;

        if (isGolden)
        {
            yield return _waitForSeconds2;

            if (StageIntroTransition.Instance != null)
                yield return StageIntroTransition.Instance.Flash(0.5f, 5);
        }

        mecha.SetExplosionInvulnerable(true);

        var bossAI = mecha.GetComponent<BossBomberAI>();
        var aiMove = mecha.GetComponent<AIMovementController>();

        if (bossAI != null) bossAI.enabled = false;
        if (aiMove != null) aiMove.enabled = true;

        Vector2 startPos = new(-1f, 5f);
        Vector2 midPos = new(-1f, 4f);
        Vector2 endPos = new(-1f, 0f);

        if (mecha.Rigidbody != null)
        {
            mecha.Rigidbody.simulated = true;
            mecha.Rigidbody.linearVelocity = Vector2.zero;
            mecha.Rigidbody.position = startPos;
        }
        else
        {
            mecha.transform.position = startPos;
        }

        mecha.gameObject.SetActive(true);

        if (!isGolden)
        {
            if (aiMove != null)
                aiMove.SetAIDirection(Vector2.down);

            while (true)
            {
                if (mecha == null) yield break;

                Vector2 pos = mecha.Rigidbody != null
                    ? mecha.Rigidbody.position
                    : (Vector2)mecha.transform.position;

                if (pos.y <= endPos.y + 0.05f)
                    break;

                if (aiMove != null)
                    aiMove.SetAIDirection(Vector2.down);

                yield return null;
            }

            if (mecha.Rigidbody != null)
            {
                mecha.Rigidbody.position = endPos;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                mecha.transform.position = endPos;
            }
        }
        else
        {
            if (aiMove != null)
            {
                aiMove.SetAIDirection(Vector2.zero);
                aiMove.enabled = false;
            }

            float moveSpeed = mecha.speed > 0f ? mecha.speed : 2f;

            yield return MoveMechaVertically(mecha, startPos, midPos, moveSpeed);

            yield return _waitForSeconds2;

            yield return MoveMechaVertically(mecha, midPos, endPos, moveSpeed);

            if (aiMove != null)
                aiMove.enabled = true;
        }

        if (aiMove != null)
            aiMove.SetAIDirection(Vector2.zero);

        yield return _waitForSeconds2;

        if (bossAI != null) bossAI.enabled = true;

        mecha.SetExplosionInvulnerable(false);

        yield return StartCoroutine(CloseGateRoutine());

        if (!itemLoopStarted && groundTilemap != null && itemPrefabs != null && itemPrefabs.Length > 0)
        {
            itemLoopStarted = true;
            StartCoroutine(ItemSpawnLoop());
        }

        SetItemSpawnEnabled(true);
        LockPlayer(false);
    }

    IEnumerator MoveMechaVertically(MovementController mecha, Vector2 from, Vector2 to, float speed)
    {
        if (mecha == null)
            yield break;

        mecha.ApplyDirectionFromVector(Vector2.down);

        float distance = Mathf.Abs(to.y - from.y);
        if (distance <= Mathf.Epsilon || speed <= 0f)
        {
            if (mecha.Rigidbody != null)
            {
                mecha.Rigidbody.position = to;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                mecha.transform.position = to;
            }

            mecha.ApplyDirectionFromVector(Vector2.zero);
            yield break;
        }

        float duration = distance / speed;
        float t = 0f;

        while (t < duration)
        {
            if (mecha == null) yield break;

            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            float newY = Mathf.Lerp(from.y, to.y, lerp);

            if (mecha.Rigidbody != null)
            {
                Vector2 pos = mecha.Rigidbody.position;
                pos.x = from.x;
                pos.y = newY;
                mecha.Rigidbody.position = pos;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                Vector3 pos = mecha.transform.position;
                pos.x = from.x;
                pos.y = newY;
                mecha.transform.position = pos;
            }

            yield return null;
        }

        if (mecha != null)
        {
            if (mecha.Rigidbody != null)
            {
                mecha.Rigidbody.position = to;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                mecha.transform.position = to;
            }
        }

        mecha.ApplyDirectionFromVector(Vector2.zero);
    }

    IEnumerator OpenGateRoutine()
    {
        if (indestructibleTilemap == null)
            yield break;

        indestructibleTilemap.SetTile(gateCell, null);

        if (gateStepDelay > 0f)
            yield return new WaitForSeconds(gateStepDelay);

        indestructibleTilemap.SetTile(gateLeftCell, null);
        indestructibleTilemap.SetTile(gateRightCell, null);
    }

    IEnumerator CloseGateRoutine()
    {
        if (indestructibleTilemap == null)
            yield break;

        indestructibleTilemap.SetTile(gateLeftCell, gateLeftTile);
        indestructibleTilemap.SetTile(gateRightCell, gateRightTile);

        if (gateStepDelay > 0f)
            yield return new WaitForSeconds(gateStepDelay);

        indestructibleTilemap.SetTile(gateCell, gateCenterTile);
    }

    public void SetStandsEmpty(bool empty)
    {
        ReplaceCrowdWithEmpty(empty);
    }

    void ReplaceCrowdWithEmpty(bool setEmpty)
    {
        if (standsTilemap == null) return;
        if (crowdTiles == null || emptyTiles == null) return;
        if (crowdTiles.Length != emptyTiles.Length) return;

        if (setEmpty)
        {
            changedStandCells.Clear();

            BoundsInt bounds = standsTilemap.cellBounds;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new(x, y, 0);
                    TileBase current = standsTilemap.GetTile(cell);
                    if (current == null) continue;

                    for (int i = 0; i < crowdTiles.Length; i++)
                    {
                        if (current == crowdTiles[i])
                        {
                            standsTilemap.SetTile(cell, emptyTiles[i]);
                            changedStandCells[cell] = i;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var kvp in changedStandCells)
            {
                Vector3Int cell = kvp.Key;
                int index = kvp.Value;

                if (index >= 0 && index < crowdTiles.Length)
                    standsTilemap.SetTile(cell, crowdTiles[index]);
            }

            changedStandCells.Clear();
        }
    }

    void RebuildSpawnableCells()
    {
        spawnableCells.Clear();

        if (groundTilemap == null)
            return;

        for (int x = spawnMinX; x <= spawnMaxX; x++)
        {
            for (int y = spawnMinY; y <= spawnMaxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (!groundTilemap.HasTile(cell))
                    continue;

                if (destructibleTilemap != null && destructibleTilemap.HasTile(cell))
                    continue;

                if (indestructibleTilemap != null && indestructibleTilemap.HasTile(cell))
                    continue;

                spawnableCells.Add(cell);
            }
        }
    }

    IEnumerator ItemSpawnLoop()
    {
        while (!finalSequenceStarted)
        {
            float waitTime = UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
            float elapsed = 0f;

            while (elapsed < waitTime && !finalSequenceStarted)
            {
                if (!GamePauseController.IsPaused && itemSpawnEnabled)
                    elapsed += Time.deltaTime;

                yield return null;
            }

            if (finalSequenceStarted || !itemSpawnEnabled)
                continue;

            TrySpawnItem();
        }
    }

    void TrySpawnItem()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
            return;

        if (groundTilemap == null)
            return;

        if (spawnableCells == null || spawnableCells.Count == 0)
            return;

        for (int i = 0; i < maxTriesPerSpawn; i++)
        {
            int index = UnityEngine.Random.Range(0, spawnableCells.Count);
            Vector3Int cell = spawnableCells[index];

            Vector3 worldPos = groundTilemap.GetCellCenterWorld(cell);
            Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.2f);

            if (hit != null && hit.GetComponent<ItemPickup>() != null)
                continue;

            ItemPickup prefab = itemPrefabs[UnityEngine.Random.Range(0, itemPrefabs.Length)];
            Instantiate(prefab, worldPos, Quaternion.identity);
            return;
        }
    }

    void SetItemSpawnEnabled(bool enabled)
    {
        itemSpawnEnabled = enabled;
    }

    void LockPlayer(bool locked)
    {
        if (player != null)
        {
            player.SetInputLocked(locked);
            player.SetExplosionInvulnerable(locked);
        }

        if (playerBomb != null)
            playerBomb.enabled = !locked;
    }

    void OnMechaDied(MovementController sender)
    {
        int currentIndex = Array.IndexOf(mechas, sender);
        int nextIndex = currentIndex + 1;

        if (nextIndex < mechas.Length && mechas[nextIndex] != null)
        {
            StartMechaIntro(nextIndex);
            return;
        }

        if (sender == goldenMecha && !finalSequenceStarted)
        {
            finalSequenceStarted = true;
            StartCoroutine(FinalBossDefeatedRoutine());
            return;
        }

        if (gameManager != null)
            gameManager.CheckWinState();
    }

    IEnumerator FinalBossDefeatedRoutine()
    {
        yield return _waitForSeconds1;

        if (player == null || player.isDead)
            yield break;

        player.StartCheering();

        if (GameMusicController.Instance != null && bossCheeringMusic != null)
            GameMusicController.Instance.PlayMusic(bossCheeringMusic, 1f, false);

        float cheeringDuration = 4f;
        float fadeDuration = 1f;
        float timeBeforeFade = Mathf.Max(0f, cheeringDuration - fadeDuration);

        if (timeBeforeFade > 0f)
            yield return new WaitForSeconds(timeBeforeFade);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(fadeDuration);

        if (fadeDuration > 0f)
            yield return new WaitForSeconds(fadeDuration);

        if (gameManager != null)
            gameManager.EndStage();
    }
}
