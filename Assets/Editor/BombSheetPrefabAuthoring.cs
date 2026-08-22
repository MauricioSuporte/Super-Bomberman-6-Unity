using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class BombSheetPrefabAuthoring
{
    private const string SheetPath = "Assets/Resources/Sprites/BombItems/Itens.png";
    private const string BombPrefabPath = "Assets/Prefabs/Bombs/Bomb.prefab";
    private const string PierceBombPrefabPath = "Assets/Prefabs/Bombs/PierceBomb.prefab";
    private const string ExplosionPrefabPath = "Assets/Resources/Explosions/BombExplosion.prefab";
    private const int CellSize = 16;

    [MenuItem("Tools/Super Bomberman 6/Apply new Sheet Sprites")]
    public static void ApplyAllBombAndExplosionSheetSprites()
    {
        ApplyNormalBombSprites();
        ApplyBombExplosionSprites();
        ApplyPierceBombSprites();

        Debug.Log("[BombSheetPrefabAuthoring] All configured bomb and explosion sprites were applied from Itens.png.");
    }

    public static void ApplyNormalBombSprites()
    {
        ConfigureSheet();

        Sprite large = LoadSprite("BombLarge");
        Sprite medium = LoadSprite("BombMedium");
        Sprite small = LoadSprite("BombSmall");

        if (large == null || medium == null || small == null)
            throw new InvalidOperationException("Normal bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("Bomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = medium;
            renderer.animationSprite = new[] { medium, large, medium, small };
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = medium;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] Bomb.prefab now uses Itens.png cells (20,0), (19,0), (20,0), (21,0).");
    }

    public static void ApplyBombExplosionSprites()
    {
        ConfigureSheet();

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ExplosionPrefabPath);
        try
        {
            BombExplosion explosion = prefabRoot.GetComponent<BombExplosion>();
            if (explosion == null)
                throw new InvalidOperationException("BombExplosion.prefab is missing BombExplosion.");

            ApplyExplosionAnimation(explosion.start, "ExplosionStart");
            ApplyExplosionAnimation(explosion.middle, "ExplosionMiddle");
            ApplyExplosionAnimation(explosion.end, "ExplosionEnd");

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ExplosionPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] BombExplosion.prefab now uses the 8-frame sheet animations.");
    }

    public static void ApplyPierceBombSprites()
    {
        ConfigureSheet();

        Sprite large = LoadSprite("PierceBombLarge");
        Sprite medium = LoadSprite("PierceBombMedium");
        Sprite small = LoadSprite("PierceBombSmall");

        if (large == null || medium == null || small == null)
            throw new InvalidOperationException("Pierce bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PierceBombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("PierceBomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = medium;
            renderer.animationSprite = new[] { medium, large, medium, small };
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = medium;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PierceBombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] PierceBomb.prefab now uses Itens.png cells (20,1), (19,1), (20,1), (21,1).");
    }

    private static void ConfigureSheet()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"Texture importer not available for {SheetPath}.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellSize;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var factories = new SpriteDataProviderFactories();
        factories.Init();

        var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] existingSpriteRects = dataProvider.GetSpriteRects();
        SpriteRect[] spriteRects =
        {
            CreateSpriteRect("BombLarge", 19, 0, existingSpriteRects),
            CreateSpriteRect("BombMedium", 20, 0, existingSpriteRects),
            CreateSpriteRect("BombSmall", 21, 0, existingSpriteRects),
            CreateSpriteRect("PierceBombLarge", 19, 1, existingSpriteRects),
            CreateSpriteRect("PierceBombMedium", 20, 1, existingSpriteRects),
            CreateSpriteRect("PierceBombSmall", 21, 1, existingSpriteRects),
            CreateSpriteRect("ExplosionStartWeak", 2, 17, existingSpriteRects),
            CreateSpriteRect("ExplosionStartMedium", 2, 12, existingSpriteRects),
            CreateSpriteRect("ExplosionStartStrong", 2, 7, existingSpriteRects),
            CreateSpriteRect("ExplosionStartMaximum", 2, 2, existingSpriteRects),
            CreateSpriteRect("ExplosionMiddleWeak", 3, 17, existingSpriteRects),
            CreateSpriteRect("ExplosionMiddleMedium", 3, 12, existingSpriteRects),
            CreateSpriteRect("ExplosionMiddleStrong", 3, 7, existingSpriteRects),
            CreateSpriteRect("ExplosionMiddleMaximum", 3, 2, existingSpriteRects),
            CreateSpriteRect("ExplosionEndWeak", 3, 16, existingSpriteRects),
            CreateSpriteRect("ExplosionEndMedium", 3, 11, existingSpriteRects),
            CreateSpriteRect("ExplosionEndStrong", 3, 6, existingSpriteRects),
            CreateSpriteRect("ExplosionEndMaximum", 3, 1, existingSpriteRects),
            CreateSpriteRect("PierceExplosionStartWeak", 8, 17, existingSpriteRects),
            CreateSpriteRect("PierceExplosionStartMedium", 8, 12, existingSpriteRects),
            CreateSpriteRect("PierceExplosionStartStrong", 8, 7, existingSpriteRects),
            CreateSpriteRect("PierceExplosionStartMaximum", 8, 2, existingSpriteRects),
            CreateSpriteRect("PierceExplosionMiddleWeak", 9, 17, existingSpriteRects),
            CreateSpriteRect("PierceExplosionMiddleMedium", 9, 12, existingSpriteRects),
            CreateSpriteRect("PierceExplosionMiddleStrong", 9, 7, existingSpriteRects),
            CreateSpriteRect("PierceExplosionMiddleMaximum", 9, 2, existingSpriteRects),
            CreateSpriteRect("PierceExplosionEndWeak", 9, 16, existingSpriteRects),
            CreateSpriteRect("PierceExplosionEndMedium", 9, 11, existingSpriteRects),
            CreateSpriteRect("PierceExplosionEndStrong", 9, 6, existingSpriteRects),
            CreateSpriteRect("PierceExplosionEndMaximum", 9, 1, existingSpriteRects)
        };

        dataProvider.SetSpriteRects(spriteRects);

        var nameProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider.SetNameFileIdPairs(spriteRects
            .Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID))
            .ToArray());

        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static void ApplyExplosionAnimation(AnimatedSpriteRenderer renderer, string spritePrefix)
    {
        if (renderer == null)
            throw new InvalidOperationException($"BombExplosion prefab is missing its {spritePrefix} renderer.");

        Sprite[] frames = Enumerable.Range(0, 4)
            .Select(index => LoadSprite($"{spritePrefix}{GetStrengthName(index)}"))
            .ToArray();

        if (frames.Any(sprite => sprite == null))
            throw new InvalidOperationException($"Could not load every frame for {spritePrefix}.");

        renderer.idleSprite = frames[0];
        renderer.animationSprite = new[]
        {
            frames[0], frames[1], frames[2], frames[3],
            frames[3], frames[2], frames[1], frames[0]
        };
        renderer.animationTime = 0.0625f;
        renderer.idle = false;
        renderer.loop = true;

        SpriteRenderer spriteRenderer = renderer.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.sprite = frames[0];
    }

    private static string GetStrengthName(int index)
    {
        return index switch
        {
            0 => "Weak",
            1 => "Medium",
            2 => "Strong",
            3 => "Maximum",
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    private static SpriteRect CreateSpriteRect(string name, int column, int row, SpriteRect[] existingSpriteRects)
    {
        SpriteRect existing = existingSpriteRects.FirstOrDefault(spriteRect => spriteRect.name == name);

        return new SpriteRect
        {
            name = name,
            rect = new Rect(column * CellSize, 320 - ((row + 1) * CellSize), CellSize, CellSize),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = existing != null ? existing.spriteID : GUID.Generate()
        };
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == name);
    }
}
