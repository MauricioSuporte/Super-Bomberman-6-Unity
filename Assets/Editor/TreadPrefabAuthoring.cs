#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the Tread prefab through Unity's prefab API.</summary>
internal static class TreadPrefabAuthoring
{
    private const string SpriteSheetPath = "Assets/Sprites/Enemies/Tread.png";
    private const string BanboPrefabPath = "Assets/Prefabs/Enemies/Banbo.prefab";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Tread.prefab";

    [MenuItem("Tools/Super Bomberman 6/Create Enemies/Tread Prefab")]
    private static void CreatePrefab()
    {
        Dictionary<string, Sprite> sprites = LoadSprites();
        Sprite[] down = GetFrames(sprites, 0, 1, 2);
        Sprite[] left = GetFrames(sprites, 3, 4, 5);
        Sprite[] up = GetFrames(sprites, 6, 7, 8);
        Sprite[] death = BuildCoreMechanismsDeath(GetFrames(sprites, 9)[0]);

        GameObject root = new("Tread") { layer = LayerMask.NameToLayer("Enemy") };
        try
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            TreadMovementController controller = root.AddComponent<TreadMovementController>();
            CharacterHealth health = root.GetComponent<CharacterHealth>();
            health.life = 2;
            health.hitInvulnerableDuration = 1f;

            AnimatedSpriteRenderer upRenderer = CreateWalkRenderer(root.transform, "Up", up, false);
            AnimatedSpriteRenderer downRenderer = CreateWalkRenderer(root.transform, "Down", down, true);
            AnimatedSpriteRenderer leftRenderer = CreateWalkRenderer(root.transform, "Left", left, false);
            AnimatedSpriteRenderer deathRenderer = CreateDeathRenderer(root.transform, death);

            controller.speed = 1.5f;
            controller.tileSize = 1f;
            controller.spriteUp = upRenderer;
            controller.spriteDown = downRenderer;
            controller.spriteLeft = leftRenderer;
            controller.spriteDeath = deathRenderer;
            controller.waitForFullDeathAnimation = true;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
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
        animated.animationSprite = new[] { frames[0], frames[1], frames[2], frames[1] };
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
        AnimatedSpriteRenderer banboDeath = banbo != null
            ? banbo.GetComponentsInChildren<AnimatedSpriteRenderer>(true).FirstOrDefault(renderer => renderer.gameObject.name == "Death")
            : null;
        if (banboDeath == null || banboDeath.animationSprite == null || banboDeath.animationSprite.Length < 8)
            throw new InvalidOperationException("Banbo's eight-frame CoreMechanisms death finish is required.");

        Sprite[] result = new Sprite[13];
        for (int index = 0; index < 5; index++)
            result[index] = deathStart;

        Array.Copy(banboDeath.animationSprite, banboDeath.animationSprite.Length - 8, result, 5, 8);
        return result;
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath).OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
            throw new InvalidOperationException($"No sprites were imported from {SpriteSheetPath}.");

        return sprites.ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
    }

    private static Sprite[] GetFrames(IReadOnlyDictionary<string, Sprite> sprites, params int[] indexes)
    {
        Sprite[] frames = new Sprite[indexes.Length];
        for (int index = 0; index < indexes.Length; index++)
        {
            string spriteName = $"Tread_{indexes[index]}";
            if (!sprites.TryGetValue(spriteName, out Sprite sprite))
                throw new InvalidOperationException($"Missing expected sprite {spriteName}.");

            frames[index] = sprite;
        }

        return frames;
    }
}
#endif
