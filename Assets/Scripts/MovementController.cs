using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AbilitySystem))]
[RequireComponent(typeof(StunReceiver))]
public class MovementController : MonoBehaviour, IKillable
{
    public event Action<MovementController> Died;

    [Header("SFX")]
    public AudioClip deathSfx;

    [Header("Stats")]
    public float speed = 5f;
    public float tileSize = 1f;
    public LayerMask obstacleMask;

    [Header("Speed (SB5 Internal)")]
    [SerializeField] private int speedInternal = PlayerPersistentStats.BaseSpeedNormal;
    public int SpeedInternal => speedInternal;

    [Header("Input")]
    public KeyCode inputUp = KeyCode.W;
    public KeyCode inputDown = KeyCode.S;
    public KeyCode inputLeft = KeyCode.A;
    public KeyCode inputRight = KeyCode.D;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererDeath;
    public AnimatedSpriteRenderer spriteRendererEndStage;
    public AnimatedSpriteRenderer spriteRendererCheering;

    [Header("Mounted On Louie")]
    public AnimatedSpriteRenderer mountedSpriteUp;
    public AnimatedSpriteRenderer mountedSpriteDown;
    public AnimatedSpriteRenderer mountedSpriteLeft;
    public AnimatedSpriteRenderer mountedSpriteRight;

    [Header("Mounted On Louie - Pink Y Override")]
    public float pinkMountedSpritesLocalY = 1.3f;

    bool mountedSpritesYOverridden;

    [Header("Contact Damage")]
    public float contactDamageCooldownSeconds = 0.15f;

    float nextContactDamageTime;

    readonly System.Collections.Generic.HashSet<Collider2D> touchingHazards = new();

    [Header("Death Timing")]
    public float deathDisableSeconds = 2f;

    [Header("Death Behavior")]
    public bool checkWinStateOnDeath = true;

    protected Vector2 facingDirection = Vector2.down;
    public Vector2 FacingDirection => facingDirection;

    private bool isMountedOnLouie;
    public bool IsMountedOnLouie => isMountedOnLouie;

    StunReceiver stunReceiver;

    [Header("End Stage Animation")]
    public float endStageTotalTime = 1f;
    public int endStageFrameCount = 9;

    public Rigidbody2D Rigidbody { get; private set; }
    public Vector2 Direction => direction;

    protected AudioSource audioSource;
    protected BombController bombController;
    protected AbilitySystem abilitySystem;

    protected AnimatedSpriteRenderer activeSpriteRenderer;

    protected Vector2 direction = Vector2.zero;
    protected bool hasInput;
    protected bool inputLocked;
    protected bool explosionInvulnerable;

    public bool isDead;
    public bool InputLocked => inputLocked;
    private bool isEndingStage;
    public bool IsEndingStage => isEndingStage;

    private const float CenterEpsilon = 0.01f;
    private float SlideDeadZone => tileSize * 0.25f;

    protected virtual void Awake()
    {
        Rigidbody = GetComponent<Rigidbody2D>();
        stunReceiver = GetComponent<StunReceiver>();
        bombController = GetComponent<BombController>();

        abilitySystem = GetComponent<AbilitySystem>();
        if (abilitySystem == null)
            abilitySystem = gameObject.AddComponent<AbilitySystem>();

        if (CompareTag("Player"))
            PlayerPersistentStats.LoadInto(this, bombController);

        activeSpriteRenderer = spriteRendererDown;
        direction = Vector2.zero;

        audioSource = GetComponent<AudioSource>();

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Stage", "Bomb");

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.clip = null;
        }

        ApplySpeedInternal(speedInternal);
    }

    protected virtual void OnEnable()
    {
        direction = Vector2.zero;
        hasInput = false;
        touchingHazards.Clear();

        if (CompareTag("Player"))
            PlayerPersistentStats.LoadInto(this, bombController);

        ApplySpeedInternal(speedInternal);
        ForceExclusiveSpriteFromState();
    }

    protected virtual void OnDisable()
    {
        touchingHazards.Clear();
    }

    protected virtual void Update()
    {
        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            hasInput = false;
            direction = Vector2.zero;

            if (activeSpriteRenderer != null)
            {
                activeSpriteRenderer.idle = true;
                activeSpriteRenderer.RefreshFrame();
            }

            return;
        }

        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        hasInput = false;
        HandleInput();
    }

    protected virtual void HandleInput()
    {
        if (Input.GetKey(inputUp))
            ApplyDirectionFromVector(Vector2.up);
        else if (Input.GetKey(inputDown))
            ApplyDirectionFromVector(Vector2.down);
        else if (Input.GetKey(inputLeft))
            ApplyDirectionFromVector(Vector2.left);
        else if (Input.GetKey(inputRight))
            ApplyDirectionFromVector(Vector2.right);
        else
            ApplyDirectionFromVector(Vector2.zero);
    }

    public void ApplyDirectionFromVector(Vector2 dir)
    {
        hasInput = dir != Vector2.zero;

        if (dir != Vector2.zero)
            facingDirection = dir;

        if (isMountedOnLouie)
        {
            if (dir == Vector2.up)
                SetDirection(Vector2.up, mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp);
            else if (dir == Vector2.down)
                SetDirection(Vector2.down, mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown);
            else if (dir == Vector2.left)
                SetDirection(Vector2.left, mountedSpriteLeft != null ? mountedSpriteLeft : spriteRendererLeft);
            else if (dir == Vector2.right)
                SetDirection(Vector2.right, mountedSpriteRight != null ? mountedSpriteRight : spriteRendererRight);
            else
            {
                Vector2 face = facingDirection;

                if (face == Vector2.up)
                    SetDirection(Vector2.zero, mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp);
                else if (face == Vector2.down)
                    SetDirection(Vector2.zero, mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown);
                else if (face == Vector2.left)
                    SetDirection(Vector2.zero, mountedSpriteLeft != null ? mountedSpriteLeft : spriteRendererLeft);
                else if (face == Vector2.right)
                    SetDirection(Vector2.zero, mountedSpriteRight != null ? mountedSpriteRight : spriteRendererRight);
                else
                    SetDirection(Vector2.zero, mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown);
            }

            return;
        }

        if (dir == Vector2.up)
            SetDirection(Vector2.up, spriteRendererUp);
        else if (dir == Vector2.down)
            SetDirection(Vector2.down, spriteRendererDown);
        else if (dir == Vector2.left)
            SetDirection(Vector2.left, spriteRendererLeft);
        else if (dir == Vector2.right)
            SetDirection(Vector2.right, spriteRendererRight);
        else
        {
            Vector2 face = facingDirection;

            if (face == Vector2.up)
                SetDirection(Vector2.zero, spriteRendererUp);
            else if (face == Vector2.down)
                SetDirection(Vector2.zero, spriteRendererDown);
            else if (face == Vector2.left)
                SetDirection(Vector2.zero, spriteRendererLeft);
            else if (face == Vector2.right)
                SetDirection(Vector2.zero, spriteRendererRight);
            else
                SetDirection(Vector2.zero, activeSpriteRenderer);
        }
    }

    public void ApplySpeedInternal(int newInternal)
    {
        speedInternal = PlayerPersistentStats.ClampSpeedInternal(newInternal);
        speed = PlayerPersistentStats.InternalSpeedToTilesPerSecond(speedInternal);
    }

    public bool TryAddSpeedUp(int speedStep = PlayerPersistentStats.SpeedStep)
    {
        int before = speedInternal;
        ApplySpeedInternal(speedInternal + speedStep);
        return speedInternal != before;
    }

    protected virtual void FixedUpdate()
    {
        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        if (!hasInput || direction == Vector2.zero)
            return;

        if (stunReceiver != null && stunReceiver.IsStunned)
        {
            if (Rigidbody != null)
                Rigidbody.linearVelocity = Vector2.zero;

            return;
        }

        float dt = Time.fixedDeltaTime;
        float speedWorldPerSecond = speed * tileSize;
        float moveSpeed = speedWorldPerSecond * dt;

        Vector2 position = Rigidbody.position;

        bool blockLeft = IsSolidAt(position + Vector2.left * (tileSize * 0.5f));
        bool blockRight = IsSolidAt(position + Vector2.right * (tileSize * 0.5f));
        bool blockUp = IsSolidAt(position + Vector2.up * (tileSize * 0.5f));
        bool blockDown = IsSolidAt(position + Vector2.down * (tileSize * 0.5f));

        bool movingVertical = Mathf.Abs(direction.y) > 0.01f;
        bool movingHorizontal = Mathf.Abs(direction.x) > 0.01f;

        if (movingVertical && blockLeft && blockRight)
        {
            float targetX = Mathf.Round(position.x / tileSize) * tileSize;
            position.x = Mathf.MoveTowards(position.x, targetX, moveSpeed);
        }

        if (movingHorizontal && blockUp && blockDown)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            position.y = Mathf.MoveTowards(position.y, targetY, moveSpeed);
        }

        Vector2 targetPosition = position + direction * moveSpeed;

        if (!IsBlocked(targetPosition))
        {
            Rigidbody.MovePosition(targetPosition);
            return;
        }

        if (movingVertical)
        {
            float currentCenterX = Mathf.Round(position.x / tileSize) * tileSize;
            float offsetX = Mathf.Abs(position.x - currentCenterX);

            if (offsetX > SlideDeadZone)
                TrySlideHorizontally(position, moveSpeed);
        }
        else if (movingHorizontal)
        {
            float currentCenterY = Mathf.Round(position.y / tileSize) * tileSize;
            float offsetY = Mathf.Abs(position.y - currentCenterY);

            if (offsetY > SlideDeadZone)
                TrySlideVertically(position, moveSpeed);
        }
    }

    void TrySlideHorizontally(Vector2 position, float moveSpeed)
    {
        float leftCenter = Mathf.Floor(position.x / tileSize) * tileSize;
        float rightCenter = Mathf.Ceil(position.x / tileSize) * tileSize;

        Vector2 verticalStep = new(0f, direction.y * moveSpeed);

        bool leftFree = !IsBlocked(new Vector2(leftCenter, position.y) + verticalStep);
        bool rightFree = !IsBlocked(new Vector2(rightCenter, position.y) + verticalStep);

        if (!leftFree && !rightFree)
            return;

        float targetX;

        if (leftFree && !rightFree)
            targetX = leftCenter;
        else if (rightFree && !leftFree)
            targetX = rightCenter;
        else
        {
            targetX = Mathf.Abs(position.x - leftCenter) <= Mathf.Abs(position.x - rightCenter)
                ? leftCenter
                : rightCenter;
        }

        if (Mathf.Abs(position.x - targetX) > CenterEpsilon)
        {
            float newX = Mathf.MoveTowards(position.x, targetX, moveSpeed);
            Rigidbody.MovePosition(new Vector2(newX, position.y));
        }
        else
        {
            Vector2 newPos = new Vector2(targetX, position.y) + verticalStep;
            if (!IsBlocked(newPos))
                Rigidbody.MovePosition(newPos);
        }
    }

    void TrySlideVertically(Vector2 position, float moveSpeed)
    {
        float bottomCenter = Mathf.Floor(position.y / tileSize) * tileSize;
        float topCenter = Mathf.Ceil(position.y / tileSize) * tileSize;

        Vector2 horizontalStep = new(direction.x * moveSpeed, 0f);

        bool bottomFree = !IsBlocked(new Vector2(position.x, bottomCenter) + horizontalStep);
        bool topFree = !IsBlocked(new Vector2(position.x, topCenter) + horizontalStep);

        if (!bottomFree && !topFree)
            return;

        float targetY;

        if (bottomFree && !topFree)
            targetY = bottomCenter;
        else if (topFree && !bottomFree)
            targetY = topCenter;
        else
        {
            targetY = Mathf.Abs(position.y - bottomCenter) <= Mathf.Abs(position.y - topCenter)
                ? bottomCenter
                : topCenter;
        }

        if (Mathf.Abs(position.y - targetY) > CenterEpsilon)
        {
            float newY = Mathf.MoveTowards(position.y, targetY, moveSpeed);
            Rigidbody.MovePosition(new Vector2(position.x, newY));
        }
        else
        {
            Vector2 newPos = new Vector2(position.x, targetY) + horizontalStep;
            if (!IsBlocked(newPos))
                Rigidbody.MovePosition(newPos);
        }
    }

    protected bool IsSolidAt(Vector2 worldPosition)
    {
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPosition, size, 0f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles =
            abilitySystem != null &&
            abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            if (hit.gameObject.layer == LayerMask.NameToLayer("Bomb"))
            {
                var bomb = hit.GetComponent<Bomb>();
                if (bomb != null && bomb.Owner == bombController)
                {
                    var bombCollider = bomb.GetComponent<Collider2D>();
                    if (bombCollider != null && bombCollider.isTrigger)
                        continue;
                }
            }

            return true;
        }

        return false;
    }

    protected bool IsBlocked(Vector2 targetPosition)
    {
        Vector2 size;

        if (Mathf.Abs(direction.x) > 0f)
            size = new Vector2(tileSize * 0.6f, tileSize * 0.2f);
        else
            size = new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles =
            abilitySystem != null &&
            abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

        var monos = GetComponents<MonoBehaviour>();

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            for (int i = 0; i < monos.Length; i++)
            {
                if (monos[i] is IMovementAbility ability && ability.IsEnabled)
                {
                    if (ability.TryHandleBlockedHit(hit, direction, tileSize, obstacleMask))
                        return true;
                }
            }

            return true;
        }

        return false;
    }

    protected void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        direction = newDirection;

        if (newDirection != Vector2.zero)
            facingDirection = newDirection;

        if (spriteRenderer == null)
        {
            if (activeSpriteRenderer != null)
            {
                activeSpriteRenderer.idle = (direction == Vector2.zero);
                activeSpriteRenderer.RefreshFrame();
            }
            return;
        }

        bool isIdle = (direction == Vector2.zero);

        if (activeSpriteRenderer != spriteRenderer)
        {
            if (activeSpriteRenderer != null)
                activeSpriteRenderer.enabled = false;

            spriteRenderer.idle = isIdle;
            spriteRenderer.enabled = true;
            activeSpriteRenderer = spriteRenderer;
        }
        else
        {
            activeSpriteRenderer.idle = isIdle;
        }

        activeSpriteRenderer.RefreshFrame();
        ApplyFlipForHorizontal(facingDirection);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        RegisterHazard(other);
        TryApplyHazardDamage(other);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        RegisterHazard(other);
        TryApplyHazardDamage(other);
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
            return;

        touchingHazards.Remove(other);
    }

    void RegisterHazard(Collider2D other)
    {
        if (other == null)
            return;

        int layer = other.gameObject.layer;
        if (layer != LayerMask.NameToLayer("Explosion") && layer != LayerMask.NameToLayer("Enemy"))
            return;

        touchingHazards.Add(other);
    }

    void TryApplyHazardDamage(Collider2D other)
    {
        if (isDead || isEndingStage)
            return;

        if (other == null)
            return;

        int layer = other.gameObject.layer;
        if (layer != LayerMask.NameToLayer("Explosion") && layer != LayerMask.NameToLayer("Enemy"))
            return;

        if (Time.time < nextContactDamageTime)
            return;

        if (layer == LayerMask.NameToLayer("Explosion") && explosionInvulnerable)
            return;

        CharacterHealth playerHealth = null;

        if (TryGetComponent<CharacterHealth>(out var ph) && ph != null)
        {
            playerHealth = ph;
            if (playerHealth.IsInvulnerable)
                return;
        }

        if (CompareTag("Player") && isMountedOnLouie)
        {
            var mountedHealth = GetMountedLouieHealth();
            if (mountedHealth != null && mountedHealth.IsInvulnerable)
                return;

            if (TryGetComponent<PlayerLouieCompanion>(out var companion) && companion != null)
            {
                companion.OnMountedLouieHit(1);
                nextContactDamageTime = Time.time + Mathf.Max(0.01f, contactDamageCooldownSeconds);
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(1);
                nextContactDamageTime = Time.time + Mathf.Max(0.01f, contactDamageCooldownSeconds);
                return;
            }

            Kill();
            nextContactDamageTime = Time.time + Mathf.Max(0.01f, contactDamageCooldownSeconds);
            return;
        }

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(1);
            nextContactDamageTime = Time.time + Mathf.Max(0.01f, contactDamageCooldownSeconds);
            return;
        }

        DeathSequence();
        nextContactDamageTime = Time.time + Mathf.Max(0.01f, contactDamageCooldownSeconds);
    }

    CharacterHealth GetMountedLouieHealth()
    {
        var louieMove = GetComponentInChildren<LouieMovementController>(true);
        if (louieMove == null)
            return null;

        return louieMove.GetComponent<CharacterHealth>();
    }

    public virtual void Kill()
    {
        if (isEndingStage)
            return;

        if (!isDead)
            DeathSequence();
    }

    protected virtual void DeathSequence()
    {
        if (isDead || isEndingStage)
            return;

        isDead = true;
        inputLocked = true;

        touchingHazards.Clear();

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        if (CompareTag("Player"))
        {
            PlayerPersistentStats.Life = 1;

            if (TryGetComponent<CharacterHealth>(out var health) && health != null)
                health.life = 1;

            PlayerPersistentStats.CanKickBombs = false;
            PlayerPersistentStats.CanPunchBombs = false;
            PlayerPersistentStats.HasPierceBombs = false;
            PlayerPersistentStats.HasControlBombs = false;
            PlayerPersistentStats.HasFullFire = false;
            PlayerPersistentStats.CanPassBombs = false;
            PlayerPersistentStats.CanPassDestructibles = false;
            PlayerPersistentStats.MountedLouie = MountedLouieType.None;
        }

        if (bombController != null)
            bombController.enabled = false;

        if (Rigidbody != null)
        {
            Rigidbody.linearVelocity = Vector2.zero;
            Rigidbody.simulated = false;
        }

        if (TryGetComponent<Collider2D>(out var col) && col != null)
            col.enabled = false;

        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        if (CompareTag("Player"))
        {
            if (GameMusicController.Instance != null &&
                GameMusicController.Instance.deathMusic != null)
            {
                GameMusicController.Instance.PlayMusic(
                    GameMusicController.Instance.deathMusic, 1f, false);
            }
        }

        DisableAllFootSprites();
        DisableAllMountedSprites();

        if (spriteRendererCheering != null)
            spriteRendererCheering.enabled = false;

        if (spriteRendererEndStage != null)
            spriteRendererEndStage.enabled = false;

        if (spriteRendererDeath != null)
        {
            spriteRendererDeath.enabled = true;
            spriteRendererDeath.idle = false;
            spriteRendererDeath.loop = false;
            activeSpriteRenderer = spriteRendererDeath;
            spriteRendererDeath.RefreshFrame();
        }
        else
        {
            if (activeSpriteRenderer != null)
            {
                activeSpriteRenderer.enabled = true;
                activeSpriteRenderer.idle = true;
                activeSpriteRenderer.loop = false;
                activeSpriteRenderer.RefreshFrame();
            }
        }

        Invoke(nameof(OnDeathSequenceEnded), deathDisableSeconds);
    }

    protected virtual void OnDeathSequenceEnded()
    {
        gameObject.SetActive(false);

        Died?.Invoke(this);

        if (CompareTag("BossBomber"))
            return;

        if (!checkWinStateOnDeath)
            return;

        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.CheckWinState();
    }

    public void PlayEndStageSequence(Vector2 portalCenter)
    {
        isEndingStage = true;
        SetExplosionInvulnerable(true);

        if (abilitySystem != null)
            abilitySystem.DisableAll();

        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.position = portalCenter;
        direction = Vector2.zero;

        if (spriteRendererUp != null) spriteRendererUp.enabled = false;
        if (spriteRendererDown != null) spriteRendererDown.enabled = false;
        if (spriteRendererLeft != null) spriteRendererLeft.enabled = false;
        if (spriteRendererRight != null) spriteRendererRight.enabled = false;

        if (spriteRendererDeath != null)
            spriteRendererDeath.enabled = false;

        if (spriteRendererCheering != null)
            spriteRendererCheering.enabled = false;

        if (spriteRendererEndStage != null)
            spriteRendererEndStage.enabled = false;

        if (isMountedOnLouie)
        {
            facingDirection = Vector2.down;
            direction = Vector2.zero;
            hasInput = false;

            if (mountedSpriteUp != null) mountedSpriteUp.enabled = false;
            if (mountedSpriteLeft != null) mountedSpriteLeft.enabled = false;
            if (mountedSpriteRight != null) mountedSpriteRight.enabled = false;

            var mountedDown = mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;

            if (mountedDown != null)
            {
                mountedDown.enabled = true;
                mountedDown.idle = true;
                mountedDown.loop = false;
                mountedDown.RefreshFrame();

                activeSpriteRenderer = mountedDown;
            }

            if (TryGetComponent<PlayerLouieCompanion>(out var companion) && companion != null)
                companion.TryPlayMountedLouieEndStage(endStageTotalTime, endStageFrameCount);

            return;
        }

        var endSprite = spriteRendererEndStage != null ? spriteRendererEndStage : spriteRendererDown;

        if (endSprite != null)
        {
            endSprite.enabled = true;
            endSprite.idle = false;
            endSprite.loop = false;

            if (endStageFrameCount > 0)
                endSprite.animationTime = endStageTotalTime / endStageFrameCount;

            activeSpriteRenderer = endSprite;
        }
    }

    public void SetInputLocked(bool locked, bool forceIdle)
    {
        inputLocked = locked;

        if (locked && forceIdle)
            ForceIdleUp();
    }

    public void SetInputLocked(bool locked)
    {
        SetInputLocked(locked, true);
    }

    public void ForceIdleUp()
    {
        direction = Vector2.zero;
        hasInput = false;

        if (spriteRendererUp != null)
            SetDirection(Vector2.zero, spriteRendererUp);
        else
            SetDirection(Vector2.zero, activeSpriteRenderer);
    }

    public void ForceIdleUpConsideringMount()
    {
        direction = Vector2.zero;
        hasInput = false;

        facingDirection = Vector2.up;

        if (isMountedOnLouie)
        {
            var up = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;
            SetDirection(Vector2.zero, up);

            var rider = GetComponentInChildren<LouieRiderVisual>(true);
            if (rider != null)
                rider.ForceIdleUp();
        }
        else
        {
            if (spriteRendererUp != null)
                SetDirection(Vector2.zero, spriteRendererUp);
            else
                SetDirection(Vector2.zero, activeSpriteRenderer);
        }
    }

    public void ForceMountedUpExclusive()
    {
        direction = Vector2.zero;
        hasInput = false;

        facingDirection = Vector2.up;

        if (!isMountedOnLouie)
        {
            ForceIdleUp();
            return;
        }

        DisableAllFootSprites();
        DisableAllMountedSprites();

        var up = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;

        if (up != null)
        {
            up.enabled = true;
            up.idle = true;
            up.loop = false;
            up.RefreshFrame();

            activeSpriteRenderer = up;
        }

        var rider = GetComponentInChildren<LouieRiderVisual>(true);
        if (rider != null)
            rider.ForceOnlyUpEnabled();
    }

    public void SetExplosionInvulnerable(bool value)
    {
        explosionInvulnerable = value;
    }

    public void StartCheering()
    {
        if (isDead)
            return;

        SetExplosionInvulnerable(true);
        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        direction = Vector2.zero;
        hasInput = false;

        if (Rigidbody != null)
            Rigidbody.linearVelocity = Vector2.zero;

        if (spriteRendererUp != null) spriteRendererUp.enabled = false;
        if (spriteRendererDown != null) spriteRendererDown.enabled = false;
        if (spriteRendererLeft != null) spriteRendererLeft.enabled = false;
        if (spriteRendererRight != null) spriteRendererRight.enabled = false;
        if (spriteRendererDeath != null) spriteRendererDeath.enabled = false;
        if (spriteRendererEndStage != null) spriteRendererEndStage.enabled = false;

        if (spriteRendererCheering != null)
        {
            spriteRendererCheering.enabled = true;
            spriteRendererCheering.idle = false;
            spriteRendererCheering.loop = true;
            activeSpriteRenderer = spriteRendererEndStage;
        }
    }

    protected void ApplyFlipForHorizontal(Vector2 dir)
    {
        if (activeSpriteRenderer == null)
            return;

        if (!activeSpriteRenderer.TryGetComponent<SpriteRenderer>(out var sr))
            return;

        if (!activeSpriteRenderer.allowFlipX)
        {
            sr.flipX = false;
            return;
        }

        if (dir == Vector2.right)
            sr.flipX = true;
        else if (dir == Vector2.left)
            sr.flipX = false;
    }

    public void SetMountedOnLouie(bool mounted)
    {
        isMountedOnLouie = mounted;

        if (mounted)
        {
            DisableAllFootSprites();
            DisableAllMountedSprites();

            facingDirection = Vector2.down;
            direction = Vector2.zero;
            hasInput = false;

            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        if (mountedSpritesYOverridden)
            SetMountedSpritesLocalYOverride(false, 0f);

        DisableAllMountedSprites();
        ApplyDirectionFromVector(direction);
    }

    private void DisableAllFootSprites()
    {
        if (spriteRendererUp != null) spriteRendererUp.enabled = false;
        if (spriteRendererDown != null) spriteRendererDown.enabled = false;
        if (spriteRendererLeft != null) spriteRendererLeft.enabled = false;
        if (spriteRendererRight != null) spriteRendererRight.enabled = false;
    }

    private void DisableAllMountedSprites()
    {
        if (mountedSpriteUp != null) mountedSpriteUp.enabled = false;
        if (mountedSpriteDown != null) mountedSpriteDown.enabled = false;
        if (mountedSpriteLeft != null) mountedSpriteLeft.enabled = false;
        if (mountedSpriteRight != null) mountedSpriteRight.enabled = false;
    }

    public void SetMountedSpritesLocalYOverride(bool enable, float localY)
    {
        if (enable)
        {
            ApplyMountedSpritesLocalY(localY);
            mountedSpritesYOverridden = true;
            return;
        }

        ClearMountedSpritesLocalYOverride();
        mountedSpritesYOverridden = false;
    }

    void ApplyMountedSpritesLocalY(float localY)
    {
        if (mountedSpriteUp != null) mountedSpriteUp.SetRuntimeBaseLocalY(localY);
        if (mountedSpriteDown != null) mountedSpriteDown.SetRuntimeBaseLocalY(localY);
        if (mountedSpriteLeft != null) mountedSpriteLeft.SetRuntimeBaseLocalY(localY);
        if (mountedSpriteRight != null) mountedSpriteRight.SetRuntimeBaseLocalY(localY);
    }

    void ClearMountedSpritesLocalYOverride()
    {
        if (mountedSpriteUp != null) mountedSpriteUp.ClearRuntimeBaseOffset();
        if (mountedSpriteDown != null) mountedSpriteDown.ClearRuntimeBaseOffset();
        if (mountedSpriteLeft != null) mountedSpriteLeft.ClearRuntimeBaseOffset();
        if (mountedSpriteRight != null) mountedSpriteRight.ClearRuntimeBaseOffset();
    }

    void ForceExclusiveSpriteFromState()
    {
        if (isDead)
            return;

        DisableAllFootSprites();
        DisableAllMountedSprites();

        AnimatedSpriteRenderer target = null;

        Vector2 face = facingDirection;
        if (face == Vector2.zero)
            face = Vector2.down;

        if (isMountedOnLouie)
        {
            if (face == Vector2.up) target = mountedSpriteUp != null ? mountedSpriteUp : spriteRendererUp;
            else if (face == Vector2.down) target = mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;
            else if (face == Vector2.left) target = mountedSpriteLeft != null ? mountedSpriteLeft : spriteRendererLeft;
            else if (face == Vector2.right) target = mountedSpriteRight != null ? mountedSpriteRight : spriteRendererRight;
            else target = mountedSpriteDown != null ? mountedSpriteDown : spriteRendererDown;
        }
        else
        {
            if (face == Vector2.up) target = spriteRendererUp;
            else if (face == Vector2.down) target = spriteRendererDown;
            else if (face == Vector2.left) target = spriteRendererLeft;
            else if (face == Vector2.right) target = spriteRendererRight;
            else target = spriteRendererDown;
        }

        activeSpriteRenderer = null;

        SetDirection(Vector2.zero, target);
    }

    public void ForceExclusiveSpriteNow()
    {
        ForceExclusiveSpriteFromState();
    }
}
