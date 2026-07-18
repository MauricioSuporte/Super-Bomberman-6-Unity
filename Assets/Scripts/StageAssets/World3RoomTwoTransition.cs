using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StageAssets
{
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class World3RoomTwoTransition : MonoBehaviour
    {
        [Serializable]
        private struct PlayerDestination
        {
            [Range(1, 6)] public int playerId;
            public Vector2 position;
        }

        [Header("Exit")]
        [SerializeField] private World3BambooExitBlocker exitBlocker;

        [Header("Fade")]
        [SerializeField] private Image fadeImage;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 1f;
        [SerializeField, Min(0f)] private float blackScreenSeconds = 1f;
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 1f;

        [Header("Cameras")]
        [SerializeField] private Camera roomOneCamera;
        [SerializeField] private Camera roomTwoCamera;

        [Header("Room 2 Player Destinations")]
        [SerializeField] private PlayerDestination[] playerDestinations =
        {
            new PlayerDestination { playerId = 1, position = new Vector2(15f, 4f) },
            new PlayerDestination { playerId = 2, position = new Vector2(16f, 4f) },
            new PlayerDestination { playerId = 3, position = new Vector2(15f, 3f) },
            new PlayerDestination { playerId = 4, position = new Vector2(16f, 3f) },
            new PlayerDestination { playerId = 5, position = new Vector2(15f, 5f) },
            new PlayerDestination { playerId = 6, position = new Vector2(16f, 5f) }
        };

        private readonly List<MovementController> players = new();
        private readonly List<EnemyMovementController> enemies = new();
        private readonly List<CharacterHealth> invulnerableHealths = new();
        private readonly Dictionary<Behaviour, bool> previousEnabledStates = new();
        private bool transitionStarted;
        private float timeScaleBeforeTransition;

        private void Awake()
        {
            BoxCollider2D trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transitionStarted || exitBlocker == null || !exitBlocker.IsExitOpen)
                return;

            MovementController player = other.GetComponent<MovementController>();
            if (player == null)
                player = other.GetComponentInParent<MovementController>();

            if (player == null || player.isDead || !IsPlayer(player))
                return;

            transitionStarted = true;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            FreezeGameplay();
            timeScaleBeforeTransition = Time.timeScale;
            Time.timeScale = 0f;
            yield return FadeTo(1f, fadeOutSeconds);
            FaceLivingPlayersDown();

            if (blackScreenSeconds > 0f)
                yield return new WaitForSecondsRealtime(blackScreenSeconds);

            MoveLivingPlayersToRoomTwo();
            SetCameraActive(roomOneCamera, false);
            SetCameraActive(roomTwoCamera, true);
            yield return null;

            yield return FadeTo(0f, fadeInSeconds);
            Time.timeScale = timeScaleBeforeTransition;
            UnfreezeGameplay();
        }

        private void FreezeGameplay()
        {
            previousEnabledStates.Clear();
            players.Clear();
            enemies.Clear();
            invulnerableHealths.Clear();

            MovementController[] movementControllers = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < movementControllers.Length; i++)
            {
                MovementController player = movementControllers[i];
                if (player == null || player.isDead || !IsPlayer(player))
                    continue;

                players.Add(player);
                player.SetInputLocked(true, false);
                player.ApplyDirectionFromVector(Vector2.zero);

                if (player.Rigidbody != null)
                    player.Rigidbody.linearVelocity = Vector2.zero;

                SetTransitionInvulnerability(player.GetComponent<CharacterHealth>());
                RememberAndDisable(player.GetComponent<BombController>());
                RememberAndDisable(player.GetComponent<PlayerManualDismount>());
            }

            EnemyMovementController[] enemyControllers = FindObjectsByType<EnemyMovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemyControllers.Length; i++)
            {
                EnemyMovementController enemy = enemyControllers[i];
                if (enemy == null)
                    continue;

                enemies.Add(enemy);
                if (enemy.TryGetComponent<Rigidbody2D>(out Rigidbody2D enemyBody))
                    enemyBody.linearVelocity = Vector2.zero;

                SetTransitionInvulnerability(enemy.GetComponent<CharacterHealth>());
                RememberAndDisable(enemy);
            }
        }

        private void MoveLivingPlayersToRoomTwo()
        {
            for (int i = 0; i < players.Count; i++)
            {
                MovementController player = players[i];
                if (player == null || player.isDead || !TryGetDestination(player, out Vector2 destination))
                    continue;

                Vector3 position = player.transform.position;
                position.x = destination.x;
                position.y = destination.y;

                if (player.Rigidbody != null)
                {
                    player.Rigidbody.position = destination;
                    player.Rigidbody.linearVelocity = Vector2.zero;
                }

                player.transform.position = position;
                player.ApplyDirectionFromVector(Vector2.zero);
            }
        }

        private void FaceLivingPlayersDown()
        {
            for (int i = 0; i < players.Count; i++)
            {
                MovementController player = players[i];
                if (player != null && !player.isDead)
                    player.ForceIdleFacing(Vector2.down, "Room2Transition");
            }
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (fadeImage == null)
                yield break;

            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();

            Color color = fadeImage.color;
            float startAlpha = color.a;
            float elapsed = 0f;
            float normalizedDuration = Mathf.Max(0.01f, duration);

            while (elapsed < normalizedDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / normalizedDuration));
                fadeImage.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            fadeImage.color = color;

            if (targetAlpha <= 0f)
                fadeImage.gameObject.SetActive(false);
        }

        private void UnfreezeGameplay()
        {
            for (int i = 0; i < players.Count; i++)
            {
                MovementController player = players[i];
                if (player != null && !player.isDead)
                    player.SetInputLocked(false, true);
            }

            foreach (KeyValuePair<Behaviour, bool> pair in previousEnabledStates)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }

            previousEnabledStates.Clear();

            for (int i = 0; i < invulnerableHealths.Count; i++)
            {
                CharacterHealth health = invulnerableHealths[i];
                if (health != null)
                    health.SetExternalInvulnerability(false);
            }

            invulnerableHealths.Clear();
        }

        private void SetTransitionInvulnerability(CharacterHealth health)
        {
            if (health == null || invulnerableHealths.Contains(health))
                return;

            invulnerableHealths.Add(health);
            health.SetExternalInvulnerability(true);
        }

        private void RememberAndDisable(Behaviour behaviour)
        {
            if (behaviour == null || previousEnabledStates.ContainsKey(behaviour))
                return;

            previousEnabledStates.Add(behaviour, behaviour.enabled);
            behaviour.enabled = false;
        }

        private bool TryGetDestination(MovementController player, out Vector2 destination)
        {
            int playerId = player.PlayerId;
            if (player.TryGetComponent(out PlayerIdentity identity))
                playerId = identity.playerId;

            for (int i = 0; i < playerDestinations.Length; i++)
            {
                if (playerDestinations[i].playerId != playerId)
                    continue;

                destination = playerDestinations[i].position;
                return true;
            }

            destination = default;
            return false;
        }

        private static bool IsPlayer(MovementController controller)
        {
            return controller.CompareTag("Player") || controller.GetComponent<PlayerIdentity>() != null;
        }

        private static void SetCameraActive(Camera camera, bool active)
        {
            if (camera == null)
                return;

            camera.enabled = active;
            if (camera.TryGetComponent(out AudioListener listener))
                listener.enabled = active;
        }
    }
}
