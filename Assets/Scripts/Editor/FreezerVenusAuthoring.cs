using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class FreezerVenusAuthoring
{
    private const string SheetPath = "Assets/Sprites/Bosses/FreezerVenus/FreezerVenus.png";
    private const string PrefabPath = "Assets/Prefabs/Bosses/FreezerVenus/FreezerVenus.prefab";

    [MenuItem("Tools/Bosses/Rebuild Freezer Venus")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before rebuilding Freezer Venus.");
        ImportSprites();
        AssetDatabase.ImportAsset("Assets/Sounds/IceCast.wav", ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset("Assets/Sounds/Ice.wav", ImportAssetOptions.ForceSynchronousImport);
        var iceCastSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/IceCast.wav");
        var iceSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Ice.wav");
        if (iceCastSfx == null || iceSfx == null)
            throw new InvalidOperationException("Import IceCast.wav and Ice.wav before rebuilding Freezer Venus.");
        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToDictionary(s => s.name);
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Bosses/FreezerVenus"))
            AssetDatabase.CreateFolder("Assets/Prefabs/Bosses", "FreezerVenus");
        var go = new GameObject("FreezerVenus");
        try
        {
            go.layer = LayerMask.NameToLayer("Enemy");
            go.tag = "BossBomber";
            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = sprites["Closed0"];
            body.sortingOrder = 10;
            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(0.875f, 0.5f);
            hitbox.offset = new Vector2(0f, -1.75f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            var health = go.AddComponent<CharacterHealth>();
            health.life = 10;
            health.hitInvulnerableDuration = 0.85f;
            health.hitBlinkInterval = 0.08f;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var boss = go.AddComponent<FreezerVenusBoss>();
            boss.body = body;
            var shadowObject = new GameObject("Pixel Shadow");
            shadowObject.transform.SetParent(go.transform, false);
            shadowObject.transform.localPosition = Vector3.down * 1.75f;
            var shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = sprites["Shadow"];
            shadow.color = new Color(0f, 0f, 0f, 0.65f);
            shadow.sortingOrder = body.sortingOrder - 2;
            boss.shadow = shadow;
            boss.closedFrames = Frames(sprites, "Closed", 3);
            boss.openingFrames = Frames(sprites, "Opening", 5);
            boss.idleFrames = new[] { sprites["Opening3"], sprites["Opening4"] };
            boss.castFrames = Frames(sprites, "Cast", 5);
            boss.iceCastFrames = new[]
            {
                sprites["DollCast0"], sprites["DollCast1"], sprites["DollCast2"], sprites["DollCast3"],
                sprites["DollCast3"], sprites["DollCast2"], sprites["DollCast1"], sprites["DollCast2"],
                sprites["DollCast3"], sprites["DollCast2"]
            };
            boss.dollCastFrames = Frames(sprites, "DollCast", 5);
            boss.hurtFrames = Frames(sprites, "Hurt", 2);
            boss.tornadoFrames = Frames(sprites, "Tornado", 6);
            boss.iceFrames = new[] { sprites["Ice"] };
            boss.summonCastFrames = new[] { sprites["SummonCast_1"], sprites["SummonCast2"] };
            boss.summonEffectFrames = new[] { sprites["Summon_1"], sprites["Summon_2"], sprites["Summon_3"] };
            ConfigureInvocationSprites(boss, sprites);
            boss.summonCastSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SummonCast.wav");
            var sun = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosses/SunMask/SunMask.prefab");
            var reference = sun.GetComponent<SunMaskBoss>();
            boss.summonCastSfxGain = 3f;
            boss.tornadoCastSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/TornadoCast.wav");
            boss.tornadoCastSfxGain = 3f;
            boss.iceCastSfx = iceCastSfx;
            boss.iceSfx = iceSfx;
            boss.iceCastSfxGain = 3f;
            boss.iceSfxGain = 3f;
            boss.deathSfx = reference.deathExplosionSfx;
            boss.explosionPrefab = reference.explosionPrefab;
            boss.endStageMusic = sun.GetComponent<BossEndStageSequence>().endStageMusic;
            go.AddComponent<FreezerVenusIntro>();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Bosses/Wire Freezer Venus To Open Hall")]
    public static void WireOpenHall()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Stage_World3Hall.unity")
            throw new InvalidOperationException("Open Stage_World3Hall in Edit Mode first.");
        var sequence = UnityEngine.Object.FindAnyObjectByType<StageAssets.World3HallStageSevenSequence>();
        if (sequence == null) throw new InvalidOperationException("Missing hall encounter sequence.");
        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("freezerVenusPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<FreezerVenusBoss>();
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ConfigureInvocationSprites(FreezerVenusBoss boss, Dictionary<string, Sprite> bossSprites)
    {
        const string path = "Assets/Sprites/Bosses/FreezerVenus/FreezeVenusInvocation.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "Server" })
            importer.ClearPlatformTextureSettings(platform);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        var rects = new List<SpriteRect>();
        for (int i = 0; i < 12; i++)
        {
            string name = "Invocation_" + (i + 1);
            rects.Add(new SpriteRect
            {
                name = name,
                rect = i < 9 ? new Rect(i * 16, 32, 16, 32) : new Rect((i - 9) * 32, 0, 32, 32),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, 0.25f),
                spriteID = old.TryGetValue(name, out var id) ? id : new GUID(Hash128.Compute("FreezerVenus/" + name).ToString())
            });
        }
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var invocation = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        boss.dollFrames = Enumerable.Range(1, 12).Select(i => invocation["Invocation_" + i]).ToArray();
        var funya = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Funya.prefab");
        var sharedDeath = funya.GetComponentsInChildren<AnimatedSpriteRenderer>(true)
            .Single(animation => animation.name == "Death");
        boss.invocationDeathFrames = sharedDeath.animationSprite.Skip(5).ToArray();
        boss.invocationCollisionFrames = Enumerable.Range(1, 5).Select(i => bossSprites["Summon_" + i]).ToArray();
    }

    private static Sprite[] Frames(Dictionary<string, Sprite> sprites, string prefix, int count)
        => Enumerable.Range(0, count).Select(i => sprites[prefix + i]).ToArray();

    private static void ImportSprites()
    {
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps", "Server" })
            importer.ClearPlatformTextureSettings(platform);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existingRects = provider.GetSpriteRects();
        var old = existingRects.ToDictionary(r => r.name, r => r.spriteID);
        var rects = new List<SpriteRect>();
        void Add(string name, int x, int top, int width, int height)
        {
            rects.Add(new SpriteRect
            {
                name = name, rect = new Rect(x, 450 - top - height, width, height),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(Mathf.Floor(width / 2f) / width, Mathf.Floor(height / 2f) / height),
                spriteID = old.TryGetValue(name, out var id) ? id : new GUID(Hash128.Compute("FreezerVenus/" + name).ToString())
            });
        }
        for (int i = 0; i < 3; i++) Add("Closed" + i, 69 + i * 50, 2, 48, 64);
        for (int i = 0; i < 5; i++) Add("Opening" + i, 65 + i * 50, 90, 50, 64);
        for (int i = 0; i < 5; i++) Add("Cast" + i, 131 + i * 50, 254, 50, 65);
        for (int i = 0; i < 2; i++) Add("Hurt" + i, 217 + i * 50, 157, 50, 65);
        Add("IceCast0", 14, 170, 58, 65);
        Add("IceCast1", 14, 239, 58, 65);
        Add("DollCast0", 77, 360, 52, 65);
        Add("DollCast1", 148, 360, 52, 65);
        Add("DollCast2", 241, 360, 52, 65);
        Add("DollCast3", 336, 360, 52, 65);
        Add("DollCast4", 408, 360, 52, 65);
        Add("Tornado0", 400, 257, 20, 29);
        Add("Tornado1", 425, 257, 20, 29);
        Add("Tornado2", 400, 289, 20, 29);
        Add("Tornado3", 425, 289, 20, 29);
        Add("Ice", 5, 390, 16, 18);
        Add("Shadow", 76, 66, 34, 16);
        // Preserve manually sliced summon sprites and their stable identifiers.
        rects.AddRange(existingRects.Where(r => r.name.StartsWith("Summon", StringComparison.Ordinal) ||
            r.name == "Tornado4" || r.name == "Tornado5"));
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }
}
