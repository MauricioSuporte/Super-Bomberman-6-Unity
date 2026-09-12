#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StageMusicCalibrationTool
{
    const string ReferenceClipPath = "Assets/Sounds/Robot Amusement Park Loop.wav";
    const float ReferenceVolume = 0.4f;
    const string BattleModeMusicResourcesPath = "Sounds/BattleModeMusics";
    const string CalibrationAssetPath = "Assets/Resources/Audio/MusicLoudnessCalibration.asset";

    static readonly Regex NormalStageName = new(@"^Stage_\d+-\d+$", RegexOptions.CultureInvariant);

    [MenuItem("Tools/Audio/Calibrate Stage Musics")]
    static void CalibrateStageMusics()
    {
        AudioClip referenceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ReferenceClipPath);
        if (referenceClip == null)
        {
            Debug.LogError($"Music calibration reference was not found at '{ReferenceClipPath}'.");
            return;
        }

        int calibratedStages = CalibrateNormalGameStages(referenceClip);
        int calibratedBattleClips = CalibrateBattleModeMusic(referenceClip);
        AssetDatabase.SaveAssets();
        Debug.Log($"Music calibration complete. Normal Game stages calibrated: {calibratedStages}; Battle Mode clips calibrated: {calibratedBattleClips}.");
    }

    static int CalibrateNormalGameStages(AudioClip referenceClip)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int calibratedStages = 0;

        try
        {
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (!NormalStageName.IsMatch(Path.GetFileNameWithoutExtension(scenePath)))
                    continue;

                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool openedByTool = !scene.IsValid() || !scene.isLoaded;
                if (openedByTool)
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                bool stageCalibrated = false;
                GameMusicController[] musicControllers = FindMusicControllers(scene);

                for (int controllerIndex = 0; controllerIndex < musicControllers.Length; controllerIndex++)
                {
                    GameMusicController controller = musicControllers[controllerIndex];
                    if (controller == null || controller.stageMusicLoudnessCalibrated)
                        continue;

                    stageCalibrated |= controller.CalibrateStageMusic(referenceClip, ReferenceVolume);
                }

                if (stageCalibrated)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    calibratedStages++;
                }

                if (openedByTool)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        return calibratedStages;
    }

    static GameMusicController[] FindMusicControllers(Scene scene)
    {
        List<GameMusicController> controllers = new();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            controllers.AddRange(roots[i].GetComponentsInChildren<GameMusicController>(true));

        return controllers.ToArray();
    }

    static int CalibrateBattleModeMusic(AudioClip referenceClip)
    {
        float referenceRms = GameMusicController.CalculateRepresentativeRms(referenceClip);
        if (referenceRms <= 0f)
            return 0;

        MusicLoudnessCalibration calibration = LoadOrCreateCalibrationAsset();
        AudioClip[] battleClips = Resources.LoadAll<AudioClip>(BattleModeMusicResourcesPath);
        List<MusicLoudnessCalibration.ClipVolume> volumes = new();
        float targetOutputRms = referenceRms * ReferenceVolume;

        for (int i = 0; i < battleClips.Length; i++)
        {
            AudioClip clip = battleClips[i];
            float rms = GameMusicController.CalculateRepresentativeRms(clip);
            if (clip == null || rms <= 0f)
                continue;

            volumes.Add(new MusicLoudnessCalibration.ClipVolume
            {
                clipName = clip.name,
                volume = Mathf.Clamp01(targetOutputRms / rms)
            });
        }

        volumes.Sort((left, right) => string.CompareOrdinal(left.clipName, right.clipName));
        Undo.RecordObject(calibration, "Calibrate Battle Mode Music");
        calibration.SetBattleModeClipVolumes(volumes);
        EditorUtility.SetDirty(calibration);
        return volumes.Count;
    }

    static MusicLoudnessCalibration LoadOrCreateCalibrationAsset()
    {
        MusicLoudnessCalibration calibration = AssetDatabase.LoadAssetAtPath<MusicLoudnessCalibration>(CalibrationAssetPath);
        if (calibration != null)
            return calibration;

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Audio"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AssetDatabase.CreateFolder("Assets/Resources", "Audio");
        }

        calibration = ScriptableObject.CreateInstance<MusicLoudnessCalibration>();
        AssetDatabase.CreateAsset(calibration, CalibrationAssetPath);
        return calibration;
    }
}
#endif
