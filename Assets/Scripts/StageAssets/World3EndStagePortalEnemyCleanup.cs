using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class World3EndStagePortalEnemyCleanup : MonoBehaviour
    {
        private bool cleanupTriggered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (cleanupTriggered || other == null || !other.CompareTag("Player"))
                return;

            cleanupTriggered = true;
            World3GateOpenedSequenceController sequence = GetComponentInParent<World3GateOpenedSequenceController>();
            if (sequence != null)
                sequence.BeginChipBlinkOut();

            World3EndStageCelebrationEffect.Play(
                sequence != null ? sequence.GetChipCenterWorld() : other.transform.position,
                other.transform);
            KillActiveEnemies();
        }

        private static void KillActiveEnemies()
        {
            EnemyMovementController[] enemies = FindObjectsByType<EnemyMovementController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyMovementController enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                    continue;

                if (enemy.TryGetComponent(out CharacterHealth health))
                {
                    if (health.life <= 0)
                        continue;

                    health.SetExternalInvulnerability(false);
                    health.StopInvulnerability();
                    health.TakeDamage(Mathf.Max(9999, health.life), fromExplosion: false);
                }
                else if (enemy.TryGetComponent(out IKillable killable))
                {
                    killable.Kill();
                }
            }
        }
    }
}
