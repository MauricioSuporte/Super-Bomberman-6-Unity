using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class World3HallExitCursorAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite cornerSprite;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.08f;

        private static readonly int[] ExpansionFrames = { 0, 1, 2, 1 };
        private readonly SpriteRenderer[] corners = new SpriteRenderer[4];
        private SpriteRenderer selectionRenderer;
        private float animationTime;
        private bool wasVisible;
        private Vector2 selectionSize = new(64f, 32f);

        public void SetSelectionSize(Vector2 size) => selectionSize = size;

        private void Awake()
        {
            selectionRenderer = GetComponent<SpriteRenderer>();
            for (int i = 0; i < corners.Length; i++)
            {
                var part = new GameObject("Corner" + i);
                part.transform.SetParent(transform, false);
                var renderer = part.AddComponent<SpriteRenderer>();
                renderer.sprite = cornerSprite;
                renderer.sortingLayerID = selectionRenderer.sortingLayerID;
                renderer.sortingOrder = selectionRenderer.sortingOrder;
                renderer.flipX = (i & 1) != 0;
                renderer.flipY = (i & 2) != 0;
                renderer.enabled = false;
                corners[i] = renderer;
            }
        }

        private void LateUpdate()
        {
            bool visible = selectionRenderer.enabled;
            if (!wasVisible && visible)
                animationTime = 0f;
            else if (visible)
                animationTime = (animationTime + Time.unscaledDeltaTime) % (Mathf.Max(0.01f, frameSeconds) * ExpansionFrames.Length);
            wasVisible = visible;

            int frame = Mathf.FloorToInt(animationTime / Mathf.Max(0.01f, frameSeconds));
            int expansion = ExpansionFrames[frame];
            for (int i = 0; i < corners.Length; i++)
            {
                var corner = corners[i];
                corner.enabled = visible;
                // Keep the same 14 px corner sprites for Exit and the completed chip (PPU 16).
                float x = ((i & 1) != 0 ? 1f : -1f) * (selectionSize.x * 0.5f - 7f + expansion) / 16f;
                float y = ((i & 2) != 0 ? 1f : -1f) * (selectionSize.y * 0.5f - 7f + expansion) / 16f;
                corner.transform.localPosition = new Vector3(x, y, 0f);
            }
        }
    }
}