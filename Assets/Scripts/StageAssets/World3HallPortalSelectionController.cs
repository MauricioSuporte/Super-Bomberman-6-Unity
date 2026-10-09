using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StageAssets
{
    [DefaultExecutionOrder(10000)]
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
        [SerializeField] private Transform chipAnchor;
        [SerializeField] private AudioClip returnSfx;
        [SerializeField] private Text selectedStageLabel;
        [SerializeField] private StagePreIntroPlayersWalk portalWalk;
        [SerializeField] private World3HallStageSevenSequence stageSevenSequence;
        [SerializeField] private AudioClip confirmSfx;
        [SerializeField] private AudioClip cursorMoveSfx;
        [SerializeField] private AudioClip portalUnlockSfx;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

        private AudioSource audioSource;
        private WorldMapStageLabelStyle selectedStageLabelStyle;
        private int selectedIndex;
        private bool transitioning;
        private bool exitSelected;
        private bool selectionReady;
        private Vector3 exitCursorPosition;
        private bool ChipSelected => portals != null && selectedIndex == portals.Length;
        private bool ChipAvailable
        {
            get
            {
                return chipAnchor != null && StageUnlockProgress.IsUnlocked("Stage_3-8");
            }
        }
        private MovementController[] players;
        public bool PresentationBlocked { get; set; }

        public string SelectedStageSceneName => !selectionReady || exitSelected ? null :
            ChipSelected ? "Stage_3-8" : portals != null && selectedIndex >= 0 &&
            selectedIndex < portals.Length && portals[selectedIndex] != null
                ? portals[selectedIndex].DestinationScene : null;

        public void FocusCompletedChip()
        {
            if (!ChipAvailable) return;
            selectedIndex = portals.Length;
            exitSelected = false;
            RefreshCursor();
        }

        private void Awake()
        {
            PresentationBlocked = World3HallChipAssemblyController.HasPendingReveal;
            if (exitCursor != null) exitCursorPosition = exitCursor.transform.position;
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

            if (portals != null && portals.Length > 0)
            {
                string pendingStage = World3HallChipAssemblyController.PendingRevealStage;
                foreach (var portal in portals)
                    if (portal != null)
                        portal.SetAvailable(StageUnlockProgress.IsUnlocked(portal.DestinationScene) &&
                            (pendingStage == null || portal.UnlockedBeforeClear(pendingStage)));
                if (portals[selectedIndex] == null || !portals[selectedIndex].Available)
                    for (int i = 0; i < portals.Length; i++)
                        if (portals[i] != null && portals[i].Available)
                        {
                            selectedIndex = i;
                            break;
                        }
            }

            if (initialStage == "Stage_3-8" && ChipAvailable)
                selectedIndex = portals.Length;

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (portalUnlockSfx == null)
                portalUnlockSfx = Resources.Load<AudioClip>("Sounds/Portal Unlock");
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
            KeepPlayersFacingUp();

            if (selectedStageLabel == null || !selectionReady || (!ChipSelected && portals[selectedIndex] == null))
                return;

            if (exitSelected)
            {
                if (selectedStageLabelStyle != null)
                    selectedStageLabelStyle.gameObject.SetActive(false);
                else
                    selectedStageLabel.enabled = false;
                return;
            }

            string stage = (ChipSelected ? "Stage_3-8" : portals[selectedIndex].DestinationScene).Replace("Stage_", "").Replace("-", " - ");
            LocalizedTmpFontFallback.Apply(selectedStageLabel);
            selectedStageLabel.text = GameTextDatabase.WorldMap.WorldPrefix + stage;
            selectedStageLabel.enabled = true;

            if (selectedStageLabelStyle != null)
            {
                selectedStageLabelStyle.gameObject.SetActive(true);
                selectedStageLabelStyle.ApplyStyle();
            }
        }

        private void KeepPlayersFacingUp()
        {
            if (transitioning)
                return;

            if (players == null || players.Length == 0)
                players = FindObjectsByType<MovementController>();

            foreach (var player in players)
            {
                if (player == null || !player.gameObject.activeInHierarchy || player.isDead ||
                    !player.CompareTag("Player") || player.Direction != Vector2.zero)
                    continue;

                // Intro controllers can be disabled while their sprites are visible.
                // Preserve hidden entrance/fade states instead of revealing them early.
                bool visible = false;
                foreach (var sprite in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sprite.enabled && sprite.gameObject.activeInHierarchy)
                    {
                        visible = true;
                        break;
                    }
                }
                if (visible)
                    player.ForceMountedUpExclusive();
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
            Vector2 direction = Vector2.zero;
            if (input.GetDown(1, PlayerAction.MoveDown)) direction = Vector2.down;
            else if (input.GetDown(1, PlayerAction.MoveUp)) direction = Vector2.up;
            else if (input.GetDown(1, PlayerAction.MoveLeft)) direction = Vector2.left;
            else if (input.GetDown(1, PlayerAction.MoveRight)) direction = Vector2.right;
            if (direction != Vector2.zero)
                MoveSelection(direction);

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

        // Clockwise portals around the central chip. Explicit neighbors keep
        // navigation stable when the large chip selection overlaps their rows.
        private static int Neighbor(int stage, Vector2 direction)
        {
            if (direction == Vector2.left)
                return stage switch { 1 => 6, 2 => 1, 3 => 2, 4 => 7, 5 => 8, 6 => 8, 7 => 3, 8 => 1, _ => 1 };
            if (direction == Vector2.right)
                return stage switch { 1 => 8, 2 => 8, 3 => 7, 4 => 5, 5 => 6, 6 => 1, 7 => 4, 8 => 6, _ => 6 };
            if (direction == Vector2.up)
                return stage switch { 1 => 2, 2 => 3, 3 => 0, 4 => 0, 5 => 4, 6 => 5, 7 => 0, 8 => 7, _ => 8 };
            return stage switch { 1 => 0, 2 => 1, 3 => 2, 4 => 5, 5 => 6, 6 => 0, 7 => 8, 8 => 0, _ => 7 };
        }

        private static int SequentialNeighbor(int stage, Vector2 direction)
        {
            // Horizontal navigation follows the upper portal between 3-3 and 3-4.
            if (direction == Vector2.right)
                return stage switch { 3 => 7, 7 => 4, 6 => 0, _ => stage + 1 };
            if (direction == Vector2.left)
                return stage switch { 4 => 7, 7 => 3, 0 => 6, _ => stage - 1 };
            int step = direction == Vector2.up ? 1 : -1;
            // Exit is zero; the seven stage choices wrap around it.
            return (stage + step + 8) % 8;
        }

        private void MoveSelection(Vector2 direction)
        {
            int current = exitSelected ? 0 : ChipSelected ? 8 :
                int.Parse(portals[selectedIndex].DestinationScene.Substring("Stage_3-".Length));
            if (exitSelected && (direction == Vector2.left || direction == Vector2.right))
            {
                int destination = direction == Vector2.left ? 1 : 6;
                for (; destination >= 1; destination--)
                {
                    for (int i = 0; i < portals.Length; i++)
                        if (portals[i] != null && portals[i].Available && portals[i].DestinationScene == $"Stage_3-{destination}")
                        {
                            exitSelected = false;
                            selectedIndex = i;
                            return;
                        }
                    if (direction == Vector2.left) break;
                }
                return;
            }
            int next = current;
            bool chipAvailable = ChipAvailable;
            // Skip unavailable stages in the same direction without selecting
            // a locked portal or getting stuck in a cycle.
            for (int attempt = 0; attempt < 9; attempt++)
            {
                next = chipAvailable ? Neighbor(next, direction) : SequentialNeighbor(next, direction);
                if (next == current) return;
                if (next == 0 && exitCursor != null)
                {
                    exitSelected = true;
                    return;
                }
                if (next == 8 && chipAvailable)
                {
                    exitSelected = false;
                    selectedIndex = portals.Length;
                    return;
                }
                for (int i = 0; i < portals.Length; i++)
                    if (portals[i] != null && portals[i].Available && portals[i].DestinationScene == $"Stage_3-{next}")
                    {
                        exitSelected = false;
                        selectedIndex = i;
                        return;
                    }
            }
        }

        private void RefreshCursor()
        {
            if (exitCursor != null)
            {
                exitCursor.transform.position = ChipSelected && !exitSelected ? chipAnchor.position : exitCursorPosition;
                exitCursor.enabled = selectionReady && (exitSelected || ChipSelected);
                var animator = exitCursor.GetComponent<World3HallExitCursorAnimator>();
                if (animator != null)
                    animator.SetSelectionSize(ChipSelected && !exitSelected ? new Vector2(80f, 80f) : new Vector2(64f, 32f));
            }
            if (exitSelected || ChipSelected)
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
            if (ChipSelected)
            {
                if (!ChipAvailable || !Application.CanStreamedLevelBeLoaded("Stage_3-8"))
                    return;
                transitioning = true;
                if (confirmSfx != null) GameAudioSettings.PlaySfx(audioSource, confirmSfx);
                StartCoroutine(EnterCompletedChip());
                return;
            }
            var portal = portals[selectedIndex];
            if (portal == null || !portal.Available)
                return;

            if (portal.DestinationScene == "Stage_3-7")
            {
                if (stageSevenSequence == null || !stageSevenSequence.CanPlay)
                {
                    Debug.LogError("World3 Hall stage 3-7 sequence is not configured.", this);
                    return;
                }

                transitioning = true;
                selectionReady = false;
                foreach (var hallPortal in portals)
                    if (hallPortal != null)
                        hallPortal.gameObject.SetActive(false);
                if (cursor != null)
                    cursor.gameObject.SetActive(false);
                if (exitCursor != null)
                    exitCursor.gameObject.SetActive(false);
                if (selectedStageLabelStyle != null)
                    selectedStageLabelStyle.gameObject.SetActive(false);
                else if (selectedStageLabel != null)
                    selectedStageLabel.enabled = false;

                stageSevenSequence.Play();
                return;
            }

            if (portalWalk == null)
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

        private IEnumerator EnterCompletedChip()
        {
            foreach (var movement in FindObjectsByType<MovementController>())
                if (movement.CompareTag("Player") && !movement.isDead)
                    PlayerPersistentStats.StageCaptureFromRuntime(movement, movement.GetComponent<BombController>());
            PlayerPersistentStats.CommitStage();
            if (StageIntroTransition.Instance != null)
                StageIntroTransition.Instance.StartFadeOut(fadeDuration);
            yield return new WaitForSecondsRealtime(fadeDuration);
            if (GameMusicController.Instance != null)
                GameMusicController.Instance.StopMusic();
            SceneManager.LoadSceneAsync("Stage_3-8");
        }

        public IEnumerator RevealUnlockedPortals()
        {
            if (portals == null)
                yield break;
            for (int i = 0; i < portals.Length; i++)
            {
                var portal = portals[i];
                if (portal == null || portal.Available || !StageUnlockProgress.IsUnlocked(portal.DestinationScene))
                    continue;
                yield return portal.Reveal(portalUnlockSfx);
                selectedIndex = i;
                exitSelected = false;
            }
            RefreshCursor();
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
