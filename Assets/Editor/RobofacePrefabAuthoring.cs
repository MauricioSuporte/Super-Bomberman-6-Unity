#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class RobofacePrefabAuthoring
{
    private const string SpriteSheetPath = "Assets/Sprites/Enemies/Roboface.png";
    private const string BanboPrefabPath = "Assets/Prefabs/Enemies/Banbo.prefab";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Roboface.prefab";
    private const string StageScenePath = "Assets/Scenes/Stage_3-6.unity";

    [MenuItem("Tools/Super Bomberman 6/Create Enemies/Roboface Prefab and Room 1")]
    private static void CreatePrefabAndPlace()
    {
        Dictionary<string, Sprite> sprites = LoadSprites();
        GameObject root = new("Roboface") { layer = LayerMask.NameToLayer("Enemy") };
        try
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            RobofaceMovementController controller = root.AddComponent<RobofaceMovementController>();
            CharacterHealth health = root.GetComponent<CharacterHealth>();
            health.life = 2;
            health.hitInvulnerableDuration = 1f;

            controller.speed = 2f;
            controller.tileSize = 1f;
            controller.spriteDown = CreateWalkRenderer(root.transform, "Down", GetFrames(sprites, 0, 1), true);
            controller.spriteLeft = CreateWalkRenderer(root.transform, "Left", GetFrames(sprites, 2, 3), false);
            controller.spriteRight = CreateWalkRenderer(root.transform, "Right", GetFrames(sprites, 4, 5), false);
            controller.spriteUp = CreateWalkRenderer(root.transform, "Up", GetFrames(sprites, 6, 7), false);
            controller.spriteDeath = CreateDeathRenderer(root.transform, BuildCoreMechanismsDeath(GetFrames(sprites, 8)[0]));
            controller.waitForFullDeathAnimation = true;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            PlaceTwoInRoomOne(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void PlaceTwoInRoomOne(GameObject prefab)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (prefab == null || scene.path != StageScenePath)
            throw new InvalidOperationException("Open Stage_3-6 before creating and placing Roboface.");

        Collider2D roomBounds = StageAssets.World3RoomProgressionController.FindRoomBounds("Room 1");
        if (roomBounds == null)
            throw new InvalidOperationException("Stage_3-6 Room 1 bounds were not found.");

        RemoveExistingRobofaces(roomBounds);
        Vector2[] positions = FindOpenPositions(roomBounds, 2);
        if (positions.Length != 2)
            throw new InvalidOperationException("Room 1 does not have two clear tiles for Roboface.");

        for (int index = 0; index < positions.Length; index++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = $"Roboface ({index + 1})";
            instance.transform.SetParent(roomBounds.transform, true);
            instance.transform.position = positions[index];
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = prefab;
    }

    private static void RemoveExistingRobofaces(Collider2D roomBounds)
    {
        RobofaceMovementController[] existing = UnityEngine.Object.FindObjectsByType<RobofaceMovementController>(FindObjectsInactive.Include);
        for (int index = 0; index < existing.Length; index++)
            if (existing[index] != null && existing[index].gameObject.scene.IsValid() && roomBounds.OverlapPoint(existing[index].transform.position))
                UnityEngine.Object.DestroyImmediate(existing[index].gameObject);
    }

    private static Vector2[] FindOpenPositions(Collider2D roomBounds, int requiredCount)
    {
        int obstacleMask = LayerMask.GetMask("Stage", "Bomb", "Enemy");
        Bounds bounds = roomBounds.bounds;
        List<Vector2> positions = new(requiredCount);
        Physics2D.SyncTransforms();

        for (float y = Mathf.Round(bounds.max.y - 1f); y > bounds.min.y + 1f; y -= 1f)
        for (float x = Mathf.Round(bounds.min.x + 1f); x < bounds.max.x - 1f; x += 1f)
        {
            Vector2 candidate = new(x, y);
            if (!roomBounds.OverlapPoint(candidate) || Physics2D.OverlapBox(candidate, Vector2.one * 0.6f, 0f, obstacleMask) != null)
                continue;

            positions.Add(candidate);
            if (positions.Count == requiredCount)
                return positions.ToArray();
        }

        return positions.ToArray();
    }

    private static AnimatedSpriteRenderer CreateWalkRenderer(Transform parent, string name, Sprite[] frames, bool enabled)
    {
        GameObject visual = new(name) { layer = LayerMask.NameToLayer("Enemy") };
        visual.transform.SetParent(parent, false);
        SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 5;
        spriteRenderer.sprite = frames[0];
        AnimatedSpriteRenderer animated = visual.AddComponent<AnimatedSpriteRenderer>();
        animated.idleSprite = frames[0];
        animated.animationSprite = frames;
        animated.animationTime = 0.15f;
        animated.loop = true;
        animated.idle = false;
        animated.enabled = enabled;
        spriteRenderer.enabled = enabled;
        return animated;
    }

    private static AnimatedSpriteRenderer CreateDeathRenderer(Transform parent, Sprite[] frames)
    {
        GameObject visual = new("Death") { layer = LayerMask.NameToLayer("Enemy") };
        visual.transform.SetParent(parent, false);
        SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 5;
        spriteRenderer.sprite = frames[0];
        AnimatedSpriteRenderer animated = visual.AddComponent<AnimatedSpriteRenderer>();
        animated.idleSprite = frames[0];
        animated.animationSprite = frames;
        animated.animationTime = 0.1f;
        animated.loop = false;
        animated.idle = false;
        animated.enabled = false;
        spriteRenderer.enabled = false;
        return animated;
    }

    private static Sprite[] BuildCoreMechanismsDeath(Sprite deathStart)
    {
        GameObject banbo = AssetDatabase.LoadAssetAtPath<GameObject>(BanboPrefabPath);
        AnimatedSpriteRenderer banboDeath = banbo != null ? banbo.GetComponentsInChildren<AnimatedSpriteRenderer>(true).FirstOrDefault(renderer => renderer.gameObject.name == "Death") : null;
        if (banboDeath == null || banboDeath.animationSprite == null || banboDeath.animationSprite.Length < 8)
            throw new InvalidOperationException("Banbo's eight-frame CoreMechanisms death finish is required.");

        Sprite[] result = new Sprite[13];
        for (int index = 0; index < 5; index++) result[index] = deathStart;
        Array.Copy(banboDeath.animationSprite, banboDeath.animationSprite.Length - 8, result, 5, 8);
        return result;
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath).OfType<Sprite>().ToArray();
        if (sprites.Length == 0) throw new InvalidOperationException($"No sprites were imported from {SpriteSheetPath}.");
        return sprites.ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
    }

    private static Sprite[] GetFrames(IReadOnlyDictionary<string, Sprite> sprites, params int[] indexes)
    {
        Sprite[] frames = new Sprite[indexes.Length];
        for (int index = 0; index < indexes.Length; index++)
        {
            string spriteName = $"Roboface_{indexes[index]}";
            if (!sprites.TryGetValue(spriteName, out Sprite sprite)) throw new InvalidOperationException($"Missing expected sprite {spriteName}.");
            frames[index] = sprite;
        }
        return frames;
    }
}
#endif
