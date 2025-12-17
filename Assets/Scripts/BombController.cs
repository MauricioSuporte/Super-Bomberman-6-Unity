using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MovementController))]
public class BombController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode inputKey = KeyCode.Space;
    public bool useAIInput = false;
    private bool bombRequested;

    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public GameObject pierceBombPrefab;
    public float bombFuseTime = 3f;
    public int bombAmout = 1;
    private static readonly WaitForSeconds chainExplosionWait = new(0.1f);
    private readonly HashSet<int> scheduledChainBombs = new();

    private int bombsRemaining = 0;
    public int BombsRemaining => bombsRemaining;

    [Header("Explosion Settings")]
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 2;

    [Header("Stage & Tiles")]
    public Tilemap groundTiles;
    public Tilemap stageBoundsTiles;

    [Header("Destructible")]
    public Tilemap destructibleTiles;
    public Destructible destructiblePrefab;

    [Header("Items")]
    public LayerMask itemLayerMask;

    [Header("SFX")]
    public AudioClip placeBombSfx;
    public AudioSource playerAudioSource;

    private static AudioSource currentExplosionAudio;

    private void OnEnable()
    {
        bombAmout = Mathf.Min(bombAmout, PlayerPersistentStats.MaxBombAmount);
        bombsRemaining = bombAmout;
    }

    private void Update()
    {
        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        var movement = GetComponent<MovementController>();
        if (movement != null && movement.InputLocked)
            return;

        if (GamePauseController.IsPaused)
            return;

        if (bombsRemaining <= 0)
            return;

        if (!useAIInput)
        {
            if (Input.GetKeyDown(inputKey))
                PlaceBomb();
        }
        else
        {
            if (bombRequested)
            {
                PlaceBomb();
                bombRequested = false;
            }
        }
    }

    private void Awake()
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        if (groundTiles == null)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm != null && tm.name.ToLowerInvariant().Contains("ground"))
                {
                    groundTiles = tm;
                    break;
                }
            }

            if (groundTiles == null && tilemaps.Length > 0)
                groundTiles = tilemaps[0];
        }

        if (stageBoundsTiles == null)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm != null && tm.name.ToLowerInvariant().Contains("indestruct"))
                {
                    stageBoundsTiles = tm;
                    break;
                }
            }

            if (stageBoundsTiles == null)
                stageBoundsTiles = groundTiles;
        }
    }

    public void AddBomb()
    {
        if (bombAmout >= PlayerPersistentStats.MaxBombAmount)
            return;

        bombAmout++;
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    private bool IsPierceEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(PierceBombAbility.AbilityId);

        return false;
    }

    public void ExplodeBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        var bombComp = bomb.GetComponent<Bomb>();

        Vector2 logicalPos = bombComp != null
            ? bombComp.GetLogicalPosition()
            : (Vector2)bomb.transform.position;

        bomb.transform.position = logicalPos;

        if (bomb.TryGetComponent<Rigidbody2D>(out var rb))
            rb.position = logicalPos;

        if (bombComp != null)
        {
            if (bombComp.HasExploded)
                return;

            bombComp.MarkAsExploded();
        }

        HideBombVisuals(bomb);

        if (bomb.TryGetComponent<AudioSource>(out var explosionAudio))
        {
            if (currentExplosionAudio != null && currentExplosionAudio.isPlaying)
                currentExplosionAudio.Stop();

            currentExplosionAudio = explosionAudio;
            currentExplosionAudio.Play();
        }

        Vector2 position = logicalPos;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        bool pierce = bombComp != null && bombComp.IsPierceBomb;

        Explosion centerExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        centerExplosion.Play(Explosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, position);

        Explode(position, Vector2.up, explosionRadius, pierce);
        Explode(position, Vector2.down, explosionRadius, pierce);
        Explode(position, Vector2.left, explosionRadius, pierce);
        Explode(position, Vector2.right, explosionRadius, pierce);

        float destroyDelay = 0.1f;
        if (explosionAudio != null && explosionAudio.clip != null)
            destroyDelay = explosionAudio.clip.length;

        Destroy(bomb, destroyDelay);

        bombsRemaining++;
    }

    private void PlaceBomb()
    {
        Vector2 position = transform.position;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        if (TileHasBomb(position))
            return;

        if (playerAudioSource != null && placeBombSfx != null)
            playerAudioSource.PlayOneShot(placeBombSfx);

        bool pierceEnabled = IsPierceEnabled();
        GameObject prefabToUse = (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab : bombPrefab;

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPierceBomb = pierceEnabled;

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.Initialize(this);

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        StartCoroutine(BombFuse(bomb));
    }

    private bool TileHasBomb(Vector2 position)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int mask = 1 << bombLayer;

        return Physics2D.OverlapBox(position, Vector2.one * 0.4f, 0f, mask) != null;
    }

    private IEnumerator BombFuse(GameObject bomb)
    {
        yield return new WaitForSeconds(bombFuseTime);
        ExplodeBomb(bomb);
    }

    private bool HasIndestructibleAt(Vector2 worldPos)
    {
        if (stageBoundsTiles == null)
            return false;

        Vector3Int cell = stageBoundsTiles.WorldToCell(worldPos);
        return stageBoundsTiles.GetTile(cell) != null;
    }

    private bool HasDestructibleAt(Vector2 worldPos)
    {
        if (destructibleTiles == null)
            return false;

        Vector3Int cell = destructibleTiles.WorldToCell(worldPos);
        return destructibleTiles.GetTile(cell) != null;
    }

    private void Explode(Vector2 origin, Vector2 direction, int length, bool pierce)
    {
        if (length <= 0)
            return;

        List<Vector2> positionsToSpawn = new(length);

        Vector2 position = origin;

        for (int i = 0; i < length; i++)
        {
            position += direction;

            if (HasIndestructibleAt(position))
                break;

            var itemHit = Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, itemLayerMask);
            if (itemHit != null)
            {
                if (itemHit.TryGetComponent<ItemPickup>(out var item))
                    item.DestroyWithAnimation();

                positionsToSpawn.Add(position);

                if (!pierce)
                    break;

                continue;
            }

            bool hitDestructible = HasDestructibleAt(position);
            if (hitDestructible)
            {
                ClearDestructible(position);

                if (pierce)
                {
                    positionsToSpawn.Add(position);
                    continue;
                }

                break;
            }

            int bombLayer = LayerMask.NameToLayer("Bomb");
            int bombMask = 1 << bombLayer;
            var bombHit = Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, bombMask);

            if (bombHit != null)
            {
                if (i < length - 1)
                {
                    GameObject otherBombGo = bombHit.attachedRigidbody != null
                        ? bombHit.attachedRigidbody.gameObject
                        : bombHit.gameObject;

                    if (otherBombGo != null && otherBombGo != gameObject)
                        ScheduleChainExplosion(otherBombGo);
                }

                break;
            }

            positionsToSpawn.Add(position);
        }

        for (int i = 0; i < positionsToSpawn.Count; i++)
        {
            Vector2 p = positionsToSpawn[i];

            bool reachedMaxRange = positionsToSpawn.Count == length;
            bool isLastSpawned = i == positionsToSpawn.Count - 1;

            Explosion.ExplosionPart part =
                (isLastSpawned && reachedMaxRange)
                    ? Explosion.ExplosionPart.End
                    : Explosion.ExplosionPart.Middle;

            Explosion explosion = Instantiate(explosionPrefab, p, Quaternion.identity);
            explosion.Play(part, direction, 0f, explosionDuration, origin);
        }
    }

    private void ClearDestructible(Vector2 position)
    {
        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile == null)
            return;

        var gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
            gameManager.OnDestructibleDestroyed(cell);

        Transform parent = destructibleTiles != null ? destructibleTiles.transform : null;

        if (parent != null)
            Instantiate(destructiblePrefab, position, Quaternion.identity, parent);
        else
            Instantiate(destructiblePrefab, position, Quaternion.identity);

        if (gameManager != null)
        {
            GameObject spawnPrefab = gameManager.GetSpawnForDestroyedBlock();
            if (spawnPrefab != null)
            {
                if (parent != null)
                    Instantiate(spawnPrefab, position, Quaternion.identity, parent);
                else
                    Instantiate(spawnPrefab, position, Quaternion.identity);
            }
        }

        destructibleTiles.SetTile(cell, null);
    }

    private void HideBombVisuals(GameObject bomb)
    {
        if (bomb.TryGetComponent<SpriteRenderer>(out var sprite))
            sprite.enabled = false;

        if (bomb.TryGetComponent<Collider2D>(out var collider))
            collider.enabled = false;

        if (bomb.TryGetComponent<AnimatedSpriteRenderer>(out var anim))
            anim.enabled = false;

        if (bomb.TryGetComponent<Bomb>(out var bombScript))
            bombScript.enabled = false;
    }

    public void RequestBombFromAI()
    {
        bombRequested = true;
    }

    private void ScheduleChainExplosion(GameObject bomb)
    {
        if (bomb == null)
            return;

        int id = bomb.GetInstanceID();
        if (!scheduledChainBombs.Add(id))
            return;

        StartCoroutine(ChainExplodeRoutine(bomb, id));
    }

    private IEnumerator ChainExplodeRoutine(GameObject bomb, int id)
    {
        yield return chainExplosionWait;

        scheduledChainBombs.Remove(id);

        if (bomb == null)
            yield break;

        if (bomb.TryGetComponent<Bomb>(out var bombComp) && bombComp.HasExploded)
            yield break;

        ExplodeBomb(bomb);
    }
}
