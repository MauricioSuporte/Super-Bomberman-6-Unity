using UnityEngine;

/// <summary>Stationary five-tile flight cycle for Mageko; ground navigation stays junction-turning.</summary>
public sealed class MagekoMovementController : JunctionTurningEnemyMovementController
{
    [SerializeField] private Sprite prepareSprite;
    [SerializeField] private Sprite airborneSprite;
    [SerializeField] private Sprite descentSprite;
    [SerializeField] private Sprite shadowSprite;
    [SerializeField] private AnimatedSpriteRenderer leftWing;
    [SerializeField] private AnimatedSpriteRenderer rightWing;
    [SerializeField] private SpriteRenderer ascend;
    [SerializeField] private SpriteRenderer descend;
    [SerializeField] private Sprite[] wingFrames;
    [SerializeField] private SpriteRenderer squishedPose;
    [SerializeField] private AnimatedSpriteRenderer coreDestruction;
    [SerializeField] private float preparationSeconds = 0.3f;
    [SerializeField] private float ascentSeconds = 0.5f;
    [SerializeField] private float groundWaitSeconds = 5f;
    [SerializeField] private float flightSeconds = 3f;
    [SerializeField] private float descentSeconds = 0.5f;
    [SerializeField] private float flightHeight = 3f;
    [SerializeField] private float hoverAmplitude = 0.08f;
    [SerializeField] private float hoverFrequency = 6f;
    [SerializeField] private Vector2 leftWingOffset;
    [SerializeField] private Vector2 rightWingOffset;
    private enum State { Ground, Preparing, Ascending, Flying, Descending }
    private State state;
    private float elapsed;
    private float groundElapsed;
    private GameObject shadow;
    private SpriteRenderer flightVisual;
    private static Sprite generatedShadowSprite;
    private Vector3 leftWingBasePosition;
    private Vector3 rightWingBasePosition;
    private Vector3 ascendBasePosition;
    private Vector3 descendBasePosition;
    private bool authoredPositionsCaptured;

    protected override void Awake()
    {
        base.Awake();
        flightHeight = 3f;
        flightSeconds = 3f;
    }

    protected override void Start()
    {
        base.Start();
        FindAuthoredFlightVisuals();
        CreateShadow();
        SetWings(false);
        SetAuthoredFlightVisuals(false, false);
    }
    protected override void FixedUpdate()
    {
        if (isDead)
        {
            ClearFlight();
            return;
        }

        if (TryGetComponent(out StunReceiver stun) && stun.IsStunned)
        {
            AbortFlight();
            return;
        }

        if (isInDamagedLoop)
        {
            AbortFlight();
            base.FixedUpdate();
            return;
        }

        if (state == State.Ground)
        {
            base.FixedUpdate();
            groundElapsed += Time.fixedDeltaTime;
            if (groundElapsed >= groundWaitSeconds)
                Begin(State.Preparing);
            return;
        }

        elapsed += Time.fixedDeltaTime;
        // Preparing happens entirely on the floor. It must show Ascend plus
        // both wings before any vertical offset is applied.
        float p = state == State.Ascending
            ? Mathf.Clamp01(elapsed / ascentSeconds)
            : state == State.Descending
                ? 1f - Mathf.Clamp01(elapsed / descentSeconds)
                : state == State.Flying
                    ? 1f
                    : 0f;
        float height = p * flightHeight;
        if (state == State.Flying)
        {
            // Keep the dragon just below its maximum height and bob it
            // softly without allowing it to exceed the configured 3 tiles.
            height = flightHeight - hoverAmplitude +
                (Mathf.Sin(elapsed * hoverFrequency) + 1f) * .5f * hoverAmplitude;
            p = height / flightHeight;
        }
        ApplyHeight(height); UpdateShadow(p);
        if ((state == State.Preparing && elapsed >= preparationSeconds) || (state == State.Ascending && elapsed >= ascentSeconds) || (state == State.Flying && elapsed >= flightSeconds) || (state == State.Descending && elapsed >= descentSeconds))
            Begin(state == State.Preparing ? State.Ascending : state == State.Ascending ? State.Flying : state == State.Flying ? State.Descending : State.Ground);
    }
    private void Begin(State next)
    {
        state = next; elapsed = 0f;
        SetWings(next != State.Ground);
        if (next == State.Preparing)
        {
            groundElapsed = 0f;
            SetSprite(prepareSprite, ascending: true);
            SetWingFrames(preparing: true);
        }
        else if (next == State.Ascending)
        {
            SetSprite(prepareSprite, ascending: true);
            SetWingFrames(preparing: false);
        }
        else if (next == State.Flying || next == State.Descending)
        {
            SetSprite(airborneSprite != null ? airborneSprite : descentSprite, ascending: false);
            SetWingFrames(preparing: false);
        }
        else
        {
            ClearFlight();
            groundElapsed = 0f;
            UpdateSpriteDirection(direction);
        }
    }
    private void SetSprite(Sprite sprite, bool ascending)
    {
        if (sprite == null)
            return;

        FindAuthoredFlightVisuals();
        DisableWalkingVisuals();

        if (ascend != null || descend != null)
        {
            SetAuthoredFlightVisuals(true, ascending);
            SpriteRenderer selected = ascending ? ascend : descend;
            if (selected != null)
                selected.sprite = sprite;
            return;
        }

        EnsureFlightVisual();
        flightVisual.sprite = sprite;
        flightVisual.enabled = true;
    }

    private void FindAuthoredFlightVisuals()
    {
        if (leftWing == null)
        {
            Transform child = transform.Find("LeftWind") ?? transform.Find("LeftWing");
            if (child != null)
            {
                child.name = "LeftWind";
                leftWing = child.GetComponent<AnimatedSpriteRenderer>();
            }
        }
        if (rightWing == null)
        {
            Transform child = transform.Find("RigthWing") ?? transform.Find("RigtWing") ?? transform.Find("RightWing");
            if (child != null)
            {
                child.name = "RigthWing";
                rightWing = child.GetComponent<AnimatedSpriteRenderer>();
            }
        }
        if (ascend == null)
        {
            Transform child = transform.Find("Ascend");
            if (child != null)
                ascend = child.GetComponent<SpriteRenderer>();
        }
        if (descend == null)
        {
            Transform child = transform.Find("Descend");
            if (child != null)
                descend = child.GetComponent<SpriteRenderer>();
        }

        if (!authoredPositionsCaptured &&
            (leftWing != null || rightWing != null || ascend != null || descend != null))
        {
            if (leftWing != null) leftWingBasePosition = leftWing.transform.localPosition;
            if (rightWing != null) rightWingBasePosition = rightWing.transform.localPosition;
            if (ascend != null) ascendBasePosition = ascend.transform.localPosition;
            if (descend != null) descendBasePosition = descend.transform.localPosition;
            authoredPositionsCaptured = true;
        }
    }

    private void SetAuthoredFlightVisuals(bool visible, bool showAscend)
    {
        if (ascend != null)
            ascend.enabled = visible && showAscend;
        if (descend != null)
            descend.enabled = visible && !showAscend;
    }

    private void EnsureFlightVisual()
    {
        if (flightVisual != null)
            return;

        GameObject visual = new("MagekoFlightVisual");
        visual.transform.SetParent(transform, false);
        flightVisual = visual.AddComponent<SpriteRenderer>();
        if (spriteDown != null && spriteDown.TryGetComponent(out SpriteRenderer downRenderer))
        {
            flightVisual.sortingLayerID = downRenderer.sortingLayerID;
            flightVisual.sortingOrder = downRenderer.sortingOrder;
        }
        RegisterForBlackout(flightVisual);
    }

    private void DisableWalkingVisuals()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;
    }

    private void ApplyHeight(float height)
    {
        if (flightVisual != null)
            flightVisual.transform.localPosition = Vector3.up * height;
        if (leftWing != null)
            leftWing.SetExternalBaseLocalPosition(leftWingBasePosition + Vector3.up * height);
        if (rightWing != null)
            rightWing.SetExternalBaseLocalPosition(rightWingBasePosition + Vector3.up * height);
        if (ascend != null)
            ascend.transform.localPosition = ascendBasePosition + Vector3.up * height;
        if (descend != null)
            descend.transform.localPosition = descendBasePosition + Vector3.up * height;
    }
    private void CreateShadow()
    {
        if (shadow != null)
            return;

        shadow = new GameObject("MagekoShadow");
        SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = shadowSprite != null ? shadowSprite : GetGeneratedShadowSprite();
        renderer.color = new Color(0f, 0f, 0f, .45f);
        renderer.sortingOrder = 4;
        if (spriteDown != null && spriteDown.TryGetComponent(out SpriteRenderer downRenderer))
        {
            renderer.sortingLayerID = downRenderer.sortingLayerID;
            renderer.sortingOrder = downRenderer.sortingOrder - 1;
        }
        shadow.SetActive(false);
    }

    private static Sprite GetGeneratedShadowSprite()
    {
        if (generatedShadowSprite != null)
            return generatedShadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "MagekoShadow"
        };
        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
            texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
        }
        texture.Apply();
        generatedShadowSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(.5f, .5f), 16f, 0, SpriteMeshType.FullRect);
        generatedShadowSprite.name = "MagekoShadowSprite";
        return generatedShadowSprite;
    }
    private void UpdateShadow(float height01)
    {
        if (shadow == null)
            return;

        // The preparation pose happens on the floor, but it still needs its
        // grounded shadow so the enemy does not visually disappear.
        shadow.SetActive(state == State.Preparing || height01 > 0f);
        shadow.transform.position = transform.position;
        float scale = Mathf.Lerp(1f, .3f, height01);
        shadow.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void SetWings(bool on)
    {
        EnsureWings();
        foreach (var wing in new[] { leftWing, rightWing })
        {
            if (wing == null)
                continue;

            wing.gameObject.SetActive(on);
            wing.enabled = on;
            wing.loop = true;
            wing.animationTime = .1f;

            if (wing.TryGetComponent(out SpriteRenderer renderer))
                renderer.enabled = on;

            if (on)
            {
                wing.idle = false;
                wing.RestartAnimation();
            }
        }
    }

    private void SetWingFrames(bool preparing)
    {
        if (wingFrames == null || wingFrames.Length == 0)
            return;

        // Preparation cycles 1-2-3. Once airborne, the deliberate 2-1-2-3
        // rhythm is used for both wings.
        Sprite first = wingFrames[0];
        Sprite second = wingFrames[Mathf.Min(1, wingFrames.Length - 1)];
        Sprite third = wingFrames[Mathf.Min(2, wingFrames.Length - 1)];
        Sprite[] sequence = preparing
            ? new[] { first, second, third }
            : new[] { second, first, second, third };

        foreach (AnimatedSpriteRenderer wing in new[] { leftWing, rightWing })
        {
            if (wing == null)
                continue;

            wing.idleSprite = sequence[0];
            wing.animationSprite = sequence;
            wing.CurrentFrame = 0;
            wing.idle = false;
            wing.loop = !preparing;
            wing.animationTime = preparing
                ? Mathf.Max(.01f, preparationSeconds / sequence.Length)
                : .1f;
            wing.RestartAnimation();
        }
    }
    private void EnsureWings() { if (wingFrames == null || wingFrames.Length == 0 || leftWing != null) return; leftWing = CreateWing("LeftWind", leftWingOffset, false); rightWing = CreateWing("RigthWing", rightWingOffset, true); FindAuthoredFlightVisuals(); }
    private AnimatedSpriteRenderer CreateWing(string wingName, Vector2 offset, bool flip) { var go=new GameObject(wingName); go.transform.SetParent(transform,false); go.transform.localPosition=offset; var r=go.AddComponent<SpriteRenderer>(); r.sprite=wingFrames[0]; r.flipX=flip; r.sortingOrder=4; RegisterForBlackout(r); var a=go.AddComponent<AnimatedSpriteRenderer>(); a.idleSprite=wingFrames[0]; a.animationSprite=new[]{wingFrames[0]}; a.animationTime=.1f; a.loop=true; return a; }

    private void RegisterForBlackout(SpriteRenderer renderer)
    {
        if (renderer == null || !TryGetComponent(out BlackoutVisibleParts blackout))
            return;

        blackout.RegisterRuntimePart(renderer, GetComponent<BlackoutColorPalette>());
    }
    private void ClearFlight()
    {
        if (activeSprite != null)
            activeSprite.ClearExternalBase();
        if (flightVisual != null)
        {
            flightVisual.enabled = false;
            flightVisual.transform.localPosition = Vector3.zero;
        }
        if (leftWing != null)
            leftWing.ClearExternalBase();
        if (rightWing != null)
            rightWing.ClearExternalBase();
        if (ascend != null)
            ascend.transform.localPosition = ascendBasePosition;
        if (descend != null)
            descend.transform.localPosition = descendBasePosition;
        SetAuthoredFlightVisuals(false, false);
        if (shadow != null)
            shadow.SetActive(false);
        SetWings(false);
    }
    private void AbortFlight()
    {
        if (state == State.Ground)
            return;

        state = State.Ground;
        elapsed = 0f;
        groundElapsed = 0f;
        ClearFlight();
        UpdateSpriteDirection(direction);
    }

    public bool TryBarrelCrushStun(float seconds)
    {
        if (state == State.Ascending || state == State.Flying || state == State.Descending)
            return false;

        if (!TryGetComponent(out StunReceiver stun) || !stun.TryCrushStun(seconds, squishedPose))
            return false;

        // A barrel can crush the dragon while it is preparing; that cancels
        // the pending flight instead of resuming it after the stun.
        AbortFlight();
        return true;
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        bool airborne = state == State.Ascending || state == State.Flying || state == State.Descending;
        if (airborne && other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
            return;

        base.OnTriggerEnter2D(other);
    }
    protected override void Die() { ClearFlight(); base.Die(); if(squishedPose!=null)squishedPose.enabled=false; }
    protected override void OnDestroy()
    {
        if (shadow != null)
            Destroy(shadow);

        base.OnDestroy();
    }
    protected override void OnDeathAnimationEnded() { if(coreDestruction==null){base.OnDeathAnimationEnded();return;} coreDestruction.gameObject.SetActive(true); coreDestruction.enabled=true; coreDestruction.idle=false; coreDestruction.loop=false; coreDestruction.useSequenceDuration=true; coreDestruction.sequenceDuration=.5f; coreDestruction.RestartAnimation(); Invoke(nameof(Finish),.5f); }
    private void Finish(){base.OnDeathAnimationEnded();}
}
