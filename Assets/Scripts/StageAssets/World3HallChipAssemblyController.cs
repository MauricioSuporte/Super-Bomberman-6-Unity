using System.Collections;
using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class World3HallChipAssemblyController : MonoBehaviour
    {
        private static string pendingRevealStage;
        private static Assets.Scripts.SaveSystem.StageSlot pendingRevealSlot;

        public static bool HasPendingReveal => pendingRevealStage != null && pendingRevealSlot == SaveSystem.ActiveSlot;

        public static string PendingRevealStage => HasPendingReveal ? pendingRevealStage : null;

        public static bool IsChipStage(string sceneName) =>
            sceneName != null && sceneName.Length == 9 && sceneName.StartsWith("Stage_3-") &&
            sceneName[8] >= '1' && sceneName[8] <= '7';

        public static void PrepareReturn(string sceneName, bool firstClear)
        {
            pendingRevealStage = firstClear ? sceneName : null;
            pendingRevealSlot = firstClear ? SaveSystem.ActiveSlot : null;
        }

        [SerializeField] private SpriteRenderer[] parts = new SpriteRenderer[7];
        [SerializeField] private AudioClip revealSfx;
        [SerializeField, Range(0f, 1f)] private float revealSfxVolume = 1f;
        [SerializeField, Min(0.01f)] private float blinkInterval = 0.5f;

        [SerializeField] private World3HallPortalSelectionController selection;

        private string selectedStage;
        private float elapsed;
        private bool pendingVisible;
        private int revealingPart = -1;

        private void Awake()
        {
            if (HasPendingReveal)
            {
                revealingPart = pendingRevealStage[8] - '1';
                SetAlpha(parts[revealingPart], 0f);
            }
        }

        private void OnEnable()
        {
            elapsed = 0f;
            pendingVisible = true;
            RefreshParts();
        }

        private IEnumerator Start()
        {
            pendingRevealStage = null;
            pendingRevealSlot = null;
            RefreshParts();
            if (revealingPart < 0)
                yield break;

            if (selection != null)
                selection.PresentationBlocked = true;
            // StageIntroTransition sets IntroRunning in Start. Wait until every
            // scene Start has run before checking it, otherwise the effect can
            // be created during fade-in and remain visible at timeScale zero.
            yield return null;
            while (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning)
                yield return null;

            World3EndStageCelebrationEffect.Play(transform.position, transform, reverse: true);
            yield return new WaitForSeconds(World3EndStageCelebrationEffect.Duration);

            SpriteRenderer part = parts[revealingPart];
            // Six cycles in half the time previously used by three cycles.
            var revealBlinkWait = new WaitForSeconds(blinkInterval * 0.25f);
            for (int i = 0; i < 6; i++)
            {
                SetAlpha(part, 1f);
                yield return revealBlinkWait;
                SetAlpha(part, 0f);
                yield return revealBlinkWait;
            }

            if (GameMusicController.Instance != null)
                GameMusicController.Instance.PlaySfx(revealSfx, revealSfxVolume);
            const float fadeDuration = 1.25f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                SetAlpha(part, Mathf.Clamp01(fadeElapsed / fadeDuration));
                yield return null;
                fadeElapsed += Time.deltaTime;
            }
            SetAlpha(part, 1f);
            revealingPart = -1;
            RefreshParts();
            if (selection != null)
                yield return selection.RevealUnlockedPortals();
            if (selection != null)
            {
                selection.PresentationBlocked = false;
            }
            RefreshParts();
        }

        private static void SetAlpha(SpriteRenderer part, float alpha)
        {
            if (part == null)
                return;
            Color color = part.color;
            color.a = alpha;
            part.color = color;
        }

        private void LateUpdate()
        {
            string currentStage = selection != null ? selection.SelectedStageSceneName : null;
            if (selectedStage != currentStage)
            {
                selectedStage = currentStage;
                elapsed = 0f;
                pendingVisible = true;
            }
            else
            {
                float selectionBlinkInterval = blinkInterval * 0.5f;
                elapsed += Time.deltaTime;
                if (elapsed >= selectionBlinkInterval)
                {
                    int steps = Mathf.FloorToInt(elapsed / selectionBlinkInterval);
                    elapsed -= steps * selectionBlinkInterval;
                    if ((steps & 1) != 0)
                        pendingVisible = !pendingVisible;
                }
            }

            RefreshParts();
        }

        private void RefreshParts()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || i == revealingPart)
                    continue;

                // The outgoing hall already shares the updated save. Keep its
                // newly earned part hidden until the reloaded hall reveals it.
                if (revealingPart < 0 && PendingRevealStage == $"Stage_3-{i + 1}")
                {
                    SetAlpha(parts[i], 0f);
                    continue;
                }

                bool cleared = StageUnlockProgress.IsCleared($"Stage_3-{i + 1}");
                Color color = parts[i].color;
                color.a = cleared || (selectedStage == $"Stage_3-{i + 1}" && pendingVisible) ? 1f : 0f;
                parts[i].color = color;
            }
        }
    }
}
