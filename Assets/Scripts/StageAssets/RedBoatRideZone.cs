using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class RedBoatRideZone : MonoBehaviour
{
    [Header("Boat Visuals (children)")]
    [SerializeField] private AnimatedSpriteRenderer down;
    [SerializeField] private AnimatedSpriteRenderer up;
    [SerializeField] private AnimatedSpriteRenderer left;
    [SerializeField] private AnimatedSpriteRenderer right;

    [Header("Mount Settings")]
    [SerializeField] private bool hidePlayerWhileRiding = true;

    [Header("Water Pass (by Tag)")]
    [SerializeField] private bool allowPassOnWaterWhileRiding = true;
    [SerializeField] private string waterTag = "Water";

    [Header("Follow")]
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Header("Mount/Unmount Safety")]
    [SerializeField, Min(0f)] private float remountBlockSeconds = 0.15f;

    private MovementController rider;
    private Rigidbody2D riderRb;
    private float boatZ;

    private readonly List<Collider2D> riderColliders = new();
    private readonly List<Collider2D> waterColliders = new();
    private bool waterIgnoredApplied;

    private AnimatedSpriteRenderer currentVisual;
    private bool currentIdle = true;

    private float nextAllowedMountTime;
    private int lastUnmountFrame = -999;

    private MovementController remountBlockedRider;
    private bool blockRemountUntilExit;

    private void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        boatZ = transform.position.z;

        currentVisual = down;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);
    }

    private void LateUpdate()
    {
        if (rider == null || riderRb == null)
            return;

        Vector2 p = riderRb.position + followOffset;
        transform.position = new Vector3(p.x, p.y, boatZ);

        UpdateBoatVisualFromRider();
    }

    private void OnDisable()
    {
        if (rider != null)
            ForceUnmount();
    }

    public bool HasRider => rider != null;
    public bool IsRider(MovementController mc) => rider != null && rider == mc;

    public bool IsRemountBlockedFor(MovementController mc)
    {
        if (mc == null) return false;
        return blockRemountUntilExit && remountBlockedRider == mc;
    }

    public void ClearRemountBlock(MovementController mc)
    {
        if (mc == null) return;
        if (!blockRemountUntilExit) return;
        if (remountBlockedRider != mc) return;

        blockRemountUntilExit = false;
        remountBlockedRider = null;
    }

    public bool CanMount(MovementController mc)
    {
        if (mc == null) return false;
        if (HasRider) return false;
        if (mc.isDead) return false;
        if (!mc.CompareTag("Player")) return false;

        if (IsRemountBlockedFor(mc)) return false;

        if (Time.frameCount == lastUnmountFrame) return false;
        if (Time.time < nextAllowedMountTime) return false;

        return true;
    }

    public bool TryMount(MovementController mc)
    {
        if (!CanMount(mc))
            return false;

        rider = mc;
        riderRb = mc.Rigidbody;

        boatZ = transform.position.z;

        Vector2 p = riderRb != null ? riderRb.position : (Vector2)rider.transform.position;
        transform.position = new Vector3(p.x + followOffset.x, p.y + followOffset.y, boatZ);

        if (hidePlayerWhileRiding)
            rider.SetAllSpritesVisible(false);

        if (allowPassOnWaterWhileRiding)
        {
            rider.SetPassTaggedObstacles(true, waterTag);
            ApplyIgnoreWater(true);
        }

        currentVisual = down;
        currentIdle = true;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);

        return true;
    }

    public bool TryUnmount(MovementController mc)
    {
        if (mc == null) return false;
        if (rider == null) return false;
        if (mc != rider) return false;

        ForceUnmount();

        remountBlockedRider = mc;
        blockRemountUntilExit = true;

        lastUnmountFrame = Time.frameCount;
        nextAllowedMountTime = Time.time + remountBlockSeconds;

        return true;
    }

    private void ForceUnmount()
    {
        if (rider == null) return;

        if (allowPassOnWaterWhileRiding)
        {
            ApplyIgnoreWater(false);
            rider.SetPassTaggedObstacles(false, waterTag);
        }

        if (hidePlayerWhileRiding)
        {
            rider.SetAllSpritesVisible(true);
            rider.EnableExclusiveFromState();
        }

        rider = null;
        riderRb = null;

        currentVisual = down;
        currentIdle = true;
        ForceOnly(currentVisual);
        ApplyIdleToAll(true);
    }

    private void UpdateBoatVisualFromRider()
    {
        Vector2 moveDir = rider.Direction;
        Vector2 faceDir = rider.FacingDirection;

        bool isMoving = moveDir != Vector2.zero;
        Vector2 dirToUse = isMoving ? moveDir : faceDir;

        var target = PickBoatRenderer(dirToUse);
        if (target == null) target = down;

        bool wantIdle = !isMoving;

        if (target != currentVisual)
        {
            currentVisual = target;
            ForceOnly(currentVisual);
        }

        if (wantIdle != currentIdle)
        {
            currentIdle = wantIdle;
            ApplyIdleToAll(true);
            SetIdle(currentVisual, currentIdle);
        }
        else
        {
            SetIdle(currentVisual, currentIdle);
        }
    }

    private AnimatedSpriteRenderer PickBoatRenderer(Vector2 dir)
    {
        if (dir == Vector2.up) return up != null ? up : down;
        if (dir == Vector2.down) return down;
        if (dir == Vector2.left) return left != null ? left : down;
        if (dir == Vector2.right) return right != null ? right : down;
        return down;
    }

    private void ApplyIgnoreWater(bool ignore)
    {
        if (rider == null) return;

        if (ignore)
        {
            if (waterIgnoredApplied) return;

            riderColliders.Clear();
            waterColliders.Clear();

            rider.GetComponentsInChildren(true, riderColliders);

            var waterGos = GameObject.FindGameObjectsWithTag(waterTag);
            for (int i = 0; i < waterGos.Length; i++)
            {
                if (waterGos[i] == null) continue;

                if (waterGos[i].TryGetComponent<Collider2D>(out var wc)) waterColliders.Add(wc);

                var childColliders = waterGos[i].GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < childColliders.Length; c++)
                {
                    var cc = childColliders[c];
                    if (cc != null && !waterColliders.Contains(cc))
                        waterColliders.Add(cc);
                }
            }

            for (int i = 0; i < riderColliders.Count; i++)
            {
                var rc = riderColliders[i];
                if (rc == null) continue;

                for (int j = 0; j < waterColliders.Count; j++)
                {
                    var wc = waterColliders[j];
                    if (wc == null) continue;

                    Physics2D.IgnoreCollision(rc, wc, true);
                }
            }

            waterIgnoredApplied = true;
        }
        else
        {
            if (!waterIgnoredApplied) return;

            for (int i = 0; i < riderColliders.Count; i++)
            {
                var rc = riderColliders[i];
                if (rc == null) continue;

                for (int j = 0; j < waterColliders.Count; j++)
                {
                    var wc = waterColliders[j];
                    if (wc == null) continue;

                    Physics2D.IgnoreCollision(rc, wc, false);
                }
            }

            riderColliders.Clear();
            waterColliders.Clear();
            waterIgnoredApplied = false;
        }
    }

    private void ForceOnly(AnimatedSpriteRenderer target)
    {
        SetEnabled(down, target == down);
        SetEnabled(up, target == up);
        SetEnabled(left, target == left);
        SetEnabled(right, target == right);
    }

    private void ApplyIdleToAll(bool idle)
    {
        SetIdle(down, idle);
        SetIdle(up, idle);
        SetIdle(left, idle);
        SetIdle(right, idle);
    }

    private static void SetEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;
        r.enabled = on;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    private static void SetIdle(AnimatedSpriteRenderer r, bool idle)
    {
        if (r == null) return;
        r.idle = idle;
        r.RefreshFrame();
    }
}
