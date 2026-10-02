using UnityEditor;
using UnityEngine;

/// <summary>Migrates the authored Dash prefab through Unity's prefab APIs after compilation.</summary>
[InitializeOnLoad]
public static class DashPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/Dash.prefab";

    static DashPrefabSetup()
    {
        EditorApplication.delayCall += EnsureConfigured;
    }

    [MenuItem("Tools/Enemies/Configure Dash Prefab")]
    public static void EnsureConfigured()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null || asset.GetComponent<DashMovementController>() != null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnemyMovementController previous = root.GetComponent<EnemyMovementController>();
            DashMovementController dash = root.AddComponent<DashMovementController>();
            if (previous != null)
            {
                dash.speed = previous.speed;
                dash.tileSize = previous.tileSize;
                dash.obstacleMask = previous.obstacleMask;
                dash.bombLayerMask = previous.bombLayerMask;
                dash.enemyLayerMask = previous.enemyLayerMask;
                dash.deathSfx = previous.deathSfx;
                dash.recheckStuckEverySeconds = previous.recheckStuckEverySeconds;
                Object.DestroyImmediate(previous);
            }

            dash.spriteUp = FindSprite(root, "Up");
            dash.spriteDown = FindSprite(root, "Down");
            dash.spriteLeft = FindSprite(root, "Left");
            dash.spriteDeath = FindSprite(root, "Death");
            dash.waitForFullDeathAnimation = true;
            SerializedObject serialized = new(dash);
            serialized.FindProperty("boostUp").objectReferenceValue = FindSprite(root, "DashUp");
            serialized.FindProperty("boostDown").objectReferenceValue = FindSprite(root, "DashDown");
            serialized.FindProperty("boostLeft").objectReferenceValue = FindSprite(root, "DashLeft");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (AnimatedSpriteRenderer animation in root.GetComponentsInChildren<AnimatedSpriteRenderer>(true))
            {
                SpriteRenderer renderer = animation.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    throw new System.InvalidOperationException($"Dash child {animation.name} has no SpriteRenderer.");
                renderer.sprite = animation.idleSprite;
                animation.enabled = animation == dash.spriteDown;
                renderer.enabled = animation.enabled;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved.GetComponents<EnemyMovementController>().Length != 1 || saved.GetComponent<DashMovementController>() == null)
            throw new System.InvalidOperationException("Dash prefab movement configuration failed.");
        Debug.Log("Dash prefab configured: pursuit, rocket animations and five-second boost release.");
    }

    private static AnimatedSpriteRenderer FindSprite(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);
        if (child == null || !child.TryGetComponent<AnimatedSpriteRenderer>(out var animation))
            throw new System.InvalidOperationException($"Dash prefab is missing animation {childName}.");
        return animation;
    }
}
