using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// Connects the two open vertical edges in Stage 3-2, Room 1.
    /// The player preserves its horizontal position and movement overshoot when
    /// crossing either opening, so walking out of one edge continues at the other.
    /// </summary>
    public sealed class Stage32Room1VerticalWrap : MonoBehaviour
    {
        private static Stage32Room1VerticalWrap activeInstance;

        [Header("Room 1 openings")]
        [SerializeField] private Vector2 bottomOpeningCell = new(5f, -7f);
        [SerializeField] private Vector2 topOpeningCell = new(5f, 5f);
        [SerializeField, Min(0.1f)] private float openingWidth = 1f;

        private void Awake() => activeInstance = this;

        private void OnDestroy()
        {
            if (activeInstance == this)
                activeInstance = null;
        }

        /// <summary>
        /// Maps one explosion step through Room 1's vertical seam. This is
        /// intentionally limited to the two opening cells, not the whole edge.
        /// </summary>
        public static bool TryWrapExplosionStep(
            Vector2 from,
            Vector2 direction,
            out Vector2 wrappedPosition)
        {
            if (activeInstance == null)
            {
                wrappedPosition = default;
                return false;
            }

            return activeInstance.TryWrapExplosionStepInternal(from, direction, out wrappedPosition);
        }

        /// <summary>Maps one kicked or thrown bomb step through Room 1's seam.</summary>
        public static bool TryWrapBombStep(
            Vector2 from,
            Vector2 direction,
            out Vector2 wrappedPosition)
        {
            if (activeInstance == null)
            {
                wrappedPosition = default;
                return false;
            }

            return activeInstance.TryWrapExplosionStepInternal(from, direction, out wrappedPosition);
        }

        private void LateUpdate()
        {
            MovementController[] players =
                FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);

            for (int i = 0; i < players.Length; i++)
                WrapPlayerIfNeeded(players[i]);
        }

        private void WrapPlayerIfNeeded(MovementController player)
        {
            if (player == null || player.isDead || !player.CompareTag("Player"))
                return;

            Rigidbody2D body = player.Rigidbody != null
                ? player.Rigidbody
                : player.GetComponent<Rigidbody2D>();

            if (body == null)
                return;

            Vector2 position = body.position;
            if (Mathf.Abs(position.x - topOpeningCell.x) > openingWidth * 0.5f)
                return;

            float topBoundary = topOpeningCell.y + 0.5f;
            float bottomBoundary = bottomOpeningCell.y - 0.5f;
            float span = topBoundary - bottomBoundary;
            if (span <= 0f)
                return;

            if (position.y > topBoundary)
                position.y -= span;
            else if (position.y < bottomBoundary)
                position.y += span;
            else
                return;

            // Assigning the Rigidbody position keeps the collider and rendered
            // character synchronized immediately after crossing the seam.
            body.position = position;
        }

        private bool TryWrapExplosionStepInternal(
            Vector2 from,
            Vector2 direction,
            out Vector2 wrappedPosition)
        {
            wrappedPosition = default;

            if (Mathf.Abs(from.x - topOpeningCell.x) > 0.01f ||
                Mathf.Abs(direction.x) > 0.01f)
            {
                return false;
            }

            if (direction.y > 0.01f && Mathf.Abs(from.y - topOpeningCell.y) <= 0.01f)
            {
                wrappedPosition = bottomOpeningCell;
                return true;
            }

            if (direction.y < -0.01f && Mathf.Abs(from.y - bottomOpeningCell.y) <= 0.01f)
            {
                wrappedPosition = topOpeningCell;
                return true;
            }

            return false;
        }
    }
}
