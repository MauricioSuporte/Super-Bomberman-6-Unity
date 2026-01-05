using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(CharacterHealth))]
public class BombController : MonoBehaviour
{
    [Header("Player Id (only used if tagged Player)")]
    [SerializeField, Range(1, 4)] private int playerId = 1;
    public int PlayerId => playerId;

    [Header("Input")]
    public bool useAIInput = false;
    private bool bombRequested;

    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public GameObject pierceBombPrefab;
    public GameObject controlBombPrefab;
    public float bombFuseTime = 2f;
    public int bombAmout = 1;
    private readonly HashSet<int> scheduledChainBombs = new();

    private int bombsRemaining = 0;
    public int BombsRemaining => bombsRemaining;

    [Header("Control Bomb")]
    private readonly List<GameObject> plantedBombs = new();

    [Header("Explosion Settings")]
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 2;

    [Header("Stage & Tiles")]
    public Tilemap groundTiles;
    public Tilemap stageBoundsTiles;

    [Header("Destructible (resolved from GameManager)")]
    public Tilemap destructibleTiles;
    public Destructible destructiblePrefab;

    [Header("Items")]
    public LayerMask itemLayerMask;

    [Header("SFX")]
    public AudioClip placeBombSfx;
    public AudioSource playerAudioSource;

    [Header("Explosion SFX By Radius (1..9, >=10 = last)")]
    public AudioClip[] explosionSfxByRadius = new AudioClip[10];
    [Range(0f, 1f)] public float explosionSfxVolume = 1f;

    private static AudioSource currentExplosionAudio;

    private GameManager _gm;
    private AudioSource _localAudio;

    public void SetPlayerId(int id)
    {
        playerId = Mathf.Clamp(id, 1, 4);
    }

    private void Awake()
    {
        _localAudio = GetComponent<AudioSource>();
        if (playerAudioSource == null)
            playerAudioSource = _localAudio;

        ResolveTilemaps();
    }

    private void Start()
    {
        ResolveTilemaps();
    }

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
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
            return;

        if (GamePauseController.IsPaused)
            return;

        if (!useAIInput && CompareTag("Player"))
        {
            var input = PlayerInputManager.Instance;
            if (input == null)
                return;

            if (bombsRemaining > 0 && input.GetDown(playerId, PlayerAction.ActionA))
                PlaceBomb();

            if (IsControlEnabled() && input.GetDown(playerId, PlayerAction.ActionB))
                TryExplodeOldestControlledBomb();
        }
        else
        {
            if (bombsRemaining <= 0)
                return;

            if (bombRequested)
            {
                PlaceBomb();
                bombRequested = false;
            }
        }
    }

    private void ResolveTilemaps()
    {
        if (_gm == null)
            _gm = FindFirstObjectByType<GameManager>();

        if (_gm != null)
        {
            if (groundTiles == null) groundTiles = _gm.groundTilemap;
            if (destructibleTiles == null) destructibleTiles = _gm.destructibleTilemap;

            if (stageBoundsTiles == null)
                stageBoundsTiles = _gm.indestructibleTilemap != null ? _gm.indestructibleTilemap : _gm.groundTilemap;

            destructiblePrefab = _gm.destructiblePrefab;
        }

        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        if (groundTiles == null)
            groundTiles = FindTilemapByNameContains(tilemaps, "ground") ?? (tilemaps.Length > 0 ? tilemaps[0] : null);

        if (stageBoundsTiles == null)
            stageBoundsTiles = FindTilemapByNameContains(tilemaps, "indestruct") ?? groundTiles;

        if (destructibleTiles == null)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm != null && tm.CompareTag("Destructibles"))
                {
                    destructibleTiles = tm;
                    break;
                }
            }

            if (destructibleTiles == null)
                destructibleTiles = FindTilemapByNameContains(tilemaps, "destruct");
        }
    }

    private Tilemap FindTilemapByNameContains(Tilemap[] tilemaps, string containsLower)
    {
        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null) continue;

            string n = tm.name.ToLowerInvariant();
            if (n.Contains(containsLower))
                return tm;
        }
        return null;
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

        Bomb bombComp = bomb.GetComponent<Bomb>();
        BombController realOwner = bombComp != null ? bombComp.Owner : null;

        if (realOwner != null && realOwner != this)
        {
            realOwner.ExplodeBomb(bomb);
            return;
        }

        UnregisterBomb(bomb);

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

        int effectiveRadius = IsFullFireEnabled()
            ? PlayerPersistentStats.MaxExplosionRadius
            : explosionRadius;

        HideBombVisuals(bomb);

        if (bomb.TryGetComponent<AudioSource>(out var explosionAudio))
        {
            if (currentExplosionAudio != null && currentExplosionAudio.isPlaying)
                currentExplosionAudio.Stop();

            currentExplosionAudio = explosionAudio;
            PlayExplosionSfx(currentExplosionAudio, effectiveRadius);
        }

        Vector2 position = logicalPos;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        bool pierce = bombComp != null && bombComp.IsPierceBomb;

        Explosion centerExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        centerExplosion.Play(Explosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, position);

        Explode(position, Vector2.up, effectiveRadius, pierce);
        Explode(position, Vector2.down, effectiveRadius, pierce);
        Explode(position, Vector2.left, effectiveRadius, pierce);
        Explode(position, Vector2.right, effectiveRadius, pierce);

        float destroyDelay = 0.1f;

        if (explosionSfxByRadius != null && explosionSfxByRadius.Length > 0)
        {
            int sfxIndex = Mathf.Clamp(effectiveRadius - 1, 0, explosionSfxByRadius.Length - 1);
            AudioClip sfx = explosionSfxByRadius[sfxIndex];

            if (sfx != null)
                destroyDelay = sfx.length;
            else if (explosionAudio != null && explosionAudio.clip != null)
                destroyDelay = explosionAudio.clip.length;
        }
        else if (explosionAudio != null && explosionAudio.clip != null)
        {
            destroyDelay = explosionAudio.clip.length;
        }

        Destroy(bomb, destroyDelay);

        bombsRemaining++;
    }

    private void PlayExplosionSfx(AudioSource source, int radius)
    {
        if (source == null || explosionSfxByRadius == null || explosionSfxByRadius.Length == 0)
            return;

        int index = Mathf.Clamp(radius - 1, 0, explosionSfxByRadius.Length - 1);
        AudioClip clip = explosionSfxByRadius[index];

        if (clip != null)
            source.PlayOneShot(clip, explosionSfxVolume);
    }

    private void PlayPlaceBombSfx()
    {
        if (placeBombSfx == null)
            return;

        AudioSource src = playerAudioSource != null ? playerAudioSource : _localAudio;
        if (src == null)
            return;

        src.PlayOneShot(placeBombSfx);
    }

    private void PlaceBomb()
    {
        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
            return;

        Vector2 position = transform.position;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        bool explosionAlreadyHere = HasActiveExplosionAt(position);

        if (TileHasBomb(position))
        {
            if (!explosionAlreadyHere)
                return;

            ExplodeAnyBombAt(position);
        }

        if (HasDestructibleAt(position))
            return;

        bool controlEnabled = IsControlEnabled();
        bool pierceEnabled = !controlEnabled && IsPierceEnabled();

        GameObject prefabToUse =
            controlEnabled && controlBombPrefab != null ? controlBombPrefab :
            (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab :
            bombPrefab;

        if (prefabToUse == null)
            return;

        PlayPlaceBombSfx();

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPierceBomb = pierceEnabled;
        bombComponent.IsControlBomb = controlEnabled;

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.Initialize(this);
        bombComponent.SetFuseSeconds(bombFuseTime);

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        if (controlEnabled)
            RegisterBomb(bomb);

        if (explosionAlreadyHere)
        {
            ExplodeBomb(bomb);
            return;
        }

        if (!controlEnabled)
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

            bool hitDestructibleTile = HasDestructibleAt(position);
            bool hitDestroyingDestructible = HasDestroyingDestructibleAt(position);

            if (hitDestructibleTile || hitDestroyingDestructible)
            {
                if (hitDestructibleTile)
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
                GameObject otherBombGo = bombHit.attachedRigidbody != null
                    ? bombHit.attachedRigidbody.gameObject
                    : bombHit.gameObject;

                if (otherBombGo != null && otherBombGo != gameObject)
                    ExplodeBombImmediate(otherBombGo);

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

    private void ExplodeBombImmediate(GameObject bomb)
    {
        if (bomb == null)
            return;

        int id = bomb.GetInstanceID();
        if (!scheduledChainBombs.Add(id))
            return;

        scheduledChainBombs.Remove(id);

        if (bomb.TryGetComponent<Bomb>(out var bombComp) && bombComp.HasExploded)
            return;

        ExplodeBomb(bomb);
    }

    private void ClearDestructible(Vector2 position)
    {
        if (destructibleTiles == null)
            return;

        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile == null)
            return;

        if (_gm == null)
            _gm = FindFirstObjectByType<GameManager>();

        if (_gm != null)
            _gm.OnDestructibleDestroyed(cell);

        Transform parent = destructibleTiles != null ? destructibleTiles.transform : null;

        if (destructiblePrefab != null)
        {
            if (parent != null)
                Instantiate(destructiblePrefab, position, Quaternion.identity, parent);
            else
                Instantiate(destructiblePrefab, position, Quaternion.identity);
        }

        if (_gm != null)
        {
            GameObject spawnPrefab = _gm.GetSpawnForDestroyedBlock();
            if (spawnPrefab != null)
            {
                float delay = GetDestructibleDestroyTime();
                StartCoroutine(SpawnHiddenObjectAfterDelay(spawnPrefab, position, parent, delay));
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

    private bool HasDestroyingDestructibleAt(Vector2 worldPos)
    {
        int mask = LayerMask.GetMask("Stage");

        Collider2D hit = Physics2D.OverlapBox(
            worldPos,
            Vector2.one * 0.6f,
            0f,
            mask
        );

        if (hit == null)
            return false;

        return hit.GetComponent<Destructible>() != null;
    }

    private IEnumerator SpawnHiddenObjectAfterDelay(GameObject prefab, Vector2 position, Transform parent, float delay)
    {
        if (prefab == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (parent != null)
            Instantiate(prefab, position, Quaternion.identity, parent);
        else
            Instantiate(prefab, position, Quaternion.identity);
    }

    private float GetDestructibleDestroyTime()
    {
        if (destructiblePrefab != null)
            return Mathf.Max(0f, destructiblePrefab.destructionTime);

        return 0.5f;
    }

    private bool IsControlEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(ControlBombAbility.AbilityId);

        return false;
    }

    private void RegisterBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        plantedBombs.Add(bomb);
    }

    private void UnregisterBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        plantedBombs.Remove(bomb);
    }

    private void CleanupNullBombs()
    {
        for (int i = plantedBombs.Count - 1; i >= 0; i--)
        {
            if (plantedBombs[i] == null)
                plantedBombs.RemoveAt(i);
        }
    }

    private bool TryExplodeOldestControlledBomb()
    {
        CleanupNullBombs();

        for (int i = 0; i < plantedBombs.Count; i++)
        {
            var bomb = plantedBombs[i];
            if (bomb == null)
                continue;

            if (!bomb.TryGetComponent<Bomb>(out var bombComp) || bombComp == null || !bombComp.IsControlBomb)
                continue;

            plantedBombs.RemoveAt(i);
            ExplodeBomb(bomb);
            return true;
        }

        return false;
    }

    public void ClearPlantedBombsOnStageEnd(bool explodeInstead = false)
    {
        CleanupNullBombs();

        for (int i = 0; i < plantedBombs.Count; i++)
        {
            var b = plantedBombs[i];
            if (b == null)
                continue;

            if (explodeInstead)
                ExplodeBomb(b);
            else
                Destroy(b);
        }

        plantedBombs.Clear();
    }

    private bool IsFullFireEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(FullFireAbility.AbilityId);

        return false;
    }

    public static void ExplodeAllControlBombsInStage()
    {
        var bombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        if (bombs == null || bombs.Length == 0)
            return;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
            if (b == null)
                continue;

            if (!b.IsControlBomb)
                continue;

            if (b.HasExploded)
                continue;

            var owner = b.Owner;
            if (owner != null)
                owner.ExplodeBomb(b.gameObject);
            else
                Object.Destroy(b.gameObject);
        }
    }

    public bool TryPlaceBombAt(Vector2 worldPos)
    {
        if (ClownMaskBoss.BossIntroRunning)
            return false;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return false;

        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return false;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
            return false;

        if (GamePauseController.IsPaused)
            return false;

        if (bombsRemaining <= 0)
            return false;

        Vector2 position = worldPos;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        bool explosionAlreadyHere = HasActiveExplosionAt(position);

        if (TileHasBomb(position))
        {
            if (!explosionAlreadyHere)
                return false;

            ExplodeAnyBombAt(position);
        }

        if (HasDestructibleAt(position))
            return false;

        bool controlEnabled = IsControlEnabled();
        bool pierceEnabled = !controlEnabled && IsPierceEnabled();

        GameObject prefabToUse =
            controlEnabled && controlBombPrefab != null ? controlBombPrefab :
            (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab :
            bombPrefab;

        if (prefabToUse == null)
            return false;

        PlayPlaceBombSfx();

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPierceBomb = pierceEnabled;
        bombComponent.IsControlBomb = controlEnabled;

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.Initialize(this);
        bombComponent.SetFuseSeconds(bombFuseTime);

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        if (controlEnabled)
            RegisterBomb(bomb);

        if (explosionAlreadyHere)
        {
            ExplodeBomb(bomb);
            return true;
        }

        if (!controlEnabled)
            StartCoroutine(BombFuse(bomb));

        return true;
    }

    public bool TryPlaceBombAtIgnoringInputLock(Vector2 worldPos)
    {
        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.isDead || movement.IsEndingStage))
            return false;

        bool previousLock = false;
        if (movement != null)
        {
            previousLock = movement.InputLocked;
            movement.SetInputLocked(false, false);
        }

        bool result = TryPlaceBombAt(worldPos);

        if (movement != null)
            movement.SetInputLocked(previousLock, false);

        return result;
    }

    private bool HasActiveExplosionAt(Vector2 position)
    {
        int explosionLayer = LayerMask.NameToLayer("Explosion");
        int mask = explosionLayerMask.value;

        if (explosionLayer >= 0)
            mask |= (1 << explosionLayer);

        return Physics2D.OverlapBox(
            position,
            Vector2.one * 0.6f,
            0f,
            mask
        ) != null;
    }

    private void ExplodeAnyBombAt(Vector2 position)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        Collider2D[] hits = Physics2D.OverlapBoxAll(position, Vector2.one * 0.6f, 0f, bombMask);
        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            GameObject bombGo = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (bombGo == null)
                continue;

            ExplodeBomb(bombGo);
        }
    }

    public bool WillCellBeHitSoon(Vector2 cellCenter, float withinSeconds)
    {
        cellCenter.x = Mathf.Round(cellCenter.x);
        cellCenter.y = Mathf.Round(cellCenter.y);

        var bombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        if (bombs == null || bombs.Length == 0)
            return false;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
            if (b == null || b.HasExploded)
                continue;

            Vector2 pos = b.GetLogicalPosition();
            pos.x = Mathf.Round(pos.x);
            pos.y = Mathf.Round(pos.y);

            if (b.RemainingFuseSeconds > withinSeconds)
                continue;

            int radius = GetEffectiveRadiusForOwner(b.Owner);
            bool pierce = b.IsPierceBomb;

            if (IsCellInBlast(pos, cellCenter, radius, pierce))
                return true;
        }

        return false;
    }

    private int GetEffectiveRadiusForOwner(BombController owner)
    {
        if (owner == null)
            return explosionRadius;

        int baseRadius = owner.explosionRadius;
        if (owner.IsFullFireEnabled())
            return PlayerPersistentStats.MaxExplosionRadius;

        return baseRadius;
    }

    private bool IsCellInBlast(Vector2 origin, Vector2 target, int radius, bool pierce)
    {
        if (target == origin)
            return true;

        Vector2 delta = target - origin;
        if (delta.x != 0f && delta.y != 0f)
            return false;

        Vector2 dir;
        int dist;

        if (delta.x != 0f)
        {
            dir = new Vector2(Mathf.Sign(delta.x), 0f);
            dist = Mathf.Abs((int)delta.x);
        }
        else
        {
            dir = new Vector2(0f, Mathf.Sign(delta.y));
            dist = Mathf.Abs((int)delta.y);
        }

        if (dist > radius)
            return false;

        Vector2 p = origin;
        for (int i = 0; i < dist; i++)
        {
            p += dir;

            if (HasIndestructibleAt(p))
                return false;

            var itemHit = Physics2D.OverlapBox(p, Vector2.one * 0.5f, 0f, itemLayerMask);
            if (itemHit != null)
                return (p == target);

            bool hitDestructibleTile = HasDestructibleAt(p);
            bool hitDestroyingDestructible = HasDestroyingDestructibleAt(p);

            if (hitDestructibleTile || hitDestroyingDestructible)
            {
                if (p == target)
                    return true;

                return pierce;
            }

            int bombLayer = LayerMask.NameToLayer("Bomb");
            int bombMask = 1 << bombLayer;
            var bombHit = Physics2D.OverlapBox(p, Vector2.one * 0.5f, 0f, bombMask);
            if (bombHit != null)
                return (p == target);
        }

        return true;
    }
}
