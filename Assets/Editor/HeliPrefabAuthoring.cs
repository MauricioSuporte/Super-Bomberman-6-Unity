#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class HeliPrefabAuthoring
{
    private const string Sheet = "Assets/Sprites/Enemies/Heli.png";
    private const string Funya = "Assets/Prefabs/Enemies/Funya.prefab";
    private const string Prefab = "Assets/Prefabs/Enemies/Heli.prefab";

    [MenuItem("Tools/Super Bomberman 6/Create Enemies/Heli Prefab and Room 1")]
    private static void Create()
    {
        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
        GameObject root = new("Heli") { layer = LayerMask.NameToLayer("Enemy") };
        try
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 0f; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = 0.35f;
            HeliMovementController controller = root.AddComponent<HeliMovementController>();
            CharacterHealth health = root.GetComponent<CharacterHealth>(); health.life = 1; health.hitInvulnerableDuration = 1f;
            controller.speed = 1.5f;
            controller.spriteDown = Walk(root.transform, "Down", Frames(sprites, 0, 2, 4), true);
            controller.spriteUp = Walk(root.transform, "Up", Frames(sprites, 1, 3, 5), false);
            controller.spriteLeft = Walk(root.transform, "Left", Frames(sprites, 6, 8, 10), false);
            controller.spriteDeath = Death(root.transform, SmallDeath(Frames(sprites, 11)[0]));
            controller.waitForFullDeathAnimation = true;
            SetPrivateSprite(controller, "projectileSprite", Frames(sprites, 9)[0]);
            SetPrivateSprite(controller, "projectileImpactSprite", Frames(sprites, 11)[0]);
            SetPrivateFloat(controller, "ghostAlpha", 1f);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            AssetDatabase.SaveAssets();
            Place(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Place(GameObject prefab)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Stage_3-6.unity") throw new InvalidOperationException("Open Stage_3-6 before creating Heli.");
        Collider2D room = StageAssets.World3RoomProgressionController.FindRoomBounds("Room 1");
        if (room == null || prefab == null) throw new InvalidOperationException("Room 1 or Heli prefab was not found.");
        foreach (HeliMovementController heli in UnityEngine.Object.FindObjectsByType<HeliMovementController>(FindObjectsInactive.Include))
            if (heli != null && heli.gameObject.scene.IsValid() && room.OverlapPoint(heli.transform.position)) UnityEngine.Object.DestroyImmediate(heli.gameObject);
        List<Vector2> positions = new(); int mask = LayerMask.GetMask("Stage", "Bomb", "Enemy"); Bounds bounds = room.bounds; Physics2D.SyncTransforms();
        for (float y = Mathf.Round(bounds.max.y - 1f); y > bounds.min.y + 1f && positions.Count < 2; y -= 1f)
        for (float x = Mathf.Round(bounds.min.x + 1f); x < bounds.max.x - 1f && positions.Count < 2; x += 1f)
            if (room.OverlapPoint(new Vector2(x, y)) && Physics2D.OverlapBox(new Vector2(x, y), Vector2.one * .6f, 0f, mask) == null) positions.Add(new Vector2(x, y));
        if (positions.Count != 2) throw new InvalidOperationException("Room 1 does not have two clear tiles for Heli.");
        for (int index = 0; index < 2; index++) { GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); instance.name = $"Heli ({index + 1})"; instance.transform.SetParent(room.transform, true); instance.transform.position = positions[index]; }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Selection.activeObject = prefab;
    }

    private static AnimatedSpriteRenderer Walk(Transform parent, string name, Sprite[] frames, bool enabled)
    {
        GameObject visual = new(name) { layer = LayerMask.NameToLayer("Enemy") }; visual.transform.SetParent(parent, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; renderer.sprite = frames[0];
        AnimatedSpriteRenderer animated = visual.AddComponent<AnimatedSpriteRenderer>(); animated.idleSprite = frames[0]; animated.animationSprite = new[] { frames[0], frames[1], frames[2], frames[1] }; animated.animationTime = .15f; animated.loop = true; animated.idle = false; animated.enabled = enabled; renderer.enabled = enabled; return animated;
    }

    private static AnimatedSpriteRenderer Death(Transform parent, Sprite[] frames)
    {
        GameObject visual = new("Death") { layer = LayerMask.NameToLayer("Enemy") }; visual.transform.SetParent(parent, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 5; renderer.sprite = frames[0];
        AnimatedSpriteRenderer animated = visual.AddComponent<AnimatedSpriteRenderer>(); animated.idleSprite = frames[0]; animated.animationSprite = frames; animated.animationTime = .1f; animated.loop = false; animated.idle = false; animated.enabled = false; renderer.enabled = false; return animated;
    }

    private static Sprite[] SmallDeath(Sprite start)
    {
        AnimatedSpriteRenderer death = AssetDatabase.LoadAssetAtPath<GameObject>(Funya).GetComponentsInChildren<AnimatedSpriteRenderer>(true).First(renderer => renderer.gameObject.name == "Death");
        Sprite[] result = new Sprite[12]; for (int index = 0; index < 5; index++) result[index] = start; Array.Copy(death.animationSprite, death.animationSprite.Length - 7, result, 5, 7); return result;
    }

    private static void SetPrivateSprite(HeliMovementController controller, string propertyName, Sprite sprite)
    {
        SerializedObject serializedController = new(controller);
        serializedController.FindProperty(propertyName).objectReferenceValue = sprite;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateFloat(HeliMovementController controller, string propertyName, float value)
    {
        SerializedObject serializedController = new(controller);
        serializedController.FindProperty(propertyName).floatValue = value;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite[] Frames(IReadOnlyDictionary<string, Sprite> sprites, params int[] indexes)
        => indexes.Select(index => sprites[$"Heli_{index}"]).ToArray();
}
#endif
