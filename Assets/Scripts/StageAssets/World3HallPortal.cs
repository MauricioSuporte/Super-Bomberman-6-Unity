using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class World3HallPortal : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Stage_3-1";

        public string DestinationScene => destinationScene;
        public const float RevealDuration = 2.5f;
        public bool Available { get; private set; }
        private SpriteRenderer visual;
        private GameObject revealObject;
        private Texture2D beamTexture;
        private Sprite beamSprite;

        public bool UnlockedBeforeClear(string stage)
        {
            if (destinationScene == "Stage_3-6")
            {
                for (int i = 1; i <= 5; i++)
                    if (stage == $"Stage_3-{i}" || !StageUnlockProgress.IsCleared($"Stage_3-{i}"))
                        return false;
                return true;
            }
            return destinationScene != "Stage_3-7" ||
                (stage != "Stage_3-6" && StageUnlockProgress.IsCleared("Stage_3-6"));
        }

        public void SetAvailable(bool available)
        {
            if (visual == null)
                visual = GetComponentInChildren<SpriteRenderer>(true);
            Available = available;
            if (visual != null)
                visual.forceRenderingOff = !available;
        }

        public IEnumerator Reveal(AudioClip unlockSfx, bool reverse = false)
        {
            if (visual == null || visual.sprite == null)
            {
                SetAvailable(!reverse);
                yield break;
            }

            // Build in world pixels so the beam remains aligned with the PPU 16 viewport.
            Bounds bounds = GetPortalPixelBounds();
            int width = Mathf.Max(5, Mathf.CeilToInt(bounds.size.x * 16f));
            int height = Mathf.Max(6, Mathf.CeilToInt(bounds.size.y * 16f) * 2);
            float beamTop = bounds.min.y + height / 16f;
            beamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            beamTexture.filterMode = FilterMode.Point;
            beamTexture.wrapMode = TextureWrapMode.Clamp;
            beamSprite = Sprite.Create(beamTexture, new Rect(0, 0, width, height), Vector2.one * 0.5f, 16f);
            revealObject = new GameObject("Portal unlock beam");
            revealObject.transform.SetParent(transform, true);
            revealObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + height / 32f, bounds.center.z);
            var beam = revealObject.AddComponent<SpriteRenderer>();
            beam.sprite = beamSprite;
            beam.sharedMaterial = visual.sharedMaterial;
            beam.sortingLayerID = visual.sortingLayerID;
            beam.sortingOrder = visual.sortingOrder + 1;
            var beamProperties = new MaterialPropertyBlock();
            beamProperties.SetFloat("_BeamMaskEnabled", 1f);
            beamProperties.SetFloat("_RevealMode", 2f);
            float baseMaskHeight = Mathf.Ceil(width * 0.25f) / 16f;
            beamProperties.SetFloat("_PortalBaseMaskHeight", baseMaskHeight);
            beamProperties.SetVector("_RenderWorldRect", new Vector4(bounds.center.x - width / 32f,
                bounds.min.y, width / 16f, height / 16f));
            beamProperties.SetVector("_RenderUVRect", new Vector4(0f, 0f, 1f, 1f));

            AudioSource unlockAudio = null;
            if (unlockSfx != null)
            {
                if (unlockSfx.loadState == AudioDataLoadState.Unloaded)
                    unlockSfx.LoadAudioData();
                while (unlockSfx.loadState == AudioDataLoadState.Loading)
                    yield return null;
                unlockAudio = revealObject.AddComponent<AudioSource>();
                unlockAudio.playOnAwake = false;
                unlockAudio.spatialBlend = 0f;
                // Fit the entire attached sound, including its tail, into the reveal.
                unlockAudio.pitch = unlockSfx.length / RevealDuration;
                GameAudioSettings.PlaySfxClip(unlockAudio, unlockSfx);
            }

            var properties = new MaterialPropertyBlock();
            visual.GetPropertyBlock(properties);
            var originalProperties = new MaterialPropertyBlock();
            visual.GetPropertyBlock(originalProperties);
            properties.SetFloat("_RevealMode", 1f);
            properties.SetVector("_PortalWorldRect", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));

            var pixels = new Color32[width * height];
            float elapsed = 0f;
            bool audioPaused = false;
            while (elapsed < RevealDuration)
            {
                // 0.5s fade, 0.75s widening, 0.5s hold, 0.75s downward reveal.
                float animationTime = reverse ? RevealDuration - elapsed : elapsed;
                float expansion = Mathf.Clamp01((animationTime - 0.5f) / 0.75f);
                int removedRows = Mathf.FloorToInt(height * Mathf.Clamp01((animationTime - 1.75f) / 0.75f));
                for (int y = 0; y < height; y++)
                {
                    // Only the footprint copies portal alpha; the shaft stays solid.
                    int narrowWidth = y == 0 ? 1 : y == 1 ? 3 : y < 4 ? 5 : 3;
                    int rowWidth = Mathf.RoundToInt(Mathf.Lerp(narrowWidth, width, expansion));
                    int left = (width - rowWidth) / 2;
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = x >= left && x < left + rowWidth
                            ? new Color32(255, 0, 255, 255) : new Color32(0, 0, 0, 0);
                }
                beamTexture.SetPixels32(pixels);
                beamTexture.Apply();
                Color color = visual.color;
                color.a = 0.75f * Mathf.Clamp01(animationTime / 0.5f);
                beam.color = color;
                beamProperties.SetFloat("_RevealY", beamTop - removedRows / 16f);
                UpdateBeamPixelMask(beam, beamProperties, properties);
                properties.SetFloat("_RevealY", beamTop - removedRows / 16f);
                visual.SetPropertyBlock(properties);
                visual.forceRenderingOff = beamTop - removedRows / 16f >= bounds.max.y;
                if (unlockAudio != null && audioPaused != GamePauseController.IsPaused)
                {
                    audioPaused = GamePauseController.IsPaused;
                    if (audioPaused) unlockAudio.Pause();
                    else unlockAudio.UnPause();
                }
                yield return null;
                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;
            }

            // Destroy is deferred until the end of the frame: hide the beam first
            // so the final fully visible portal never overlaps its remaining rows.
            beam.enabled = false;
            visual.SetPropertyBlock(originalProperties);
            SetAvailable(!reverse);
            ClearReveal();
        }

        private Bounds GetPortalPixelBounds()
        {
            Sprite sprite = visual.sprite;
            Vector2 min = -sprite.pivot / sprite.pixelsPerUnit;
            Vector2 max = (sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit;
            Vector3 worldMin = visual.transform.TransformPoint(new Vector3(min.x, min.y, 0f));
            Vector3 worldMax = visual.transform.TransformPoint(new Vector3(max.x, max.y, 0f));
            return new Bounds((worldMin + worldMax) * 0.5f,
                new Vector3(Mathf.Abs(worldMax.x - worldMin.x), Mathf.Abs(worldMax.y - worldMin.y), 0f));
        }

        private void UpdateBeamPixelMask(SpriteRenderer beam, MaterialPropertyBlock properties, MaterialPropertyBlock portalProperties)
        {
            Sprite sprite = visual.sprite;
            Bounds bounds = GetPortalPixelBounds();
            Rect rect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Vector4 uvRect = new Vector4(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height);
            if (visual.flipX)
            {
                uvRect.x += uvRect.z;
                uvRect.z = -uvRect.z;
            }
            if (visual.flipY)
            {
                uvRect.y += uvRect.w;
                uvRect.w = -uvRect.w;
            }
            properties.SetTexture("_PortalMaskTex", texture);
            properties.SetVector("_PortalWorldRect", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            properties.SetVector("_PortalUVRect", uvRect);
            portalProperties.SetVector("_RenderWorldRect", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            portalProperties.SetVector("_RenderUVRect", uvRect);
            beam.SetPropertyBlock(properties);
        }

        private void ClearReveal()
        {
            if (revealObject != null) Destroy(revealObject);
            if (beamSprite != null) Destroy(beamSprite);
            if (beamTexture != null) Destroy(beamTexture);
        }

        private void OnDestroy() => ClearReveal();

        public int DestinationBuildIndex =>
            SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{destinationScene}.unity");
    }
}
