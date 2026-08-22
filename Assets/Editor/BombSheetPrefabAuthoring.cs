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
    private const string RubberBombPrefabPath = "Assets/Prefabs/Bombs/RubberBomb.prefab";
    private const string PowerBombPrefabPath = "Assets/Prefabs/Bombs/PowerBomb.prefab";
    private const string ControlBombPrefabPath = "Assets/Prefabs/Bombs/ControlBomb.prefab";
    private const string MagnetBombPrefabPath = "Assets/Prefabs/Bombs/MagnetBomb.prefab";
    private const string ExplosionPrefabPath = "Assets/Resources/Explosions/BombExplosion.prefab";
    private const int CellSize = 16;

    [MenuItem("Tools/Super Bomberman 6/Apply new Sheet Sprites")]
    public static void ApplyAllBombAndExplosionSheetSprites()
    {
        ApplyNormalBombSprites();
        ApplyBombExplosionSprites();
        ApplyPierceBombSprites();
        ApplyRubberBombSprites();
        ApplyPowerBombSprites();
        ApplyControlBombSprites();
        ApplyMagnetBombSprites();

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

    public static void ApplyRubberBombSprites()
    {
        ConfigureSheet();

        Sprite large = LoadSprite("RubberBombLarge");
        Sprite medium = LoadSprite("RubberBombMedium");
        Sprite small = LoadSprite("RubberBombSmall");

        if (large == null || medium == null || small == null)
            throw new InvalidOperationException("Rubber bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(RubberBombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("RubberBomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = medium;
            renderer.animationSprite = new[] { medium, large, medium, small };
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = medium;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, RubberBombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] RubberBomb.prefab now uses Itens.png cells (24,0), (23,0), (24,0), (25,0).");
    }

    public static void ApplyPowerBombSprites()
    {
        ConfigureSheet();

        Sprite large = LoadSprite("PowerBombLarge");
        Sprite medium = LoadSprite("PowerBombMedium");
        Sprite small = LoadSprite("PowerBombSmall");

        if (large == null || medium == null || small == null)
            throw new InvalidOperationException("Power bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PowerBombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("PowerBomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = medium;
            renderer.animationSprite = new[] { medium, large, medium, small };
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = medium;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PowerBombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] PowerBomb.prefab now uses Itens.png cells (20,2), (19,2), (20,2), (21,2).");
    }

    public static void ApplyControlBombSprites()
    {
        ConfigureSheet();

        Sprite[] frames =
        {
            LoadSprite("ControlBombFrame1"),
            LoadSprite("ControlBombFrame2"),
            LoadSprite("ControlBombFrame3"),
            LoadSprite("ControlBombFrame4")
        };

        if (frames.Any(sprite => sprite == null))
            throw new InvalidOperationException("Control bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ControlBombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("ControlBomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = frames[0];
            renderer.animationSprite = frames;
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = frames[0];

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ControlBombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] ControlBomb.prefab now uses Itens.png frames (22,2), (23,2), (24,2), (25,2).");
    }

    public static void ApplyMagnetBombSprites()
    {
        ConfigureSheet();

        Sprite large = LoadSprite("MagnetBombLarge");
        Sprite medium = LoadSprite("MagnetBombMedium");
        Sprite small = LoadSprite("MagnetBombSmall");

        if (large == null || medium == null || small == null)
            throw new InvalidOperationException("Magnet bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(MagnetBombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("MagnetBomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = medium;
            renderer.animationSprite = new[] { medium, large, medium, small };
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = medium;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, MagnetBombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BombSheetPrefabAuthoring] MagnetBomb.prefab now uses Itens.png cells (26,4), (25,4), (26,4), (27,4).");
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
            CreateSpriteRect("RubberBombLarge", 23, 0, existingSpriteRects),
            CreateSpriteRect("RubberBombMedium", 24, 0, existingSpriteRects),
            CreateSpriteRect("RubberBombSmall", 25, 0, existingSpriteRects),
            CreateSpriteRect("PowerBombLarge", 19, 2, existingSpriteRects),
            CreateSpriteRect("PowerBombMedium", 20, 2, existingSpriteRects),
            CreateSpriteRect("PowerBombSmall", 21, 2, existingSpriteRects),
            CreateSpriteRect("ControlBombFrame1", 22, 2, existingSpriteRects),
            CreateSpriteRect("ControlBombFrame2", 23, 2, existingSpriteRects),
            CreateSpriteRect("ControlBombFrame3", 24, 2, existingSpriteRects),
            CreateSpriteRect("ControlBombFrame4", 25, 2, existingSpriteRects),
            CreateSpriteRect("MagnetBombLarge", 25, 4, existingSpriteRects),
            CreateSpriteRect("MagnetBombMedium", 26, 4, existingSpriteRects),
            CreateSpriteRect("MagnetBombSmall", 27, 4, existingSpriteRects),
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
