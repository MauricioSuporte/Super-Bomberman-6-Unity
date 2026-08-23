using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// Moves a decorative Stage 3-3 fish between two points local to its parent.
    /// The supplied sprites face left, so the renderer is mirrored while
    /// travelling right.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage33PatrolFish : MonoBehaviour
    {
        [Header("Patrol")]
        [SerializeField] private Vector2 leftPoint;
        [SerializeField] private Vector2 rightPoint = Vector2.right;
        [SerializeField, Min(0.01f)] private float speed = 1f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;
        [SerializeField, Min(0.1f)] private float debugLogIntervalSeconds = 0.5f;

        private Vector2 target;
        private float nextDebugLogTime;
        private Vector3 lastAppliedLocalPosition;
        private bool hasAppliedPosition;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            transform.localPosition = new Vector3(leftPoint.x, leftPoint.y, transform.localPosition.z);
            SetTarget(rightPoint);
            LogDebug($"enabled | parent={transform.parent?.name ?? "<none>"} | " +
                $"local={transform.localPosition} | world={transform.position} | " +
                $"left={leftPoint} | right={rightPoint} | speed={speed}");
        }

        private void Update()
        {
            Vector2 current = transform.localPosition;
            Vector2 next = Vector2.MoveTowards(current, target, speed * Time.unscaledDeltaTime);
            transform.localPosition = new Vector3(next.x, next.y, transform.localPosition.z);
            lastAppliedLocalPosition = transform.localPosition;
            hasAppliedPosition = true;

            LogDebug($"moving | before={current} | after={next} | target={target} | " +
                $"distance={Vector2.Distance(next, target):F3} | dt={Time.unscaledDeltaTime:F4}");

            if ((next - target).sqrMagnitude <= 0.000001f)
            {
                SetTarget(target == rightPoint ? leftPoint : rightPoint);
                LogDebug($"reversed | newTarget={target}", force: true);
            }
        }

        private void LateUpdate()
        {
            if (!debugLogging || !hasAppliedPosition)
                return;

            if ((transform.localPosition - lastAppliedLocalPosition).sqrMagnitude > 0.000001f)
            {
                Debug.LogWarning(
                    $"[Stage33PatrolFish] {name} position was overwritten after Update | " +
                    $"applied={lastAppliedLocalPosition} | now={transform.localPosition}",
                    this);
                lastAppliedLocalPosition = transform.localPosition;
            }
        }

        private void SetTarget(Vector2 nextTarget)
        {
            target = nextTarget;

            if (spriteRenderer != null)
                spriteRenderer.flipX = target.x > transform.localPosition.x;
        }

        private void LogDebug(string message, bool force = false)
        {
            if (!debugLogging)
                return;

            if (!force && Time.unscaledTime < nextDebugLogTime)
                return;

            nextDebugLogTime = Time.unscaledTime + debugLogIntervalSeconds;
            Debug.Log($"[Stage33PatrolFish] {name} {message}", this);
        }
    }
}
