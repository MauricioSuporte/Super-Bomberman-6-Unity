using UnityEngine;
using System.Text;

/// <summary>
/// A junction-turning enemy that gains a short burst of speed whenever it
/// makes a perpendicular turn.
/// </summary>
[RequireComponent(typeof(CharacterHealth))]
public sealed class BoboMovementController : JunctionTurningEnemyMovementController
{
    [Header("Bobo Turn Burst")]
    [SerializeField, Min(1f)] float turnSpeedMultiplier = 1.5f;
    [SerializeField, Min(0.01f)] float turnSpeedDuration = 0.18f;

    [Header("Vertical Sprites")]
    [Tooltip("Uses distinct Up and Down child sprites instead of mirroring the Down sprite vertically.")]
    [SerializeField] bool useAuthoredVerticalSprites;

    [Header("Debug")]
    [Tooltip("Logs the selected direction and every active movement sprite when the visual state changes.")]
    [SerializeField] bool logSpriteState;

    float normalSpeed;
    float turnSpeedTimer;
    string lastLoggedSpriteState;

    protected override void Awake()
    {
        base.Awake();
        normalSpeed = speed;
    }

    protected override void FixedUpdate()
    {
        if (turnSpeedTimer <= 0f && !Mathf.Approximately(speed, normalSpeed))
            speed = normalSpeed;

        base.FixedUpdate();

        if (turnSpeedTimer <= 0f)
            return;

        turnSpeedTimer -= Time.fixedDeltaTime;
        if (turnSpeedTimer <= 0f)
            speed = normalSpeed;
    }

    protected override void DecideNextTile()
    {
        Vector2 previousDirection = direction;
        base.DecideNextTile();

        if (previousDirection == Vector2.zero ||
            direction == previousDirection ||
            Vector2.Dot(previousDirection, direction) != 0f)
        {
            return;
        }

        turnSpeedTimer = turnSpeedDuration;
        speed = normalSpeed * turnSpeedMultiplier;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        base.UpdateSpriteDirection(dir);

        if (useAuthoredVerticalSprites)
        {
            SetVerticalFlip(spriteUp, false);
            SetVerticalFlip(spriteDown, false);
            return;
        }

        if (spriteDown != null && spriteDown.TryGetComponent<SpriteRenderer>(out var downRenderer))
            downRenderer.flipY = dir == Vector2.down;

        LogSpriteState($"direction={dir}");
    }

    void LateUpdate()
    {
        LogSpriteState("late-update");
    }

    void LogSpriteState(string trigger)
    {
        if (!logSpriteState)
            return;

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        var activeNames = new StringBuilder();
        int activeCount = 0;

        for (int i = 0; i < sprites.Length; i++)
        {
            AnimatedSpriteRenderer sprite = sprites[i];
            if (sprite == null || !sprite.enabled)
                continue;

            if (activeNames.Length > 0)
                activeNames.Append(", ");

            activeNames.Append(sprite.gameObject.name);
            activeCount++;
        }

        string state = $"dir={direction}; active={activeCount}; sprites=[{activeNames}]";
        if (state == lastLoggedSpriteState)
            return;

        lastLoggedSpriteState = state;
        Debug.Log($"[Bobo Sprite] '{name}' {trigger}; {state}; " +
                  $"configured Up='{NameOf(spriteUp)}', Down='{NameOf(spriteDown)}', " +
                  $"Left='{NameOf(spriteLeft)}', Right='{NameOf(spriteRight)}', " +
                  $"Death='{NameOf(spriteDeath)}'",
                  this);
    }

    static string NameOf(AnimatedSpriteRenderer sprite)
    {
        return sprite == null ? "<none>" : sprite.gameObject.name;
    }

    static void SetVerticalFlip(AnimatedSpriteRenderer sprite, bool flipY)
    {
        if (sprite != null && sprite.TryGetComponent<SpriteRenderer>(out var renderer))
            renderer.flipY = flipY;
    }
}
