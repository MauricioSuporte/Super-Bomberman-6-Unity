using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StageAssets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class World3HallPortalSelectionController : MonoBehaviour
    {
        public const string SceneName = "Stage_World3Hall";
        private static string focusedStageSceneName;

        public static void FocusStageOnNextLoad(string sceneName)
        {
            focusedStageSceneName = sceneName;
        }

        [SerializeField] private World3HallPortal[] portals;
        [SerializeField] private SpriteRenderer cursor;
        [SerializeField] private SpriteRenderer exitCursor;
        [SerializeField] private AudioClip returnSfx;
        [SerializeField] private Text selectedStageLabel;
        [SerializeField] private StagePreIntroPlayersWalk portalWalk;
        [SerializeField] private AudioClip confirmSfx;
        [SerializeField] private AudioClip cursorMoveSfx;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

        private AudioSource audioSource;
        private WorldMapStageLabelStyle selectedStageLabelStyle;
        private int selectedIndex;
        private bool transitioning;
        private bool exitSelected;
        private bool selectionReady;
        public bool PresentationBlocked { get; set; }

        public string SelectedStageSceneName =>
            selectionReady && !exitSelected && portals != null &&
            selectedIndex >= 0 && selectedIndex < portals.Length && portals[selectedIndex] != null
                ? portals[selectedIndex].DestinationScene
                : null;

        private void Awake()
        {
            PresentationBlocked = World3HallChipAssemblyController.HasPendingReveal;
            string initialStage = focusedStageSceneName;
            focusedStageSceneName = null;
            if (portals != null && !string.IsNullOrEmpty(initialStage))
            {
                for (int i = 0; i < portals.Length; i++)
                {
                    if (portals[i] != null && portals[i].DestinationScene == initialStage)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (cursor != null)
                cursor.enabled = false;
            if (exitCursor != null)
                exitCursor.enabled = false;
            if (selectedStageLabel != null)
            {
                selectedStageLabelStyle = selectedStageLabel.GetComponentInParent<WorldMapStageLabelStyle>();
                if (selectedStageLabelStyle != null)
                    selectedStageLabelStyle.gameObject.SetActive(false);
                else
                    selectedStageLabel.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (selectedStageLabel == null || !selectionReady || portals[selectedIndex] == null)
                return;

            if (exitSelected)
            {
                if (selectedStageLabelStyle != null)
                    selectedStageLabelStyle.gameObject.SetActive(false);
                else
                    selectedStageLabel.enabled = false;
                return;
            }

            string stage = portals[selectedIndex].DestinationScene.Replace("Stage_", "").Replace("-", " - ");
            LocalizedTmpFontFallback.Apply(selectedStageLabel);
            selectedStageLabel.text = GameTextDatabase.WorldMap.WorldPrefix + stage;
            selectedStageLabel.enabled = true;

            if (selectedStageLabelStyle != null)
            {
                selectedStageLabelStyle.gameObject.SetActive(true);
                selectedStageLabelStyle.ApplyStyle();
            }
        }

        private void Update()
        {
            if (PresentationBlocked || transitioning || GamePauseController.IsPaused || portals == null || portals.Length == 0)
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

            if (input.GetDown(1, PlayerAction.ActionB))
            {
                ReturnToWorldMap();
                return;
            }

            bool previousExitSelected = exitSelected;
            int previousIndex = selectedIndex;
            if (input.GetDown(1, PlayerAction.MoveDown) && exitCursor != null)
                exitSelected = true;
            else if (input.GetDown(1, PlayerAction.MoveUp))
                exitSelected = false;
            else if (!exitSelected && input.GetDown(1, PlayerAction.MoveLeft))
                selectedIndex = (selectedIndex + portals.Length - 1) % portals.Length;
            else if (!exitSelected && input.GetDown(1, PlayerAction.MoveRight))
                selectedIndex = (selectedIndex + 1) % portals.Length;

            if ((selectedIndex != previousIndex || exitSelected != previousExitSelected) && cursorMoveSfx != null)
                GameAudioSettings.PlaySfx(audioSource, cursorMoveSfx);

            RefreshCursor();

            if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
            {
                if (exitSelected)
                    ReturnToWorldMap();
                else
                    ConfirmSelection();
            }
        }

        private void RefreshCursor()
        {
            if (exitCursor != null)
                exitCursor.enabled = exitSelected;
            if (exitSelected)
            {
                if (cursor != null)
                    cursor.enabled = false;
                return;
            }

            if (cursor == null || portals[selectedIndex] == null)
                return;

            cursor.transform.position = portals[selectedIndex].transform.position;
            cursor.enabled = true;
        }

        private void ReturnToWorldMap()
        {
            if (transitioning)
                return;

            transitioning = true;
            if (returnSfx != null)
                GameAudioSettings.PlaySfx(audioSource, returnSfx);
            StartCoroutine(ReturnToWorldMapRoutine());
        }

        private IEnumerator ReturnToWorldMapRoutine()
        {
            if (StageIntroTransition.Instance != null)
                StageIntroTransition.Instance.StartFadeOut(fadeDuration);
            yield return new WaitForSecondsRealtime(fadeDuration);
            if (GameMusicController.Instance != null)
                GameMusicController.Instance.StopMusic();
            SceneManager.LoadSceneAsync("WorldMap");
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
