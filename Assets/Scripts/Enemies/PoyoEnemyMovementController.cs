using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public sealed class PoyoEnemyMovementController : JunctionTurningEnemyMovementController
{
    [Header("Jump (on spawn/launch)")]
    [SerializeField] private AnimatedSpriteRenderer jumpSprite;
    [SerializeField, Min(0.01f)] private float jumpSeconds = 0.5f;

    [Header("Launch Arc")]
    [SerializeField, Min(0.1f)] private float arcHeightTiles = 3f;

    [Header("Ground (optional override)")]
    [SerializeField] private Tilemap groundTilemapOverride;

    private Rigidbody2D _rb;
    private Collider2D _col;

    private bool _isLaunching;
    private bool _launchScheduled;
    private Vector2 _launchStart;
    private Vector2 _launchEnd;
    private float _launchSeconds;
    private float _launchArcTiles;

    protected override void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        if (jumpSeconds <= 0f)
            jumpSeconds = 0.5f;

        DisableJumpOnly();
    }

    protected override void Start()
    {
        if (_launchScheduled)
            return;

        base.Start();
    }

    protected override void FixedUpdate()
    {
        if (_isLaunching)
        {
            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;

            targetTile = _rb != null ? _rb.position : (Vector2)transform.position;
            return;
        }

        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (_isLaunching)
            return;

        base.UpdateSpriteDirection(dir);
    }

    public void LaunchTo(Vector2 landingWorld, float seconds, float arcHeightTilesOverride = 3f)
    {
        if (_launchScheduled || _isLaunching)
            return;

        _launchScheduled = true;

        _launchStart = _rb != null ? _rb.position : (Vector2)transform.position;
        _launchEnd = landingWorld;

        _launchSeconds = Mathf.Max(0.01f, seconds);
        _launchArcTiles = arcHeightTilesOverride > 0f ? arcHeightTilesOverride : arcHeightTiles;

        StartCoroutine(LaunchRoutine());
    }

    private IEnumerator LaunchRoutine()
    {
        _isLaunching = true;

        if (_col != null)
            _col.enabled = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        DisableAllDirectionalSprites();
        EnableJumpOnly();

        float dur = Mathf.Max(0.01f, _launchSeconds);
        float hWorld = _launchArcTiles * tileSize;

        float t = 0f;
        while (t < dur)
        {
            float u = t / dur;

            Vector2 pos = Vector2.Lerp(_launchStart, _launchEnd, u);
            float arc = Mathf.Sin(u * Mathf.PI) * hWorld;

            transform.position = new Vector3(pos.x, pos.y + arc, transform.position.z);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Vector2 landing = SnapWorldToGrid(_launchEnd);
        transform.position = new Vector3(landing.x, landing.y, transform.position.z);

        if (_rb != null)
        {
            _rb.simulated = true;
            _rb.position = landing;
            _rb.linearVelocity = Vector2.zero;
        }

        targetTile = landing;

        DisableJumpOnly();

        if (_col != null)
            _col.enabled = true;

        _isLaunching = false;
        _launchScheduled = false;

        ChooseInitialDirection();
        UpdateSpriteDirection(direction == Vector2.zero ? Vector2.down : direction);
        DecideNextTile();

        if (_rb != null)
            _rb.position = SnapWorldToGrid(_rb.position);
    }

    private void EnableJumpOnly()
    {
        DisableAllDirectionalSprites();

        if (jumpSprite == null)
            return;

        jumpSprite.enabled = true;
        jumpSprite.idle = false;
        jumpSprite.loop = true;
        jumpSprite.CurrentFrame = 0;
        jumpSprite.RefreshFrame();
        activeSprite = jumpSprite;

        if (jumpSprite.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            sr.flipX = false;
    }

    private void DisableJumpOnly()
    {
        if (jumpSprite != null)
            jumpSprite.enabled = false;

        if (activeSprite == jumpSprite)
            activeSprite = null;
    }

    private Vector2 SnapWorldToGrid(Vector2 world)
    {
        float ts = Mathf.Max(0.0001f, tileSize);
        world.x = Mathf.Round(world.x / ts) * ts;
        world.y = Mathf.Round(world.y / ts) * ts;
        return world;
    }

    void DisableAllDirectionalSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;

        if (spriteDamaged != null) spriteDamaged.enabled = false;

        if (spriteDeath != null && spriteDeath != activeSprite)
            spriteDeath.enabled = false;
    }
}
