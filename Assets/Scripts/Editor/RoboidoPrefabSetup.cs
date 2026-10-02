using UnityEditor;
using UnityEngine;

// Migrates the supplied Crasher-based prefab once, after the controller compiles.
[InitializeOnLoad]
public static class RoboidoPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/Roboido.prefab";

    static RoboidoPrefabSetup()
    {
        EditorApplication.delayCall += MigrateLegacyPrefab;
    }

    private static void MigrateLegacyPrefab()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += MigrateLegacyPrefab;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null || asset.GetComponent<CrasherMovementController>() == null) return;
        Configure();
    }

    [MenuItem("Tools/Enemies/Configure Roboido Prefab")]
    public static void Configure()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnemyMovementController previous = root.GetComponent<EnemyMovementController>();
            RoboidoMovementController controller = previous as RoboidoMovementController;
            if (controller == null)
            {
                // Retain only the shared movement settings, without Crasher behavior.
                controller = root.AddComponent<RoboidoMovementController>();
                if (previous != null)
                {
                    controller.speed = previous.speed;
                    controller.tileSize = previous.tileSize;
                    controller.spriteUp = previous.spriteUp;
                    controller.spriteDown = previous.spriteDown;
                    controller.spriteLeft = previous.spriteLeft;
                    controller.spriteRight = previous.spriteRight;
                    controller.spriteDeath = previous.spriteDeath;
                    controller.spriteDamaged = previous.spriteDamaged;
                    controller.waitForFullDeathAnimation = previous.waitForFullDeathAnimation;
                    controller.obstacleMask = previous.obstacleMask;
                    controller.bombLayerMask = previous.bombLayerMask;
                    controller.enemyLayerMask = previous.enemyLayerMask;
                    controller.deathSfx = previous.deathSfx;
                    controller.recheckStuckEverySeconds = previous.recheckStuckEverySeconds;
                    if (previous is JunctionTurningEnemyMovementController junction)
                    {
                        controller.minAvailablePathsToTurn = junction.minAvailablePathsToTurn;
                        controller.preferTurnAtJunction = junction.preferTurnAtJunction;
                    }
                }
                if (previous != null) Object.DestroyImmediate(previous);
            }
            var serialized = new SerializedObject(controller);
            Assign(serialized, "wakeUp", root, "WakeUp");
            Assign(serialized, "attackUp", root, "AttackUp");
            Assign(serialized, "attackDown", root, "AttackDown");
            Assign(serialized, "attackLeft", root, "AttackLeft");
            serialized.FindProperty("destructionSfx").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/CrasherDestruction.mp3");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (AnimatedSpriteRenderer animation in root.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
            {
                bool sleeping = animation.name == "WakeUp";
                bool special = sleeping || animation.name.StartsWith("Attack", System.StringComparison.Ordinal);
                if (special) animation.loop = false;
                animation.idle = sleeping;
                animation.enabled = sleeping;
                var renderer = animation.GetComponent<SpriteRenderer>();
                renderer.sprite = animation.idleSprite;
                renderer.enabled = sleeping;
                EditorUtility.SetDirty(animation);
                EditorUtility.SetDirty(renderer);
            }
            var importer = AssetImporter.GetAtPath("Assets/Sprites/Enemies/Roboido.png") as TextureImporter;
            if (importer != null && importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Roboido prefab configured: sleeping, directional attacks, Crasher destruction SFX.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Assign(SerializedObject serialized, string field, GameObject root, string child)
    {
        Transform transform = root.transform.Find(child);
        if (transform == null) throw new System.InvalidOperationException($"Roboido is missing {child}.");
        serialized.FindProperty(field).objectReferenceValue = transform.GetComponent<AnimatedSpriteRenderer>();
    }
}
