using UnityEditor;
using UnityEngine;

public static class MachindaPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/Machinda.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleMigration()
    {
        EditorApplication.delayCall += MigrateExistingPrefab;
    }

    private static void MigrateExistingPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset != null && asset.GetComponent<MachindaMovementController>() == null)
            ConfigurePrefab();
    }

    [MenuItem("Tools/Enemies/Configure Machinda Prefab")]
    public static void ConfigurePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var previous = root.GetComponent<EnemyMovementController>();
            if (!(previous is MachindaMovementController))
            {
                var controller = root.AddComponent<MachindaMovementController>();
                if (previous != null)
                {
                    EditorUtility.CopySerializedManagedFieldsOnly(previous, controller);
                    Object.DestroyImmediate(previous);
                }
            }
            root.GetComponent<CharacterHealth>().life = 1;
            var controllers = root.GetComponents<EnemyMovementController>();
            if (controllers.Length != 1) throw new System.InvalidOperationException("Machinda must have exactly one movement controller.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
