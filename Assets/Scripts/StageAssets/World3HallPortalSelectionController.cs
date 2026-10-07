using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StageAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class World3HallPortalSelectionController : MonoBehaviour
    {
        [SerializeField] private World3HallPortal[] portals;
        [SerializeField] private SpriteRenderer cursor;
        [SerializeField] private StagePreIntroPlayersWalk portalWalk;
        [SerializeField] private AudioClip confirmSfx;
        [SerializeField] private AudioClip cursorMoveSfx;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

        private AudioSource audioSource;
        private int selectedIndex;
        private bool transitioning;
        private bool selectionReady;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (cursor != null)
                cursor.enabled = false;
        }

        private void Update()
        {
            if (transitioning || GamePauseController.IsPaused || portals == null || portals.Length == 0)
                return;

            var intro = StageIntroTransition.Instance;
            if (intro != null && intro.IntroRunning)
                return;

            if (!selectionReady)
            {
                selectionReady = true;
                RefreshCursor();
                // Ignore the key that completed the intro in this frame.
                return;
            }

            var input = PlayerInputManager.Instance;
            if (input == null)
                return;

            int previousIndex = selectedIndex;
            if (input.GetDown(1, PlayerAction.MoveLeft))
                selectedIndex = (selectedIndex + portals.Length - 1) % portals.Length;
            else if (input.GetDown(1, PlayerAction.MoveRight))
                selectedIndex = (selectedIndex + 1) % portals.Length;

            if (selectedIndex != previousIndex && cursorMoveSfx != null)
                GameAudioSettings.PlaySfx(audioSource, cursorMoveSfx);

            RefreshCursor();

            if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
                ConfirmSelection();
        }

        private void RefreshCursor()
        {
            if (cursor == null || portals[selectedIndex] == null)
                return;

            cursor.transform.position = portals[selectedIndex].transform.position;
            cursor.enabled = true;
        }

        private void ConfirmSelection()
        {
            var portal = portals[selectedIndex];
            if (portal == null || portalWalk == null)
                return;

            int destinationIndex = portal.DestinationBuildIndex;
            if (destinationIndex < 0 || !Application.CanStreamedLevelBeLoaded(destinationIndex))
            {
                Debug.LogError($"Portal destination for '{portal.name}' is unavailable.", portal);
                return;
            }

            transitioning = true;
            if (confirmSfx != null)
                GameAudioSettings.PlaySfx(audioSource, confirmSfx);

            foreach (var movement in FindObjectsByType<MovementController>())
            {
                if (movement.CompareTag("Player") && !movement.isDead)
                    PlayerPersistentStats.StageCaptureFromRuntime(movement, movement.GetComponent<BombController>());
            }

            PlayerPersistentStats.CommitStage();
            StartCoroutine(EnterSelectedPortal(portal.transform.position, destinationIndex));
        }

        private IEnumerator EnterSelectedPortal(Vector2 portalCenter, int destinationIndex)
        {
            yield return portalWalk.PlayPortalEntry(portalCenter);

            if (cursor != null)
                cursor.enabled = false;

            if (StageIntroTransition.Instance != null)
                StageIntroTransition.Instance.StartFadeOut(fadeDuration);

            yield return new WaitForSecondsRealtime(fadeDuration);

            if (GameMusicController.Instance != null)
                GameMusicController.Instance.StopMusic();

            StagePreIntroPlayersWalk.SkipOnNextLoad();
            SceneManager.LoadSceneAsync(destinationIndex);
        }
    }
}
