using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StageAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class World3HallPortal : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Stage_3-1";
        private bool transitioning;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transitioning || GamePauseController.IsPaused || !other.CompareTag("Player") ||
                !other.TryGetComponent<MovementController>(out var player) ||
                player.isDead || player.IsEndingStage || player.InputLocked)
                return;

            int destinationIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{destinationScene}.unity");
            if (destinationIndex < 0 || !Application.CanStreamedLevelBeLoaded(destinationIndex))
            {
                Debug.LogError($"Portal destination '{destinationScene}' is unavailable.", this);
                return;
            }

            foreach (var portal in FindObjectsByType<World3HallPortal>())
                portal.transitioning = true;

            foreach (var movement in FindObjectsByType<MovementController>())
            {
                if (!movement.CompareTag("Player") || movement.isDead)
                    continue;

                movement.SetInputLocked(true, true);
                PlayerPersistentStats.StageCaptureFromRuntime(movement, movement.GetComponent<BombController>());
            }

            PlayerPersistentStats.CommitStage();
            StartCoroutine(EnterStage(destinationIndex));
        }

        private IEnumerator EnterStage(int destinationIndex)
        {
            const float fadeDuration = 0.5f;
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
