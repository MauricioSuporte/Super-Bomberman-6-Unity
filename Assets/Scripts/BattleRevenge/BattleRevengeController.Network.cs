using System.Collections.Generic;
using Mirror;
using Assets.Scripts.Netcode;
using UnityEngine;

public sealed partial class BattleRevengeController
{
    public bool NetworkTickControlled { get; set; }
    public uint NetworkGeneration { get; private set; }
    public uint NetworkLaunchSerial { get; private set; }
    public float NetworkPerimeter => perimeterPosition;
    public int NetworkCartId => GetEntityId().GetHashCode();
    static readonly PlayerAction[] NetworkCartActions = { PlayerAction.MoveUp, PlayerAction.MoveDown,
        PlayerAction.MoveLeft, PlayerAction.MoveRight, PlayerAction.ActionA, PlayerAction.ActionC };
    // Cart wire masks predate PlayerAction's Start entry; never shift its enum ordinal.
    public static int NetworkCartActionMask(PlayerAction action) => action switch
    {
        PlayerAction.MoveUp => 1, PlayerAction.MoveDown => 2,
        PlayerAction.MoveLeft => 4, PlayerAction.MoveRight => 8,
        PlayerAction.ActionA => 16, PlayerAction.ActionC => 64,
        _ => 0
    };
    bool networkView;
    SpriteRenderer[] networkSprites;
    AnimatedSpriteRenderer[] networkAnimations;

    void BindNetworkControlOwner(MovementController source)
    {
        NetworkTickControlled = NetSync.IsServer && source != null && source.TryGetComponent<NetworkIdentity>(out var identity) &&
            identity.connectionToClient != null && identity.connectionToClient != NetworkServer.localConnection;
        BombermanNetworkManager.RevengeCartChanged();
    }

    public void PresentNetworkCartHud() => SyncTopHudOverlayImages();

    void CacheNetworkSprites()
    {
        if (networkSprites != null) return;
        networkSprites = GetComponentsInChildren<SpriteRenderer>(true);
        networkAnimations = new AnimatedSpriteRenderer[networkSprites.Length];
        for (int i = 0; i < networkSprites.Length; i++)
            networkAnimations[i] = networkSprites[i].GetComponent<AnimatedSpriteRenderer>();
    }

    public void CancelNetworkCharge()
    {
        isChargingLaunch = false;
        chargedLaunchDistanceTiles = Mathf.Clamp(minLaunchDistanceTiles, 3, 7);
        HideLandingIndicator();
    }

    public RevengeCartState CaptureNetworkCart(uint playerNetId, uint acknowledged)
    {
        CacheNetworkSprites();
        var poses = new List<RevengeCartSprite>();
        for (int i = 0; i < networkSprites.Length; i++)
        {
            var sprite = networkSprites[i];
            if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy) continue;
            var animation = networkAnimations[i];
            poses.Add(new RevengeCartSprite { index = (ushort)i, sortingLayer = sprite.sortingLayerID, sortingOrder = sprite.sortingOrder,
                frame = new NetworkWorldMount.MountFrame { visible = true, idle = animation != null && animation.idle,
                    frame = animation != null ? animation.CurrentFrame : 0, color = sprite.color,
                    flipX = sprite.flipX, flipY = sprite.flipY, pose = NetworkVisualPose.Capture(transform, sprite) } });
        }
        var targets = new Vector3[5];
        if (system != null && IsInLaunchableSegment)
            for (int distance = 3; distance <= 7; distance++)
                if (system.TryGetPredictedLandingPosition(this, distance, out var target))
                    targets[distance - 3] = new Vector3(target.x, target.y, 1);
        return new RevengeCartState { id = NetworkCartId, generation = NetworkGeneration, owner = ownerPlayerId,
            playerNetId = playerNetId, acknowledged = acknowledged, launchSerial = NetworkLaunchSerial,
            spawnPerimeter = spawnPerimeterPosition, suppressSpawnCorner = suppressCornerVisualAtSpawn,
            landingPosition = landingIndicatorRenderer != null ? landingIndicatorRenderer.transform.position : Vector3.zero,
            position = transform.position, recoilOffset = visualRoot != null ? visualRoot.localPosition : Vector3.zero, perimeter = perimeterPosition, animating = isAnimating,
            geometry = new[] { horizontalWallMinX, horizontalWallMaxX, verticalWallMinY, verticalWallMaxY,
                topWallY, bottomWallY, leftWallX, rightWallX, moveSpeed, actionCSpeedMultiplier },
            chargeTargets = targets, chargeStep = chargeStepSeconds, minCharge = minLaunchDistanceTiles,
            maxCharge = maxLaunchDistanceTiles, chargeReady = system != null && system.IsRuntimeEnabled && !isAnimating && Time.unscaledTime >= nextBombAllowedAt && targets[Mathf.Clamp(minLaunchDistanceTiles, 3, 7) - 3].z > 0,
            sprites = poses.ToArray(), rechargeSprite = rechargeSprites != null && rechargeIndicatorRenderer != null ?
                System.Array.IndexOf(rechargeSprites, rechargeIndicatorRenderer.sprite) : -1 };
    }

    public void InitializeNetworkView(BattleRevengeSystem ownerSystem, MovementController source)
    {
        networkView = true; system = ownerSystem;
        EnsureVisualRoot();
        CacheCartSpriteRenderers();
        ConfigureHeadVisuals(source);
        EnsureRechargeSpritesLoaded();
        CacheNetworkSprites();
        foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (var body in GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (var animation in networkAnimations)
            if (animation != null) animation.SetManualAnimationUpdate(true);
        enabled = true; // Only the native HUD overlay LateUpdate runs; Update has an authority gate.
    }

    public bool RestoreNetworkMotor(RevengeCartState state, bool applyTransform = true)
    {
        var g = state.geometry;
        if (g == null || g.Length != 10 || !float.IsFinite(state.perimeter)) return false;
        foreach (float value in g) if (!float.IsFinite(value)) return false;
        horizontalWallMinX = g[0]; horizontalWallMaxX = g[1]; verticalWallMinY = g[2]; verticalWallMaxY = g[3];
        topWallY = g[4]; bottomWallY = g[5]; leftWallX = g[6]; rightWallX = g[7];
        moveSpeed = Mathf.Clamp(g[8], 0, 100); actionCSpeedMultiplier = Mathf.Clamp(g[9], 1, 10);
        boundsConfigured = true; ownerPlayerId = state.owner;
        spawnPerimeterPosition = state.spawnPerimeter; suppressCornerVisualAtSpawn = state.suppressSpawnCorner;
        perimeterPosition = NormalizePerimeterPosition(state.perimeter);
        if (applyTransform) ApplyTransformFromPerimeterPosition();
        return true;
    }

    // Replay is motor-only: it cannot update charge, play audio or launch a bomb.
    public float PredictNetworkPerimeter(float origin, int held, float deltaTime)
    {
        if (!float.IsFinite(origin) || !float.IsFinite(deltaTime) || deltaTime <= 0) return origin;
        float saved = perimeterPosition;
        perimeterPosition = NormalizePerimeterPosition(origin);
        try
        {
            if (!TryGetTargetWallFromMask(held, out var target)) return perimeterPosition;
            float delta = GetShortestPerimeterDelta(perimeterPosition, GetWallMidpointPerimeterPosition(target));
            float step = moveSpeed * ((held & 64) != 0 ? Mathf.Max(1, actionCSpeedMultiplier) : 1) * deltaTime;
            return NormalizePerimeterPosition(perimeterPosition + Mathf.Clamp(delta, -step, step));
        }
        finally { perimeterPosition = saved; }
    }

    public void SetNetworkPerimeter(float value)
    {
        perimeterPosition = NormalizePerimeterPosition(value);
        ApplyTransformFromPerimeterPosition();
    }

    public float InterpolateNetworkPerimeter(float from, float to, float t) =>
        NormalizePerimeterPosition(from + GetShortestPerimeterDelta(from, to) * Mathf.Clamp01(t));

    public void PresentNetworkCart(RevengeCartState from, RevengeCartState to, float t)
    {
        if (!RestoreNetworkMotor(from)) return;
        if (!from.animating && !to.animating)
            SetNetworkPerimeter(InterpolateNetworkPerimeter(from.perimeter, to.perimeter, t));
        else transform.position = Vector3.Lerp(from.position, to.position, Mathf.Clamp01(t));
        CacheNetworkSprites();
        if (visualRoot != null) visualRoot.localPosition = Vector3.Lerp(from.recoilOffset, to.recoilOffset, Mathf.Clamp01(t));
        foreach (var sprite in networkSprites) if (sprite != null) sprite.enabled = false;
        foreach (var item in from.sprites ?? System.Array.Empty<RevengeCartSprite>())
        {
            if (item.index >= networkSprites.Length) continue;
            var sprite = networkSprites[item.index]; if (sprite == null) continue;
            var frame = item.frame;
            foreach (var next in to.sprites ?? System.Array.Empty<RevengeCartSprite>())
                if (next.index == item.index) { frame.pose = NetworkVisualPose.Interpolate(frame.pose, next.frame.pose, t); break; }
            if (!sprite.gameObject.activeSelf) sprite.gameObject.SetActive(true);
            var animation = networkAnimations[item.index];
            if (animation != null) { animation.idle = frame.idle; animation.CurrentFrame = frame.frame; animation.RefreshFrame(); }
            sprite.enabled = true; sprite.sortingLayerID = item.sortingLayer; sprite.sortingOrder = item.sortingOrder; sprite.color = frame.color; sprite.flipX = frame.flipX; sprite.flipY = frame.flipY;
            frame.pose.Apply(transform, sprite);
            if (sprite == landingIndicatorRenderer) sprite.transform.position = from.landingPosition;
            if (sprite == rechargeIndicatorRenderer && rechargeSprites != null && from.rechargeSprite >= 0 && from.rechargeSprite < rechargeSprites.Length)
                sprite.sprite = rechargeSprites[from.rechargeSprite];
        }
    }

    public void PresentPausedNetworkRecoil(Vector3 offset)
    {
        var landing = CaptureApprovedLandingPosition();
        if (visualRoot != null) visualRoot.localPosition = offset;
        RestoreApprovedLandingPosition(landing);
    }

    public Vector3? CaptureApprovedLandingPosition() => landingIndicatorRenderer != null && landingIndicatorRenderer.enabled
        ? landingIndicatorRenderer.transform.position : null;

    public void RestoreApprovedLandingPosition(Vector3? position)
    {
        if (position.HasValue && landingIndicatorRenderer != null) landingIndicatorRenderer.transform.position = position.Value;
    }

    public void PresentNetworkChargePreview(RevengeCartState state, RevengeChargePreview preview, bool held)
    {
        if (!networkView || NetSync.Mode != NetSync.NetMode.Client || GamePauseController.IsPaused) return;
        // Release hides an older buffered marker immediately; no launch or gameplay is predicted.
        if (!held) { HideLandingIndicator(); return; }
        if (!preview.Active || state.chargeTargets == null || state.chargeTargets.Length != 5 || landingIndicatorRenderer == null) return;
        var target = state.chargeTargets[Mathf.Clamp(preview.Distance, 3, 7) - 3];
        if (target.z <= 0 || !float.IsFinite(target.x) || !float.IsFinite(target.y)) { HideLandingIndicator(); return; }
        landingIndicatorRenderer.gameObject.SetActive(true);
        landingIndicatorRenderer.transform.position = new Vector3(target.x, target.y, landingIndicatorRenderer.transform.position.z);
        var color = landingIndicatorRenderer.color;
        color.a = Mathf.Lerp(indicatorMinAlpha, indicatorMaxAlpha, Mathf.PingPong(Time.unscaledTime * indicatorBlinkSpeed, 1));
        landingIndicatorRenderer.color = color; landingIndicatorRenderer.enabled = true;
    }

    public void PlayConfirmedNetworkLaunchSound()
    {
        if (NetSync.Mode == NetSync.NetMode.Client && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(launchBombSfx, launchBombSfxVolume);
    }
}
