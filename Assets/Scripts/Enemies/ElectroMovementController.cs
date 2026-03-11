using UnityEngine;

public sealed class ElectroMovementController : PersecutingEnemyMovementController
{
    private enum VisualState
    {
        Walking = 0,
        Preparing = 1,
        Charging = 2,
        Unpreparing = 3
    }

    private const string LOG = "[ElectroMovementController]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] private bool enableSurgicalLogs = true;
    [SerializeField, Min(0.05f)] private float abilityLogInterval = 0.5f;

    [Header("Walk Sprites (4 dirs)")]
    [SerializeField] private AnimatedSpriteRenderer walkUp;
    [SerializeField] private AnimatedSpriteRenderer walkDown;
    [SerializeField] private AnimatedSpriteRenderer walkLeft;
    [SerializeField] private AnimatedSpriteRenderer walkRight;

    [Header("Prepare Sprites (4 dirs)")]
    [SerializeField] private AnimatedSpriteRenderer prepareUp;
    [SerializeField] private AnimatedSpriteRenderer prepareDown;
    [SerializeField] private AnimatedSpriteRenderer prepareLeft;
    [SerializeField] private AnimatedSpriteRenderer prepareRight;

    [Header("Charge Sprites (4 dirs)")]
    [SerializeField] private AnimatedSpriteRenderer chargeUp;
    [SerializeField] private AnimatedSpriteRenderer chargeDown;
    [SerializeField] private AnimatedSpriteRenderer chargeLeft;
    [SerializeField] private AnimatedSpriteRenderer chargeRight;

    [Header("Ability VFX (optional)")]
    [SerializeField] private AnimatedSpriteRenderer abilityHorizontal;
    [SerializeField] private AnimatedSpriteRenderer abilityVertical;

    [Header("Ability Timing")]
    [SerializeField, Min(0.01f)] private float prepareSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float unprepareSeconds = 0.1f;

    [Header("Ability Damage")]
    [SerializeField, Min(0.01f)] private float damageTickSeconds = 0.25f;
    [SerializeField, Range(0f, 1f)] private float damageBoxSizePercent = 0.75f;

    private VisualState visualState = VisualState.Walking;

    private bool preparing;
    private bool charging;
    private bool unpreparing;

    private float stateTimer;
    private float damageTimer;

    private Vector2 abilityDir;

    private StunReceiver stun;
    private bool wasStun;

    private CharacterHealth healthRef;

    private AnimatedSpriteRenderer abilityUpInstance;
    private AnimatedSpriteRenderer abilityDownInstance;
    private AnimatedSpriteRenderer abilityLeftInstance;
    private AnimatedSpriteRenderer abilityRightInstance;

    private float nextAbilityLogTime;

    private int lastLoggedFrameUp = -999;
    private int lastLoggedFrameDown = -999;
    private int lastLoggedFrameLeft = -999;
    private int lastLoggedFrameRight = -999;

    protected override void Awake()
    {
        base.Awake();
        DisableAllStateSprites();
        DisableAbilityPrototypes();
    }

    protected override void OnDestroy()
    {
        UnhookHealth();
        DestroyAbilityInstances();
        base.OnDestroy();
    }

    protected override void Start()
    {
        stun = GetComponent<StunReceiver>();
        wasStun = false;

        HookHealth();

        ValidateAbilityPrototype(abilityHorizontal, "AbilityHorizontal");
        ValidateAbilityPrototype(abilityVertical, "AbilityVertical");

        SnapToGrid();
        ChooseInitialDirection();
        SetVisualState(VisualState.Walking);
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        bool stunnedNow = stun != null && stun.IsStunned;

        if (stunnedNow)
        {
            wasStun = true;

            preparing = false;
            charging = false;
            unpreparing = false;

            stateTimer = 0f;
            damageTimer = 0f;

            DisableAbilityInstances();

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (wasStun)
        {
            wasStun = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                SnapToGrid();
            }

            DisableAbilityInstances();
            SetVisualState(VisualState.Walking);
            UpdateSpriteDirection(direction);
            DecideNextTile();

            return;
        }

        if (isInDamagedLoop)
        {
            DisableAbilityInstances();

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (preparing)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            stateTimer -= Time.fixedDeltaTime;
            UpdatePrepareFramesForward();

            if (stateTimer <= 0f)
            {
                preparing = false;
                charging = true;
                unpreparing = false;

                direction = abilityDir;

                SetVisualState(VisualState.Charging);
                UpdateSpriteDirection(direction);

                SnapToGrid();

                damageTimer = 0f;

                EnsureAbilityInstances();
                SyncAbilityInstancesAt4Dirs(forceEnable: true);

                ResetAbilityFrameLogs();

            }

            return;
        }

        if (unpreparing)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            stateTimer -= Time.fixedDeltaTime;
            UpdatePrepareFramesReverse();

            if (stateTimer <= 0f)
            {
                unpreparing = false;
                charging = false;
                preparing = false;

                DisableAbilityInstances();

                SetVisualState(VisualState.Walking);
                UpdateSpriteDirection(direction);
                DecideNextTile();

            }

            return;
        }

        if (charging)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (TryGetPlayerDirectionLineOfSight(out Vector2 seenDir))
            {
                if (seenDir != direction)
                {
                    direction = seenDir;
                    abilityDir = seenDir;

                    SetVisualState(VisualState.Charging);
                    UpdateSpriteDirection(direction);

                }

                EnsureAbilityInstances();
                SyncAbilityInstancesAt4Dirs(forceEnable: false);
                TryLogAbilityFrames();
            }
            else
            {
                StartUnprepare(direction);
                return;
            }

            TickAbilityDamageAll4Dirs();
            return;
        }

        if (TryGetPlayerDirectionLineOfSight(out Vector2 dirToPlayer))
        {
            StartPrepare(dirToPlayer);
            return;
        }

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (preparing || charging || unpreparing)
            return;

        if (stun != null && stun.IsStunned)
            return;

        if (isInDamagedLoop)
            return;

        base.DecideNextTile();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop || isDead)
            return;

        AnimatedSpriteRenderer chosen = PickSprite(visualState, dir);

        if (chosen == null)
            chosen = PickSprite(VisualState.Walking, dir);

        if (chosen == null)
            chosen = walkDown != null ? walkDown : activeSprite;

        if (chosen == null)
            return;

        if (activeSprite == chosen && activeSprite.enabled)
        {
            activeSprite.idle = false;
            ForceFlip(activeSprite, dir);
            return;
        }

        DisableAllStateSprites();
        activeSprite = chosen;
        activeSprite.enabled = true;
        activeSprite.idle = false;
        ForceFlip(activeSprite, dir);

        if (charging)
        {
            EnsureAbilityInstances();
            SyncAbilityInstancesAt4Dirs(forceEnable: false);
        }
    }

    protected override void Die()
    {
        preparing = false;
        charging = false;
        unpreparing = false;

        stateTimer = 0f;
        damageTimer = 0f;

        DisableAbilityInstances();
        DisableAllStateSprites();

        base.Die();
    }

    private void StartPrepare(Vector2 dir)
    {
        preparing = true;
        charging = false;
        unpreparing = false;

        abilityDir = dir;
        direction = dir;

        DisableAbilityInstances();

        stateTimer = prepareSeconds;

        SetVisualState(VisualState.Preparing);
        UpdateSpriteDirection(direction);

        SnapToGrid();

    }

    private void StartUnprepare(Vector2 dir)
    {
        preparing = false;
        charging = false;
        unpreparing = true;

        abilityDir = dir;
        direction = dir;

        DisableAbilityInstances();

        stateTimer = unprepareSeconds;

        SetVisualState(VisualState.Unpreparing);
        UpdateSpriteDirection(direction);

        SnapToGrid();

    }

    private void SetVisualState(VisualState state)
    {
        visualState = state;

        if (state == VisualState.Preparing)
        {
            var p = PickSprite(VisualState.Preparing, direction);
            if (p != null)
            {
                p.loop = false;
                p.idle = false;
                p.CurrentFrame = 0;
                p.RefreshFrame();
            }
        }
        else if (state == VisualState.Unpreparing)
        {
            var p = PickSprite(VisualState.Preparing, direction);
            if (p != null)
            {
                p.loop = false;
                p.idle = false;

                int last = GetLastFrameIndex(p);
                p.CurrentFrame = Mathf.Max(0, last);
                p.RefreshFrame();
            }
        }
    }

    private AnimatedSpriteRenderer PickSprite(VisualState state, Vector2 dir)
    {
        if (dir == Vector2.up)
            return state == VisualState.Preparing || state == VisualState.Unpreparing ? prepareUp :
                   state == VisualState.Charging ? chargeUp : walkUp;

        if (dir == Vector2.down)
            return state == VisualState.Preparing || state == VisualState.Unpreparing ? prepareDown :
                   state == VisualState.Charging ? chargeDown : walkDown;

        if (dir == Vector2.left)
            return state == VisualState.Preparing || state == VisualState.Unpreparing ? prepareLeft :
                   state == VisualState.Charging ? chargeLeft : walkLeft;

        if (dir == Vector2.right)
            return state == VisualState.Preparing || state == VisualState.Unpreparing ? prepareRight :
                   state == VisualState.Charging ? chargeRight : walkRight;

        return null;
    }

    private void DisableAllStateSprites()
    {
        if (walkUp != null) walkUp.enabled = false;
        if (walkDown != null) walkDown.enabled = false;
        if (walkLeft != null) walkLeft.enabled = false;
        if (walkRight != null) walkRight.enabled = false;

        if (prepareUp != null) prepareUp.enabled = false;
        if (prepareDown != null) prepareDown.enabled = false;
        if (prepareLeft != null) prepareLeft.enabled = false;
        if (prepareRight != null) prepareRight.enabled = false;

        if (chargeUp != null) chargeUp.enabled = false;
        if (chargeDown != null) chargeDown.enabled = false;
        if (chargeLeft != null) chargeLeft.enabled = false;
        if (chargeRight != null) chargeRight.enabled = false;
    }

    private void ForceFlip(AnimatedSpriteRenderer r, Vector2 dir)
    {
        if (r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    private bool TryGetPlayerDirectionLineOfSight(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int mask = obstacleMask | playerLayerMask;

        for (int i = 0; i < dirs.Length; i++)
        {
            var dir = dirs[i];

            Vector2 origin = rb.position + dir * (tileSize * 0.5f);

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, visionDistance, mask);

            if (hit.collider == null)
                continue;

            if (((1 << hit.collider.gameObject.layer) & playerLayerMask) != 0)
            {
                dirToPlayer = dir;
                return true;
            }
        }

        return false;
    }

    private Vector2 GetAbilityTileCenterWorld(Vector2 dir)
    {
        if (rb == null)
            return (Vector2)transform.position + dir * tileSize;

        return rb.position + dir * tileSize;
    }

    private void TickAbilityDamageAll4Dirs()
    {
        damageTimer -= Time.fixedDeltaTime;
        if (damageTimer > 0f)
            return;

        damageTimer = Mathf.Max(0.01f, damageTickSeconds);

        float p = Mathf.Clamp(damageBoxSizePercent, 0.1f, 1f);
        Vector2 size = Vector2.one * (tileSize * p);

        TryDamageAtDir(Vector2.up, size);
        TryDamageAtDir(Vector2.down, size);
        TryDamageAtDir(Vector2.left, size);
        TryDamageAtDir(Vector2.right, size);
    }

    private void TryDamageAtDir(Vector2 dir, Vector2 size)
    {
        Vector2 tileCenter = GetAbilityTileCenterWorld(dir);

        Collider2D hit = Physics2D.OverlapBox(tileCenter, size, 0f, playerLayerMask);
        if (hit == null)
            return;

        var playerHealth = hit.GetComponent<CharacterHealth>();
        if (playerHealth != null)
            playerHealth.TakeDamage(1);
    }

    private void EnsureAbilityInstances()
    {
        if (abilityHorizontal == null && abilityVertical == null)
            return;

        if (abilityLeftInstance == null && abilityHorizontal != null)
            abilityLeftInstance = CreateAbilityInstance(abilityHorizontal, "Left");

        if (abilityRightInstance == null && abilityHorizontal != null)
            abilityRightInstance = CreateAbilityInstance(abilityHorizontal, "Right");

        if (abilityUpInstance == null && abilityVertical != null)
            abilityUpInstance = CreateAbilityInstance(abilityVertical, "Up");

        if (abilityDownInstance == null && abilityVertical != null)
            abilityDownInstance = CreateAbilityInstance(abilityVertical, "Down");
    }

    private AnimatedSpriteRenderer CreateAbilityInstance(AnimatedSpriteRenderer prototype, string tag)
    {
        if (prototype == null)
            return null;

        var go = Instantiate(prototype.gameObject, null);
        go.name = prototype.gameObject.name + "_" + tag + "_Inst";
        go.SetActive(true);

        var r = go.GetComponent<AnimatedSpriteRenderer>();
        if (r != null)
        {
            r.enabled = false;
            r.idle = false;
            r.loop = true;
            r.SetExternalBaseLocalPosition(Vector3.zero);

        }

        return r;
    }

    private void SyncAbilityInstancesAt4Dirs(bool forceEnable)
    {
        Vector2 basePos = rb != null ? rb.position : (Vector2)transform.position;

        SyncAbilityInstance(abilityUpInstance, Vector2.up, basePos, forceEnable);
        SyncAbilityInstance(abilityDownInstance, Vector2.down, basePos, forceEnable);
        SyncAbilityInstance(abilityLeftInstance, Vector2.left, basePos, forceEnable);
        SyncAbilityInstance(abilityRightInstance, Vector2.right, basePos, forceEnable);
    }

    private void SyncAbilityInstance(AnimatedSpriteRenderer inst, Vector2 dir, Vector2 basePos, bool forceEnable)
    {
        if (inst == null)
            return;

        Vector2 w2 = basePos + dir * tileSize;
        Vector3 w3 = new Vector3(w2.x, w2.y, inst.transform.position.z);

        bool wasEnabled = inst.enabled;

        inst.SetExternalBaseLocalPosition(Vector3.zero);
        inst.transform.position = w3;

        if (!wasEnabled || forceEnable)
        {
            inst.enabled = true;
            inst.idle = false;
            inst.loop = true;

        }

        if (inst.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
        {
            sr.flipX = false;
            sr.flipY = false;

            if (dir == Vector2.right)
                sr.flipX = true;

            if (dir == Vector2.down)
                sr.flipY = true;
        }
    }

    private void DisableAbilityPrototypes()
    {
        if (abilityHorizontal != null)
            abilityHorizontal.enabled = false;

        if (abilityVertical != null)
            abilityVertical.enabled = false;
    }

    private void DisableAbilityInstances()
    {
        DisableAbilityInstance(abilityUpInstance, "Up");
        DisableAbilityInstance(abilityDownInstance, "Down");
        DisableAbilityInstance(abilityLeftInstance, "Left");
        DisableAbilityInstance(abilityRightInstance, "Right");
    }

    private void DisableAbilityInstance(AnimatedSpriteRenderer inst, string tag)
    {
        if (inst == null)
            return;

        if (inst.enabled)

        inst.enabled = false;
        inst.ClearExternalBase();
        ApplyAbilityFlip(inst, false, false);
    }

    private void DestroyAbilityInstances()
    {
        if (abilityUpInstance != null) Destroy(abilityUpInstance.gameObject);
        if (abilityDownInstance != null) Destroy(abilityDownInstance.gameObject);
        if (abilityLeftInstance != null) Destroy(abilityLeftInstance.gameObject);
        if (abilityRightInstance != null) Destroy(abilityRightInstance.gameObject);

        abilityUpInstance = null;
        abilityDownInstance = null;
        abilityLeftInstance = null;
        abilityRightInstance = null;
    }

    private void ApplyAbilityFlip(AnimatedSpriteRenderer r, bool flipX, bool flipY)
    {
        if (r == null)
            return;

        if (!r.TryGetComponent<SpriteRenderer>(out var sr) || sr == null)
            return;

        sr.flipX = flipX;
        sr.flipY = flipY;
    }

    private void UpdatePrepareFramesForward()
    {
        AnimatedSpriteRenderer p = PickSprite(VisualState.Preparing, direction);
        if (p == null)
            return;

        int last = GetLastFrameIndex(p);
        if (last <= 0)
            return;

        float elapsed = Mathf.Clamp(prepareSeconds - stateTimer, 0f, prepareSeconds);
        float t01 = prepareSeconds <= 0.0001f ? 1f : (elapsed / prepareSeconds);

        int frame = Mathf.Clamp(Mathf.RoundToInt(t01 * last), 0, last);
        p.CurrentFrame = frame;
        p.idle = false;
        p.RefreshFrame();
    }

    private void UpdatePrepareFramesReverse()
    {
        AnimatedSpriteRenderer p = PickSprite(VisualState.Preparing, direction);
        if (p == null)
            return;

        int last = GetLastFrameIndex(p);
        if (last <= 0)
            return;

        float elapsed = Mathf.Clamp(unprepareSeconds - stateTimer, 0f, unprepareSeconds);
        float t01 = unprepareSeconds <= 0.0001f ? 1f : (elapsed / unprepareSeconds);

        int frame = Mathf.Clamp(Mathf.RoundToInt((1f - t01) * last), 0, last);
        p.CurrentFrame = frame;
        p.idle = false;
        p.RefreshFrame();
    }

    private int GetLastFrameIndex(AnimatedSpriteRenderer r)
    {
        if (r == null || r.animationSprite == null || r.animationSprite.Length <= 0)
            return 0;

        return r.animationSprite.Length - 1;
    }

    private void HookHealth()
    {
        healthRef = GetComponent<CharacterHealth>();
        if (healthRef == null)
            return;

        healthRef.HitInvulnerabilityStarted += OnHealthInvulnStarted;
        healthRef.HitInvulnerabilityEnded += OnHealthInvulnEnded;
        healthRef.Died += OnHealthDiedInternal;
    }

    private void UnhookHealth()
    {
        if (healthRef == null)
            return;

        healthRef.HitInvulnerabilityStarted -= OnHealthInvulnStarted;
        healthRef.HitInvulnerabilityEnded -= OnHealthInvulnEnded;
        healthRef.Died -= OnHealthDiedInternal;
    }

    private void OnHealthInvulnStarted(float seconds)
    {
        preparing = false;
        charging = false;
        unpreparing = false;

        stateTimer = 0f;
        damageTimer = 0f;

        DisableAbilityInstances();
        DisableAllStateSprites();

    }

    private void OnHealthInvulnEnded()
    {
        if (isDead)
            return;

        DisableAbilityInstances();

        SetVisualState(VisualState.Walking);
        UpdateSpriteDirection(direction);
        DecideNextTile();

    }

    private void OnHealthDiedInternal()
    {
        preparing = false;
        charging = false;
        unpreparing = false;

        DisableAbilityInstances();
        DisableAllStateSprites();

    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (!Application.isPlaying || rb == null)
            return;

        if (!charging)
            return;

        float p = Mathf.Clamp(damageBoxSizePercent, 0.1f, 1f);
        Vector2 size = Vector2.one * (tileSize * p);

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(GetAbilityTileCenterWorld(Vector2.up), size);
        Gizmos.DrawWireCube(GetAbilityTileCenterWorld(Vector2.down), size);
        Gizmos.DrawWireCube(GetAbilityTileCenterWorld(Vector2.left), size);
        Gizmos.DrawWireCube(GetAbilityTileCenterWorld(Vector2.right), size);
    }

    private void ValidateAbilityPrototype(AnimatedSpriteRenderer prototype, string tag)
    {
        if (prototype == null)
        {
            return;
        }

    }

    private void TryLogAbilityFrames()
    {
        if (!enableSurgicalLogs)
            return;

        if (Time.unscaledTime < nextAbilityLogTime)
            return;

        nextAbilityLogTime = Time.unscaledTime + abilityLogInterval;

        LogAbilityFrame("Up", abilityUpInstance, ref lastLoggedFrameUp);
        LogAbilityFrame("Down", abilityDownInstance, ref lastLoggedFrameDown);
        LogAbilityFrame("Left", abilityLeftInstance, ref lastLoggedFrameLeft);
        LogAbilityFrame("Right", abilityRightInstance, ref lastLoggedFrameRight);
    }

    private void LogAbilityFrame(string tag, AnimatedSpriteRenderer inst, ref int lastLoggedFrame)
    {
        if (inst == null)
        {
            return;
        }

        int current = inst.CurrentFrame;
        bool changed = current != lastLoggedFrame;


        lastLoggedFrame = current;
    }

    private void ResetAbilityFrameLogs()
    {
        lastLoggedFrameUp = -999;
        lastLoggedFrameDown = -999;
        lastLoggedFrameLeft = -999;
        lastLoggedFrameRight = -999;
        nextAbilityLogTime = 0f;
    }

    private int GetAnimCount(AnimatedSpriteRenderer r)
    {
        if (r == null || r.animationSprite == null)
            return 0;

        return r.animationSprite.Length;
    }

    private bool HasNullAnimationSprite(AnimatedSpriteRenderer r)
    {
        if (r == null || r.animationSprite == null)
            return false;

        for (int i = 0; i < r.animationSprite.Length; i++)
        {
            if (r.animationSprite[i] == null)
                return true;
        }

        return false;
    }
}