using UnityEngine;

namespace StageAssets
{
    // Owns the final audio iteration after the moving snowball is removed.
    public sealed class SnowBallSfxTail : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip ownedClip;
        private bool audioPaused;

        public void Initialize(AudioSource audioSource, AudioClip generatedClip, bool paused)
        {
            source = audioSource;
            ownedClip = generatedClip;
            audioPaused = paused;
        }

        private void Update()
        {
            if (source == null)
            {
                Destroy(gameObject);
                return;
            }

            source.volume = GameAudioSettings.ApplySfxVolume(1f);
            bool paused = GamePauseController.IsPaused || Time.timeScale <= 0f;
            if (paused && !audioPaused) source.Pause();
            if (!paused && audioPaused) source.UnPause();
            audioPaused = paused;
            if (!paused && !source.isPlaying) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (source != null) source.Stop();
            if (ownedClip != null) Destroy(ownedClip);
        }
    }
}
