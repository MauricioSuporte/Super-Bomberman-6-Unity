using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(ClownMaskMovement))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ClownMaskBoss : MonoBehaviour, IKillable
{
    private static readonly WaitForSeconds _waitForSeconds1 = new(1f);

    [Header("References")]
    public CharacterHealth characterHealth;
    public ClownMaskMovement clownMovement;
    public BossEndStageSequence bossEndSequence;

    [Header("Stage Intro (Clown Mask)")]
    public StageIntroClownMaskBoss stageIntroClownMaskBoss;

    [Header("Renderers")]
    SpriteRenderer[] bossSpriteRenderers;
    public AnimatedSpriteRenderer introRenderer;
    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer specialRenderer;
    public AnimatedSpriteRenderer hurtRenderer;
    public AnimatedSpriteRenderer deathRenderer;

    [Header("Intro")]
    public float introDuration = 2f;

    [Header("Boss Intro Setup")]
    public Vector2 bossIntroPosition = new(-1f, 2f);
    public float delayAfterStageIntroToShowPlayer = 1f;

    [Tooltip("Fallback position if playerIntroPositions is empty.")]
    public Vector2 playerIntroPosition = new(-3f, -6f);

    [Tooltip("Player intro positions (P1..P4). If empty, uses playerIntroPosition for all.")]
    public Vector2[] playerIntroPositions = new Vector2[]
    {
        new(-3f, -6f),
        new( 1f, -6f),
        new(-5f, -6f),
        new( 3f, -6f),
    };

    public float introWaitAfterPlayerShown = 1f;

    [Header("Spotlight")]
    public float introDarknessAlpha = 0.9f;
    public float introWaitBeforeSpotlight = 1f;
    public float introSpotlightRadiusTiles = 4f;
    public float introSpotlightSoftnessTiles = 1f;

    [Header("Special Animation")]
    public float minSpecialInterval = 5f;
    public float maxSpecialInterval = 12f;

    [Header("After Hit")]
    public float extraHitStopDuringAttack = 0.3f;
    public float delayBeforeAttack = 0.1f;

    [Header("Hurt State")]
    public float hurtAnimationDuration = 0.5f;
    public float hurtWalkDuration = 3f;

    [Header("Attack")]
    public ClownStarProjectile clownStarProjectile;
    public float starSpeed = 6f;
    public float starLifeTime = 3f;
    public float starSpawnRadius = 0.5f;

    [Header("Death")]
    public float deathDuration = 5f;

    [Header("Death Explosions")]
    public ClownExplosionVfx explosionPrefab;
    public float explosionSpawnInterval = 0.05f;
    public float explosionSpawnRadius = 1.2f;
    public float explosionMinScale = 0.7f;
    public float explosionMaxScale = 1.2f;

    [Header("Death Explosion SFX")]
    public AudioClip deathExplosionSfx;
    public float deathExplosionSfxInterval = 0.12f;
    [Range(0f, 1f)] public float deathExplosionSfxVolume = 1f;
    public Vector2 deathExplosionPitchRange = new(0.95f, 1.05f);
    public bool deathSfxUseTempAudioObject = true;
    public float deathSfxSpatialBlend = 0f;

    [Header("Touch Damage")]
    public int touchDamage = 1;
    public float touchDamageCooldown = 0.35f;

    float nextTouchDamageTime;

    public static bool BossIntroRunning { get; private set; }

    bool isDead;
    bool introFinished;
    bool inDamageSequence;
    bool isInHurtWalk;
    bool bossSpawned;

    Coroutine introRoutine;
    Coroutine specialRoutine;
    Coroutine damageSequenceRoutine;
    Coroutine deathRoutine;

    AudioSource audioSource;
    float nextDeathSfxTime;

    MovementController[] players = new MovementController[0];
    BombController[] playerBombs = new BombController[0];

    Collider2D bossCollider;
    Rigidbody2D bossRb;

    void Awake()
    {
        if (!characterHealth)
            characterHealth = GetComponent<CharacterHealth>();

        if (!clownMovement)
            clownMovement = GetComponent<ClownMaskMovement>();

        if (!bossEndSequence)
            bossEndSequence = FindFirstObjectByType<BossEndStageSequence>();

        if (!stageIntroClownMaskBoss)
            stageIntroClownMaskBoss = FindFirstObjectByType<StageIntroClownMaskBoss>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.playOnAwake = false;

        bossCollider = GetComponent<Collider2D>();
        bossRb = GetComponent<Rigidbody2D>();

        bossSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        EnsurePlayersRefs();

        if (characterHealth != null)
        {
            characterHealth.Damaged += OnHealthDamaged;
            characterHealth.Died += OnHealthDied;
        }

        ResetBossState();
        MoveBossToIntroPosition();
    }

    void OnDestroy()
    {
        if (characterHealth != null)
        {
            characterHealth.Damaged -= OnHealthDamaged;
            characterHealth.Died -= OnHealthDied;
        }
    }

    void OnEnable()
    {
        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(IntroSequence());
    }

    void OnDisable()
    {
        ResetBossState();
    }

    void EnsurePlayersRefs()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (ids != null && ids.Length > 0)
        {
            var tempPlayers = new System.Collections.Generic.List<MovementController>(ids.Length);
            var tempBombs = new System.Collections.Generic.List<BombController>(ids.Length);

            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                MovementController m = null;
                if (!id.TryGetComponent<MovementController>(out m))
                    m = id.GetComponentInChildren<MovementController>(true);

                if (m == null) continue;
                if (!m.CompareTag("Player")) continue;

                BombController b = null;
                if (!id.TryGetComponent<BombController>(out b))
                    b = id.GetComponentInChildren<BombController>(true);

                tempPlayers.Add(m);
                tempBombs.Add(b);
            }

            players = tempPlayers.ToArray();
            playerBombs = tempBombs.ToArray();
            return;
        }

        var go = GameObject.FindGameObjectsWithTag("Player");
        if (go != null && go.Length > 0)
        {
            var tempPlayers = new System.Collections.Generic.List<MovementController>(go.Length);
            var tempBombs = new System.Collections.Generic.List<BombController>(go.Length);

            for (int i = 0; i < go.Length; i++)
            {
                if (go[i] == null) continue;

                var m = go[i].GetComponent<MovementController>();
                if (m == null) continue;

                tempPlayers.Add(m);
                tempBombs.Add(go[i].GetComponent<BombController>());
            }

            players = tempPlayers.ToArray();
            playerBombs = tempBombs.ToArray();
            return;
        }

        players = new MovementController[0];
        playerBombs = new BombController[0];
    }

    void ResetBossState()
    {
        if (introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; }
        if (specialRoutine != null) { StopCoroutine(specialRoutine); specialRoutine = null; }
        if (damageSequenceRoutine != null) { StopCoroutine(damageSequenceRoutine); damageSequenceRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }

        isDead = false;
        introFinished = false;
        inDamageSequence = false;
        isInHurtWalk = false;
        bossSpawned = false;

        if (clownMovement != null)
            clownMovement.enabled = false;

        if (characterHealth != null)
            characterHealth.enabled = false;

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (bossRb != null)
        {
            bossRb.simulated = false;
            bossRb.linearVelocity = Vector2.zero;
        }

        SetAllRenderers(false);
        SetBossSpriteRenderersVisible(false);
    }

    IEnumerator IntroSequence()
    {
        BossIntroRunning = true;

        ResetBossState();
        MoveBossToIntroPosition();

        EnsurePlayersRefs();
        LockPlayers(true);
        SetPlayersHidden(true);
        ForceEggQueuesVisibleAll(false);
        ForceLouieRiderVisualRenderersAll(false);

        while (StageIntroTransition.Instance != null &&
               StageIntroTransition.Instance.IntroRunning)
            yield return null;

        EnsurePlayersRefs();
        LockPlayers(true);
        SetPlayersHidden(true);
        ForceEggQueuesVisibleAll(false);
        ForceLouieRiderVisualRenderersAll(false);

        float waitAfterStageIntro = Mathf.Max(0f, delayAfterStageIntroToShowPlayer);
        if (waitAfterStageIntro > 0f)
            yield return new WaitForSeconds(waitAfterStageIntro);

        EnsurePlayersRefs();
        SpawnPlayersForBossIntro();

        EnsureMountedLouiesExistsIfNeeded();

        SetPlayersHidden(false);
        ForceEggQueuesVisibleAll(true);
        ForceLouieRiderVisualRenderersAll(true, upOnly: true);

        LockPlayers(true);
        ShowPlayersIdleUpOnly();

        if (stageIntroClownMaskBoss != null)
            stageIntroClownMaskBoss.StartDefaultMusicOnce();

        float waitAfterShowPlayer = Mathf.Max(0f, introWaitAfterPlayerShown);
        if (waitAfterShowPlayer > 0f)
            yield return new WaitForSeconds(waitAfterShowPlayer);

        float waitBeforeSpot = Mathf.Max(0f, introWaitBeforeSpotlight);
        if (waitBeforeSpot > 0f)
            yield return new WaitForSeconds(waitBeforeSpot);

        if (stageIntroClownMaskBoss != null)
            yield return stageIntroClownMaskBoss.FadeToFullDarknessAndWait(
                introDarknessAlpha,
                stageIntroClownMaskBoss.spotlightFadeInDuration
            );

        yield return _waitForSeconds1;

        SpawnBossForIntroOnly();

        float colliderRadius = 1f;

        if (bossCollider is CircleCollider2D circle)
            colliderRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        float spotlightRadius = colliderRadius * 0.3f;

        stageIntroClownMaskBoss.SetSpotlightWorld(
            bossIntroPosition,
            spotlightRadius,
            introDarknessAlpha,
            introSpotlightSoftnessTiles
        );

        EnableOnly(introRenderer);

        float duration = introDuration;
        if (introRenderer != null && duration <= 0f)
        {
            if (introRenderer.useSequenceDuration && introRenderer.sequenceDuration > 0f)
                duration = introRenderer.sequenceDuration;
            else if (introRenderer.animationSprite != null && introRenderer.animationSprite.Length > 0)
                duration = introRenderer.animationTime * introRenderer.animationSprite.Length;
            else
                duration = 2f;
        }

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (stageIntroClownMaskBoss != null)
            yield return stageIntroClownMaskBoss.FadeSpotlightAlphaAndWait(0f, 0.5f);

        if (stageIntroClownMaskBoss != null)
            stageIntroClownMaskBoss.DisableSpotlight();

        EnableOnly(idleRenderer);
        EnableBossCombat();

        RestoreLouiesAfterBossIntro();
        RestorePlayersAfterBossIntro();

        LockPlayers(false);

        introFinished = true;
        BossIntroRunning = false;

        specialRoutine = StartCoroutine(SpecialLoop());
    }

    void SpawnBossForIntroOnly()
    {
        bossSpawned = true;

        MoveBossToIntroPosition();

        if (bossRb != null)
        {
            bossRb.simulated = false;
            bossRb.linearVelocity = Vector2.zero;
        }

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (characterHealth != null)
            characterHealth.enabled = false;

        if (clownMovement != null)
            clownMovement.enabled = false;
    }

    void EnableBossCombat()
    {
        if (isDead)
            return;

        if (bossRb != null)
        {
            bossRb.simulated = true;
            bossRb.linearVelocity = Vector2.zero;
        }

        if (bossCollider != null)
            bossCollider.enabled = true;

        if (characterHealth != null)
            characterHealth.enabled = true;

        if (clownMovement != null)
            clownMovement.enabled = true;

        bossSpawned = true;
    }

    void MoveBossToIntroPosition()
    {
        if (bossRb != null)
        {
            bossRb.position = bossIntroPosition;
            bossRb.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = new Vector3(bossIntroPosition.x, bossIntroPosition.y, transform.position.z);
        }
    }

    Vector2 GetIntroPosForPlayerIndex(int index)
    {
        if (playerIntroPositions != null && playerIntroPositions.Length >= 4 && index >= 0 && index < playerIntroPositions.Length)
            return playerIntroPositions[index];

        return playerIntroPosition + new Vector2(index * 2f, 0f);
    }

    void SpawnPlayersForBossIntro()
    {
        EnsurePlayersRefs();
        if (players == null || players.Length == 0)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;

            Vector2 pos = GetIntroPosForPlayerIndex(GetPlayerIdFromGO(p.gameObject) - 1);

            if (p.Rigidbody != null)
            {
                p.Rigidbody.simulated = true;
                p.Rigidbody.position = pos;
                p.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                p.transform.position = new Vector3(pos.x, pos.y, p.transform.position.z);
            }

            p.ForceIdleUp();

            var b = GetBombForPlayer(p);
            if (b != null)
                b.enabled = false;
        }
    }

    BombController GetBombForPlayer(MovementController p)
    {
        if (p == null) return null;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == p)
            {
                if (playerBombs != null && i < playerBombs.Length)
                    return playerBombs[i];
            }
        }

        var b = p.GetComponent<BombController>();
        if (b != null) return b;

        var pid = p.GetComponentInParent<PlayerIdentity>(true);
        if (pid != null)
            return pid.GetComponentInChildren<BombController>(true);

        return null;
    }

    void RestorePlayersAfterBossIntro()
    {
        EnsurePlayersRefs();
        if (players == null) return;

        SetPlayersHidden(false);

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;

            ForceLouieRiderVisualRenderersOne(p, true);

            if (p.TryGetComponent<BombPunchAbility>(out var punch))
                punch.ForceResetPunchSprites();

            var keep = p.IsMountedOnLouie
                ? (p.mountedSpriteUp != null ? p.mountedSpriteUp : p.spriteRendererUp)
                : p.spriteRendererUp;

            DisableAllPlayerSpritesExcept(p, keep, keepLouieVisual: true);

            if (keep != null)
            {
                keep.enabled = true;
                keep.idle = true;
                keep.loop = true;
                keep.RefreshFrame();

                if (keep.TryGetComponent<SpriteRenderer>(out var sr))
                    sr.enabled = true;
            }

            p.ForceIdleUpConsideringMount();
            p.EnableExclusiveFromState();

            var b = GetBombForPlayer(p);
            if (b != null)
                b.enabled = true;

            p.SetExplosionInvulnerable(false);

            p.SetInputLocked(false, true);
            p.enabled = true;

            if (p.TryGetComponent<Collider2D>(out var col) && col != null)
                col.enabled = true;

            if (p.Rigidbody != null)
                p.Rigidbody.simulated = true;
        }
    }

    void DisableAllPlayerSpritesExcept(MovementController p, AnimatedSpriteRenderer keep, bool keepLouieVisual)
    {
        if (p == null) return;

        var anims = p.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;

            if (keepLouieVisual && a.GetComponentInParent<LouieRidingVisual>(true) != null)
                continue;

            a.enabled = (a == keep);
        }

        var srs = p.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;

            if (keepLouieVisual && sr.GetComponentInParent<LouieRidingVisual>(true) != null)
                continue;

            sr.enabled = false;
        }

        if (keep != null)
        {
            if (keep.TryGetComponent<SpriteRenderer>(out var keepSr))
                keepSr.enabled = true;
        }
    }

    void SetPlayersHidden(bool hidden)
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int p = 0; p < players.Length; p++)
        {
            var pl = players[p];
            if (pl == null) continue;

            var anims = pl.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int i = 0; i < anims.Length; i++)
                if (anims[i] != null)
                    anims[i].enabled = !hidden;

            var srs = pl.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
                if (srs[i] != null)
                    srs[i].enabled = !hidden;
        }
    }

    void ShowPlayersIdleUpOnly()
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;

            p.ForceIdleUpConsideringMount();

            DisableAllPlayerSpritesExcept(
                p,
                p.IsMountedOnLouie
                    ? (p.mountedSpriteUp != null ? p.mountedSpriteUp : p.spriteRendererUp)
                    : p.spriteRendererUp,
                keepLouieVisual: true
            );
        }
    }

    void LockPlayers(bool locked)
    {
        EnsurePlayersRefs();

        if (players == null)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (!locked)
            {
                if (!p.enabled)
                    p.enabled = true;

                if (p.TryGetComponent<Collider2D>(out var col) && col != null)
                    col.enabled = true;

                if (p.Rigidbody != null)
                    p.Rigidbody.simulated = true;
            }

            p.SetInputLocked(locked, true);
            p.SetExplosionInvulnerable(locked);

            if (locked)
                p.ApplyDirectionFromVector(Vector2.zero);

            if (p.Rigidbody != null && locked)
                p.Rigidbody.linearVelocity = Vector2.zero;

            var b = GetBombForPlayer(p);
            if (b != null)
                b.enabled = !locked;
        }
    }

    void EnsureMountedLouiesExistsIfNeeded()
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
            EnsureMountedLouieExistsIfNeededOne(players[i]);
    }

    void EnsureMountedLouieExistsIfNeededOne(MovementController p)
    {
        if (p == null) return;
        if (!p.IsMountedOnLouie) return;

        if (!p.TryGetComponent<PlayerLouieCompanion>(out var comp) || comp == null)
            return;

        if (comp.HasMountedLouie())
            return;

        int playerId = GetPlayerIdFromGO(p.gameObject);
        var state = PlayerPersistentStats.Get(playerId);

        switch (state.MountedLouie)
        {
            case MountedLouieType.Blue: comp.RestoreMountedBlueLouie(); break;
            case MountedLouieType.Black: comp.RestoreMountedBlackLouie(); break;
            case MountedLouieType.Purple: comp.RestoreMountedPurpleLouie(); break;
            case MountedLouieType.Green: comp.RestoreMountedGreenLouie(); break;
            case MountedLouieType.Yellow: comp.RestoreMountedYellowLouie(); break;
            case MountedLouieType.Pink: comp.RestoreMountedPinkLouie(); break;
            case MountedLouieType.Red: comp.RestoreMountedRedLouie(); break;
        }
    }

    void ForceLouieRiderVisualRenderersAll(bool visible, bool upOnly = false)
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
            ForceLouieRiderVisualRenderersOne(players[i], visible, upOnly);
    }

    void ForceLouieRiderVisualRenderersOne(MovementController p, bool visible, bool upOnly = false)
    {
        if (p == null)
            return;

        var rider = p.GetComponentInChildren<LouieRidingVisual>(true);
        if (rider == null)
            return;

        rider.gameObject.SetActive(visible);

        var anims = rider.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        var srs = rider.GetComponentsInChildren<SpriteRenderer>(true);

        if (!visible)
        {
            for (int i = 0; i < anims.Length; i++)
                if (anims[i] != null)
                    anims[i].enabled = false;

            for (int i = 0; i < srs.Length; i++)
                if (srs[i] != null)
                    srs[i].enabled = false;

            return;
        }

        AnimatedSpriteRenderer keep = null;

        if (upOnly)
            keep = FindLouieRendererByChildName(rider.transform, "Up");

        if (keep == null)
            keep = FindFirstEnabledLouieRenderer(anims);

        if (keep == null)
            keep = FindLouieRendererByChildName(rider.transform, "Up");

        if (keep == null && anims.Length > 0)
            keep = anims[0];

        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;
            a.enabled = (a == keep);
        }

        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;
            sr.enabled = false;
        }

        if (keep != null)
        {
            if (upOnly)
            {
                keep.idle = true;
                keep.loop = false;
                keep.RefreshFrame();
            }

            keep.RefreshFrame();

            if (keep.TryGetComponent<SpriteRenderer>(out var keepSr))
                keepSr.enabled = true;
        }
    }

    AnimatedSpriteRenderer FindLouieRendererByChildName(Transform riderRoot, string childName)
    {
        if (riderRoot == null)
            return null;

        Transform t = riderRoot.Find(childName);

        if (t == null && childName == "Right")
            t = riderRoot.Find("Rigth");

        if (t == null)
            return null;

        return t.GetComponent<AnimatedSpriteRenderer>();
    }

    AnimatedSpriteRenderer FindFirstEnabledLouieRenderer(AnimatedSpriteRenderer[] anims)
    {
        if (anims == null)
            return null;

        for (int i = 0; i < anims.Length; i++)
            if (anims[i] != null && anims[i].enabled)
                return anims[i];

        return null;
    }

    void RestoreLouiesAfterBossIntro()
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null || !p.IsMountedOnLouie)
                continue;

            var rider = p.GetComponentInChildren<LouieRidingVisual>(true);
            if (rider == null)
                continue;

            rider.ForceIdleUp();

            var anims = rider.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int a = 0; a < anims.Length; a++)
            {
                var r = anims[a];
                if (r == null) continue;

                r.idle = false;
                r.loop = true;
            }
        }
    }

    IEnumerator SpecialLoop()
    {
        while (!isDead)
        {
            float wait = Random.Range(minSpecialInterval, maxSpecialInterval);
            yield return new WaitForSeconds(wait);

            if (isDead || !introFinished || inDamageSequence || !bossSpawned)
                continue;

            yield return PlaySpecialOnce();
        }
    }

    IEnumerator PlaySpecialOnce()
    {
        if (specialRenderer == null)
            yield break;

        AnimatedSpriteRenderer previous = GetCurrentRenderer();

        EnableOnly(specialRenderer);

        float duration;
        if (specialRenderer.useSequenceDuration && specialRenderer.sequenceDuration > 0f)
            duration = specialRenderer.sequenceDuration;
        else if (specialRenderer.animationSprite != null && specialRenderer.animationSprite.Length > 0)
            duration = specialRenderer.animationTime * specialRenderer.animationSprite.Length;
        else
            duration = 1f;

        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            if (previous != null && previous != specialRenderer)
                EnableOnly(previous);
            else
                EnableOnly(idleRenderer);
        }
    }

    void OnHealthDamaged(int amount)
    {
        if (isDead || !bossSpawned || !introFinished)
            return;

        bool wasInHurtWalk = isInHurtWalk;
        bool wasInHurtAnim = hurtRenderer != null && hurtRenderer.enabled && !hurtRenderer.idle;
        bool useHurtIdleDuringInvuln = wasInHurtWalk || wasInHurtAnim;

        if (damageSequenceRoutine != null)
            StopCoroutine(damageSequenceRoutine);

        damageSequenceRoutine = StartCoroutine(DamageSequenceRoutine(useHurtIdleDuringInvuln));
    }

    IEnumerator DamageSequenceRoutine(bool useHurtIdleDuringInvuln)
    {
        inDamageSequence = true;
        isInHurtWalk = false;

        if (clownMovement != null)
            clownMovement.OnHit();

        SpawnStarBurst();

        float invulDuration = characterHealth != null ? characterHealth.hitInvulnerableDuration : 0f;

        if (useHurtIdleDuringInvuln)
        {
            if (hurtRenderer != null)
            {
                EnableOnly(hurtRenderer);
                if (hurtRenderer.animationSprite != null && hurtRenderer.animationSprite.Length > 0)
                {
                    int first = 0;
                    hurtRenderer.CurrentFrame = first;
                    hurtRenderer.idleSprite = hurtRenderer.animationSprite[first];
                }
                hurtRenderer.idle = true;
                hurtRenderer.loop = false;
            }
        }
        else
        {
            if (idleRenderer != null)
            {
                EnableOnly(idleRenderer);
                idleRenderer.idle = true;
                idleRenderer.loop = true;
            }
        }

        if (invulDuration > 0f)
            yield return new WaitForSeconds(invulDuration);

        if (isDead)
        {
            inDamageSequence = false;
            isInHurtWalk = false;
            damageSequenceRoutine = null;
            yield break;
        }

        if (hurtRenderer != null && hurtRenderer.animationSprite != null && hurtRenderer.animationSprite.Length > 0)
        {
            EnableOnly(hurtRenderer);
            hurtRenderer.loop = false;
            hurtRenderer.idle = false;

            int frames = hurtRenderer.animationSprite.Length;
            if (hurtAnimationDuration > 0f && frames > 0)
                hurtRenderer.animationTime = hurtAnimationDuration / frames;

            float hurtDuration = hurtAnimationDuration > 0f ? hurtAnimationDuration : 0.5f;
            yield return new WaitForSeconds(hurtDuration);

            if (isDead)
            {
                inDamageSequence = false;
                isInHurtWalk = false;
                damageSequenceRoutine = null;
                yield break;
            }

            int last = hurtRenderer.animationSprite.Length - 1;
            hurtRenderer.CurrentFrame = last;
            hurtRenderer.idleSprite = hurtRenderer.animationSprite[last];
            hurtRenderer.idle = true;
            hurtRenderer.loop = false;

            isInHurtWalk = true;

            float walkDuration = hurtWalkDuration > 0f ? hurtWalkDuration : 3f;
            float elapsed = 0f;

            while (elapsed < walkDuration && !isDead)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        isInHurtWalk = false;
        inDamageSequence = false;
        damageSequenceRoutine = null;

        if (!isDead && idleRenderer != null)
            EnableOnly(idleRenderer);
    }

    void SpawnStarBurst()
    {
        if (clownStarProjectile == null)
            return;

        Vector2 origin = transform.position;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right,
            new Vector2(1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 dir = dirs[i];
            Vector2 spawnPos = origin + dir * starSpawnRadius;

            ClownStarProjectile proj = Instantiate(clownStarProjectile, spawnPos, Quaternion.identity);
            if (proj != null)
                proj.Initialize(dir, starSpeed, starLifeTime);
        }
    }

    void OnHealthDied()
    {
        Kill();
    }

    public void Kill()
    {
        if (isDead)
            return;

        isDead = true;

        if (specialRoutine != null)
        {
            StopCoroutine(specialRoutine);
            specialRoutine = null;
        }

        if (damageSequenceRoutine != null)
        {
            StopCoroutine(damageSequenceRoutine);
            damageSequenceRoutine = null;
        }

        if (clownMovement != null)
            clownMovement.enabled = false;

        if (characterHealth != null)
            characterHealth.enabled = false;

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);

        nextDeathSfxTime = 0f;
        deathRoutine = StartCoroutine(DeathSequence());
    }

    void GrantPostBossDefeatImmunityToAlivePlayers()
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (p.isDead) continue;

            p.SetExplosionInvulnerable(true);

            p.SendMessage("SetInvulnerable", true, SendMessageOptions.DontRequireReceiver);
        }
    }

    IEnumerator DeathSequence()
    {
        AnimatedSpriteRenderer target = deathRenderer != null ? deathRenderer : idleRenderer;
        EnableOnly(target);

        float deathVisualDuration = GetRendererDuration(target, Mathf.Max(0.01f, deathDuration));

        float blinkInterval = characterHealth != null && characterHealth.hitBlinkInterval > 0f
            ? characterHealth.hitBlinkInterval
            : 0.1f;

        Coroutine explosions = null;
        if (explosionPrefab != null)
            explosions = StartCoroutine(SpawnDeathExplosions(deathVisualDuration));

        float elapsed = 0f;
        bool visible = true;

        SpriteRenderer sr = null;
        if (target != null)
            sr = target.GetComponent<SpriteRenderer>();

        while (elapsed < deathVisualDuration)
        {
            visible = !visible;
            if (sr != null)
                sr.enabled = visible;

            TryPlayDeathExplosionSfx();

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        if (explosions != null)
            StopCoroutine(explosions);

        HideBossVisuals();

        yield return _waitForSeconds1;

        GrantPostBossDefeatImmunityToAlivePlayers();

        if (bossEndSequence != null)
            bossEndSequence.StartBossDefeatedSequence();

        Destroy(gameObject);
    }

    IEnumerator SpawnDeathExplosions(float duration)
    {
        float elapsed = 0f;
        float d = Mathf.Max(0.01f, duration);

        while (elapsed < d)
        {
            SpawnOneExplosion();
            yield return new WaitForSeconds(explosionSpawnInterval);
            elapsed += explosionSpawnInterval;
        }
    }

    void SpawnOneExplosion()
    {
        if (explosionPrefab == null)
            return;

        Vector2 origin = transform.position;
        Vector2 offset = Random.insideUnitCircle * explosionSpawnRadius;
        Vector3 pos = new(origin.x + offset.x, origin.y + offset.y, transform.position.z);

        ClownExplosionVfx fx = Instantiate(explosionPrefab, pos, Quaternion.identity);

        float scale = Random.Range(explosionMinScale, explosionMaxScale);
        fx.transform.localScale = new Vector3(scale, scale, 1f);

        TryPlayDeathExplosionSfx();
    }

    void TryPlayDeathExplosionSfx()
    {
        if (deathExplosionSfx == null)
            return;

        float interval = deathExplosionSfxInterval <= 0f ? 0.01f : deathExplosionSfxInterval;
        if (Time.time < nextDeathSfxTime)
            return;

        nextDeathSfxTime = Time.time + interval;

        float pitch = Random.Range(deathExplosionPitchRange.x, deathExplosionPitchRange.y);
        Vector3 pos = transform.position;

        if (deathSfxUseTempAudioObject)
        {
            GameObject go = new("ClownDeathSfx");
            go.transform.position = pos;

            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = deathSfxSpatialBlend;
            s.volume = deathExplosionSfxVolume;
            s.pitch = pitch;

            s.PlayOneShot(deathExplosionSfx, 1f);

            float clipDuration = deathExplosionSfx.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            Destroy(go, clipDuration + 0.1f);
            return;
        }

        if (audioSource == null)
            return;

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(deathExplosionSfx, deathExplosionSfxVolume);
        audioSource.pitch = 1f;
    }

    void SetAllRenderers(bool value)
    {
        if (introRenderer) introRenderer.enabled = value;
        if (idleRenderer) idleRenderer.enabled = value;
        if (specialRenderer) specialRenderer.enabled = value;
        if (hurtRenderer) hurtRenderer.enabled = value;
        if (deathRenderer) deathRenderer.enabled = value;
    }

    void EnableOnly(AnimatedSpriteRenderer target)
    {
        if (introRenderer != null) introRenderer.enabled = (target == introRenderer);
        if (idleRenderer != null) idleRenderer.enabled = (target == idleRenderer);
        if (specialRenderer != null) specialRenderer.enabled = (target == specialRenderer);
        if (hurtRenderer != null) hurtRenderer.enabled = (target == hurtRenderer);
        if (deathRenderer != null) deathRenderer.enabled = (target == deathRenderer);

        SetBossSpriteRenderersVisible(false);

        if (target != null)
        {
            target.RefreshFrame();

            if (target.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = true;
        }
    }

    AnimatedSpriteRenderer GetCurrentRenderer()
    {
        if (introRenderer != null && introRenderer.enabled) return introRenderer;
        if (idleRenderer != null && idleRenderer.enabled) return idleRenderer;
        if (specialRenderer != null && specialRenderer.enabled) return specialRenderer;
        if (hurtRenderer != null && hurtRenderer.enabled) return hurtRenderer;
        if (deathRenderer != null && deathRenderer.enabled) return deathRenderer;
        return null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyExplosionDamage(other);
        TryApplyTouchDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryApplyTouchDamage(other);
    }

    void TryApplyExplosionDamage(Collider2D other)
    {
        if (isDead || !bossSpawned || !introFinished)
            return;

        if (other.gameObject.layer != LayerMask.NameToLayer("Explosion"))
            return;

        if (characterHealth == null || !characterHealth.enabled)
            return;

        if (characterHealth.IsInvulnerable)
            return;

        characterHealth.TakeDamage(1);
    }

    void TryApplyTouchDamage(Collider2D other)
    {
        if (isDead || !bossSpawned || !introFinished)
            return;

        if (!other.CompareTag("Player") && other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        if (Time.time < nextTouchDamageTime)
            return;

        if (!other.TryGetComponent<MovementController>(out var movement) || movement == null)
            return;

        if (movement.isDead || movement.IsEndingStage)
            return;

        if (movement.TryGetComponent<CharacterHealth>(out var playerHealth) && playerHealth != null)
        {
            if (playerHealth.IsInvulnerable)
                return;
        }

        if (movement.IsMountedOnLouie)
        {
            if (movement.TryGetComponent<PlayerLouieCompanion>(out var companion) && companion != null)
            {
                companion.OnMountedLouieHit(touchDamage);
                nextTouchDamageTime = Time.time + touchDamageCooldown;
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(touchDamage);
                nextTouchDamageTime = Time.time + touchDamageCooldown;
                return;
            }

            movement.Kill();
            nextTouchDamageTime = Time.time + touchDamageCooldown;
            return;
        }

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(touchDamage);
            nextTouchDamageTime = Time.time + touchDamageCooldown;
            return;
        }

        movement.Kill();
        nextTouchDamageTime = Time.time + touchDamageCooldown;
    }

    void SetBossSpriteRenderersVisible(bool visible)
    {
        bossSpriteRenderers ??= GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < bossSpriteRenderers.Length; i++)
        {
            var sr = bossSpriteRenderers[i];
            if (sr != null)
                sr.enabled = visible;
        }
    }

    int GetPlayerIdFromGO(GameObject go)
    {
        if (go == null) return 1;

        if (go.TryGetComponent<PlayerIdentity>(out var id) && id != null)
            return Mathf.Clamp(id.playerId, 1, 4);

        var parentId = go.GetComponentInParent<PlayerIdentity>(true);
        if (parentId != null)
            return Mathf.Clamp(parentId.playerId, 1, 4);

        return 1;
    }

    float GetRendererDuration(AnimatedSpriteRenderer r, float fallback)
    {
        if (r == null) return fallback;

        if (r.useSequenceDuration && r.sequenceDuration > 0f)
            return r.sequenceDuration;

        if (r.animationSprite != null && r.animationSprite.Length > 0)
            return Mathf.Max(0.01f, r.animationTime * r.animationSprite.Length);

        return fallback;
    }

    void HideBossVisuals()
    {
        bossSpriteRenderers ??= GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < bossSpriteRenderers.Length; i++)
        {
            var sr = bossSpriteRenderers[i];
            if (sr != null) sr.enabled = false;
        }

        if (introRenderer != null) introRenderer.enabled = false;
        if (idleRenderer != null) idleRenderer.enabled = false;
        if (specialRenderer != null) specialRenderer.enabled = false;
        if (hurtRenderer != null) hurtRenderer.enabled = false;
        if (deathRenderer != null) deathRenderer.enabled = false;
    }

    void ForceEggQueuesVisibleAll(bool visible)
    {
        EnsurePlayersRefs();
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;

            var q = p.GetComponentInChildren<LouieEggQueue>(true);
            if (q != null)
                q.ForceVisible(visible);
        }
    }
}
