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
    const string WorldMapScenePath = "Assets/Scenes/WorldMap.unity";

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
        int calibratedWorldMapClips = CalibrateWorldMapMusic(referenceClip);
        int calibratedBattleClips = CalibrateBattleModeMusic(referenceClip);
        AssetDatabase.SaveAssets();
        Debug.Log($"Music calibration complete. Normal Game stages calibrated: {calibratedStages}; World Map clips calibrated: {calibratedWorldMapClips}; Battle Mode clips calibrated: {calibratedBattleClips}.");
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

    static int CalibrateWorldMapMusic(AudioClip referenceClip)
    {
        float referenceRms = GameMusicController.CalculateRepresentativeRms(referenceClip);
        if (referenceRms <= 0f)
            return 0;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = SceneManager.GetSceneByPath(WorldMapScenePath);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;
            if (openedByTool)
                scene = EditorSceneManager.OpenScene(WorldMapScenePath, OpenSceneMode.Additive);

            WorldMapController controller = FindWorldMapController(scene);
            if (controller == null)
                return 0;

            SerializedObject serializedController = new(controller);
            SerializedProperty worlds = serializedController.FindProperty("worlds");
            int calibratedClips = 0;
            float targetOutputRms = referenceRms * ReferenceVolume;

            for (int i = 0; i < worlds.arraySize; i++)
            {
                SerializedProperty world = worlds.GetArrayElementAtIndex(i);
                SerializedProperty music = world.FindPropertyRelative("worldMusic");
                SerializedProperty musicLoop = world.FindPropertyRelative("worldMusicLoop");
                SerializedProperty volume = world.FindPropertyRelative("worldMusicVolume");
                AudioClip clip = musicLoop.objectReferenceValue as AudioClip ?? music.objectReferenceValue as AudioClip;
                float rms = GameMusicController.CalculateRepresentativeRms(clip);
                if (rms <= 0f)
                    continue;

                volume.floatValue = Mathf.Clamp01(targetOutputRms / rms);
                calibratedClips++;
            }

            if (calibratedClips > 0)
            {
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedByTool)
                EditorSceneManager.CloseScene(scene, true);

            return calibratedClips;
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    static WorldMapController FindWorldMapController(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            WorldMapController controller = roots[i].GetComponentInChildren<WorldMapController>(true);
            if (controller != null)
                return controller;
        }

        return null;
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
