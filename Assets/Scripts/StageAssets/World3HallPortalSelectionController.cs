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
        [SerializeField] private AudioClip returnSfx;
        [SerializeField] private Text selectedStageLabel;
        [SerializeField] private StagePreIntroPlayersWalk portalWalk;
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
        private MovementController[] players;
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
            if (input.GetDown(1, PlayerAction.MoveDown) && exitCursor != null)
            {
                if (!exitSelected)
                {
                    int below = FindPortalInDirection(Vector2.down);
                    if (below >= 0)
                        selectedIndex = below;
                    else
                        exitSelected = true;
                }
            }
            else if (input.GetDown(1, PlayerAction.MoveUp))
            {
                if (exitSelected)
                    exitSelected = false;
                else
                {
                    int above = FindPortalInDirection(Vector2.up);
                    if (above >= 0)
                        selectedIndex = above;
                }
            }
            else if (!exitSelected && input.GetDown(1, PlayerAction.MoveLeft))
                MoveHorizontal(-1f);
            else if (!exitSelected && input.GetDown(1, PlayerAction.MoveRight))
                MoveHorizontal(1f);

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

        private int FindPortalInDirection(Vector2 direction)
        {
            if (portals[selectedIndex] == null)
                return -1;

            Vector2 origin = portals[selectedIndex].transform.position;
            int nearest = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < portals.Length; i++)
            {
                if (i == selectedIndex || portals[i] == null || !portals[i].Available)
                    continue;

                Vector2 delta = (Vector2)portals[i].transform.position - origin;
                if (Vector2.Dot(delta, direction) <= 0.01f)
                    continue;

                float distance = direction.x != 0f ? Mathf.Abs(delta.x) : delta.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        private void MoveHorizontal(float direction)
        {
            int next = FindPortalInDirection(new Vector2(direction, 0f));
            if (next >= 0)
            {
                selectedIndex = next;
                return;
            }

            float edge = direction > 0f ? float.PositiveInfinity : float.NegativeInfinity;
            for (int i = 0; i < portals.Length; i++)
            {
                if (portals[i] == null || !portals[i].Available)
                    continue;
                float x = portals[i].transform.position.x;
                if ((direction > 0f && x < edge) || (direction < 0f && x > edge))
                {
                    edge = x;
                    selectedIndex = i;
                }
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
            if (portal == null || !portal.Available || portalWalk == null)
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

        public IEnumerator RevealUnlockedPortals()
        {
            if (portals == null)
                yield break;
            foreach (var portal in portals)
            {
                if (portal == null || portal.Available || !StageUnlockProgress.IsUnlocked(portal.DestinationScene))
                    continue;
                yield return portal.Reveal(portalUnlockSfx);
            }
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
