using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct CoopMusic : NetworkMessage
    {
        public string scene, introResource, loopResource;
        public int intro, loop;
        public float volume, loopVolume, pitch;
        public bool looping, stopped;
    }

    public partial class BombermanNetworkManager
    {
        CoopMusic coopMusic;
        bool coopHasMusic;

        public static void CoopPlayMusic(AudioClip intro, float volume, bool looping, float pitch,
            AudioClip loop = null, float loopVolume = 1f, bool stopped = false)
        {
            if (!IsCoopStage || !NetworkServer.active || singleton is not BombermanNetworkManager manager || manager.coopCatalog == null) return;
            manager.coopMusic = new CoopMusic { scene = manager.coopScene,
                intro = manager.CoopSoundId(intro), loop = manager.CoopSoundId(loop),
                introResource = manager.CoopSoundResource(intro), loopResource = manager.CoopSoundResource(loop),
                volume = volume, loopVolume = loopVolume, pitch = pitch, looping = looping, stopped = stopped };
            manager.coopHasMusic = true;
            NetworkServer.SendToReady(manager.coopMusic);
        }

        int CoopSoundId(AudioClip clip) => clip != null && coopSoundIds.TryGetValue(clip, out int id) ? id : -1;

        string CoopSoundResource(AudioClip clip)
        {
            if (clip == null || coopSoundIds.ContainsKey(clip)) return null;
            foreach (string path in coopCatalog.resourceSoundPaths ?? System.Array.Empty<string>())
                if (System.IO.Path.GetFileName(path) == clip.name) return path;
            CatalogId(clip, coopSoundIds);
            return null;
        }

        void ApplyCoopMusic(CoopMusic message)
        {
            if (!CanApplyCoop(message.scene) || GameMusicController.Instance == null) return;
            if (message.stopped) { GameMusicController.Instance.StopMusic(); return; }
            var intro = At(coopCatalog.sounds, message.intro) ?? (string.IsNullOrEmpty(message.introResource) ? null : Resources.Load<AudioClip>(message.introResource));
            var loop = At(coopCatalog.sounds, message.loop) ?? (string.IsNullOrEmpty(message.loopResource) ? null : Resources.Load<AudioClip>(message.loopResource));
            if (intro == null) return;
            if (loop != null) GameMusicController.Instance.PlayMusicIntroThenLoop(intro, message.volume, loop, message.loopVolume, message.pitch);
            else GameMusicController.Instance.PlayMusic(intro, message.volume, message.looping, message.pitch);
        }
    }
}
