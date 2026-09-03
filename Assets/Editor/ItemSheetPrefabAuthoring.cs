using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ItemSheetPrefabAuthoring
{
    private const string SheetPath = "Assets/Resources/Sprites/BombItems/Itens.png";
    private const string BombPrefabPath = "Assets/Prefabs/Bombs/Bomb.prefab";
    private const string PierceBombPrefabPath = "Assets/Prefabs/Bombs/PierceBomb.prefab";
    private const string RubberBombPrefabPath = "Assets/Prefabs/Bombs/RubberBomb.prefab";
    private const string PowerBombPrefabPath = "Assets/Prefabs/Bombs/PowerBomb.prefab";
    private const string ControlBombPrefabPath = "Assets/Prefabs/Bombs/ControlBomb.prefab";
    private const string MagnetBombPrefabPath = "Assets/Prefabs/Bombs/MagnetBomb.prefab";
    private const string RevengeBombPrefabPath = "Assets/Prefabs/Bombs/MadBomberBomb.prefab";
    private const string ExplosionPrefabPath = "Assets/Resources/Explosions/BombExplosion.prefab";
    private const int CellSize = 16;
    private const int ItemIconSize = 14;
    private const float ItemBorderFrameSeconds = 0.025f;
    private static Dictionary<string, Sprite> spritesByName;
    private static readonly ItemIconDefinition[] ItemIcons =
    {
        new("Assets/Resources/Items/ExtraBomb.prefab", "ExtraBomb", "ExtraBomb", 32, 0),
        new("Assets/Resources/Items/BlastRadius.prefab", "BlastRadius", "BlastRadius", 33, 0),
        new("Assets/Resources/Items/SpeedIncrese.prefab", "SpeedUp", "SpeedUp", 34, 0),
        new("Assets/Resources/Items/1-Up.prefab", "OneUp", "1-Up", 33, 5),
        new("Assets/Resources/Items/BombKick.prefab", "BombKick", "BombKick", 38, 3),
        new("Assets/Resources/Items/BombPass.prefab", "BombPass", "BombPass", 41, 0),
        new("Assets/Resources/Items/BombPunch.prefab", "BombPunch", "BombPunch", 40, 3),
        new("Assets/Resources/Items/ControlBomb.prefab", "ControlBomb", "ControlBomb", 37, 1),
        new("Assets/Resources/Items/DestructiblePass.prefab", "DestructiblePass", "DestructiblePass", 32, 1),
        new("Assets/Resources/Items/FullFire.prefab", "FullFire", "FullFire", 32, 5, false),
        new("Assets/Resources/Items/Heart.prefab", "Heart", "Heart", 33, 1),
        new("Assets/Resources/Items/InvincibleSuit.prefab", "InvincibleSuit", "InvincibleSuit", 36, 1),
        new("Assets/Resources/Items/MagnetBomb.prefab", "MagnetBomb", "MagnetBomb", 38, 2),
        new("Assets/Resources/Items/PierceBomb.prefab", "PierceBomb", "PierceBomb", 38, 1),
        new("Assets/Resources/Items/PowerBomb.prefab", "PowerBomb", "PowerBomb", 40, 1),
        new("Assets/Resources/Items/PowerGlove.prefab", "PowerGlove", "PowerGlove", 41, 3),
        new("Assets/Resources/Items/RubberBomb.prefab", "RubberBomb", "RubberBomb", 39, 1),
        new("Assets/Resources/Items/Skull.prefab", "Skull", "Skull", 34, 1),
        new("Assets/Resources/Items/Clock.prefab", "Clock", "Clock", 40, 6)
    };

    [MenuItem("Tools/Sprites/Apply new Sheet Sprites")]
    public static void ApplyNewSheetSprites()
    {
        spritesByName = null;
        ConfigureSheet();
        spritesByName = null;
        bool prefabsChanged = false;

        prefabsChanged |= ApplyNormalBombSpritesIfNeeded();
        prefabsChanged |= ApplyBombExplosionSpritesIfNeeded();
        prefabsChanged |= ApplyPierceBombSpritesIfNeeded();
        prefabsChanged |= ApplyRubberBombSpritesIfNeeded();
        prefabsChanged |= ApplyPowerBombSpritesIfNeeded();
        prefabsChanged |= ApplyControlBombSpritesIfNeeded();
        prefabsChanged |= ApplyMagnetBombSpritesIfNeeded();
        prefabsChanged |= ApplyRevengeBombSpritesIfNeeded();
        foreach (ItemIconDefinition item in ItemIcons)
            prefabsChanged |= ApplyItemIconIfNeeded(item);

        AssetDatabase.SaveAssets();
        Debug.Log(prefabsChanged
            ? "[ItemSheetPrefabAuthoring] Configured Itens.png and updated the affected prefabs."
            : "[ItemSheetPrefabAuthoring] Configured Itens.png; every prefab already referenced its sheet sprites.");
    }

    private static bool ApplyItemIconIfNeeded(ItemIconDefinition item)
    {
        Sprite icon = LoadSprite($"{item.spriteName}Icon");
        Sprite[] borderFrames = item.usesAnimatedBorder
            ? Enumerable.Range(32, 6)
                .Select(borderColumn => LoadSprite($"ItemBorder{borderColumn - 31}"))
                .ToArray()
            : null;

        if (icon == null || (item.usesAnimatedBorder && borderFrames.Any(sprite => sprite == null)))
            throw new InvalidOperationException($"{item.displayName} icon or border sprite cells could not be loaded from Itens.png.");

        if (IsItemIconConfigured(item, icon, borderFrames))
            return false;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(item.prefabPath);
        try
        {
            AnimatedSpriteRenderer iconRenderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            SpriteRenderer iconSpriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (iconRenderer == null || iconSpriteRenderer == null)
                throw new InvalidOperationException($"{item.displayName}.prefab is missing its icon renderer.");

            iconRenderer.idleSprite = icon;
            iconRenderer.animationSprite = new[] { icon };
            iconRenderer.idle = true;
            iconRenderer.loop = true;
            iconSpriteRenderer.sprite = icon;

            Transform borderTransform = prefabRoot.transform.Find("BorderAnimation");
            if (!item.usesAnimatedBorder)
            {
                if (borderTransform != null)
                    UnityEngine.Object.DestroyImmediate(borderTransform.gameObject);
            }
            else
            {
                GameObject borderObject;
                if (borderTransform == null)
                {
                    borderObject = new GameObject("BorderAnimation");
                    borderObject.transform.SetParent(prefabRoot.transform, false);
                }
                else
                {
                    borderObject = borderTransform.gameObject;
                }

                borderObject.layer = prefabRoot.layer;
                borderObject.transform.localPosition = Vector3.zero;
                borderObject.transform.localRotation = Quaternion.identity;
                borderObject.transform.localScale = Vector3.one;

                SpriteRenderer borderSpriteRenderer = borderObject.GetComponent<SpriteRenderer>();
                if (borderSpriteRenderer == null)
                    borderSpriteRenderer = borderObject.AddComponent<SpriteRenderer>();

                borderSpriteRenderer.sprite = borderFrames[0];
                borderSpriteRenderer.sortingLayerID = iconSpriteRenderer.sortingLayerID;
                borderSpriteRenderer.sortingOrder = iconSpriteRenderer.sortingOrder - 1;

                AnimatedSpriteRenderer borderRenderer = borderObject.GetComponent<AnimatedSpriteRenderer>();
                if (borderRenderer == null)
                    borderRenderer = borderObject.AddComponent<AnimatedSpriteRenderer>();

                borderRenderer.idleSprite = borderFrames[0];
                borderRenderer.animationSprite = borderFrames;
                borderRenderer.animationTime = ItemBorderFrameSeconds;
                borderRenderer.useSequenceDuration = false;
                borderRenderer.loop = true;
                borderRenderer.idle = false;
                borderRenderer.pingPong = false;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, item.prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        string borderDescription = item.usesAnimatedBorder
            ? " and six border frames at (32-37,8)"
            : " without an animated border";
        Debug.Log($"[ItemSheetPrefabAuthoring] {item.displayName}.prefab now uses the centered 14x14 icon at ({item.column},{item.row}){borderDescription}.");
        return true;
    }

    private static bool ApplyNormalBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(BombPrefabPath, false, "BombMedium", "BombLarge", "BombMedium", "BombSmall"))
            return false;

        ApplyNormalBombSprites();
        return true;
    }

    private static void ApplyNormalBombSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] Bomb.prefab now uses Itens.png cells (20,0), (19,0), (20,0), (21,0).");
    }

    private static bool ApplyBombExplosionSpritesIfNeeded()
    {
        if (IsExplosionPrefabConfigured())
            return false;

        ApplyBombExplosionSprites();
        return true;
    }

    private static void ApplyBombExplosionSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] BombExplosion.prefab now uses the 8-frame sheet animations.");
    }

    private static bool ApplyPierceBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(PierceBombPrefabPath, false, "PierceBombMedium", "PierceBombLarge", "PierceBombMedium", "PierceBombSmall"))
            return false;

        ApplyPierceBombSprites();
        return true;
    }

    private static void ApplyPierceBombSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] PierceBomb.prefab now uses Itens.png cells (20,1), (19,1), (20,1), (21,1).");
    }

    private static bool ApplyRubberBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(RubberBombPrefabPath, false, "RubberBombMedium", "RubberBombLarge", "RubberBombMedium", "RubberBombSmall"))
            return false;

        ApplyRubberBombSprites();
        return true;
    }

    private static void ApplyRubberBombSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] RubberBomb.prefab now uses Itens.png cells (24,0), (23,0), (24,0), (25,0).");
    }

    private static bool ApplyPowerBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(PowerBombPrefabPath, false, "PowerBombMedium", "PowerBombLarge", "PowerBombMedium", "PowerBombSmall"))
            return false;

        ApplyPowerBombSprites();
        return true;
    }

    private static void ApplyPowerBombSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] PowerBomb.prefab now uses Itens.png cells (20,2), (19,2), (20,2), (21,2).");
    }

    private static bool ApplyControlBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(ControlBombPrefabPath, false, "ControlBombFrame1", "ControlBombFrame2", "ControlBombFrame3", "ControlBombFrame4"))
            return false;

        ApplyControlBombSprites();
        return true;
    }

    private static void ApplyControlBombSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] ControlBomb.prefab now uses Itens.png frames (22,2), (23,2), (24,2), (25,2).");
    }

    private static bool ApplyMagnetBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(MagnetBombPrefabPath, false, "MagnetBombMedium", "MagnetBombLarge", "MagnetBombMedium", "MagnetBombSmall"))
            return false;

        ApplyMagnetBombSprites();
        return true;
    }

    private static void ApplyMagnetBombSprites()
    {
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

        Debug.Log("[ItemSheetPrefabAuthoring] MagnetBomb.prefab now uses Itens.png cells (26,4), (25,4), (26,4), (27,4).");
    }

    private static bool ApplyRevengeBombSpritesIfNeeded()
    {
        if (IsAnimatedSpritePrefabConfigured(RevengeBombPrefabPath, false, "RevengeBombMedium", "RevengeBombLarge", "RevengeBombMedium", "RevengeBombSmall"))
            return false;

        ApplyRevengeBombSprites();
        return true;
    }

    private static void ApplyRevengeBombSprites()
    {
        Sprite large = LoadSprite("RevengeBombLarge");
        Sprite medium = LoadSprite("RevengeBombMedium");
        Sprite small = LoadSprite("RevengeBombSmall");

        if (large == null || medium == null || small == null)
            throw new InvalidOperationException("Revenge bomb sprite cells could not be loaded from Itens.png.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(RevengeBombPrefabPath);
        try
        {
            AnimatedSpriteRenderer renderer = prefabRoot.GetComponent<AnimatedSpriteRenderer>();
            if (renderer == null)
                throw new InvalidOperationException("MadBomberBomb.prefab is missing AnimatedSpriteRenderer.");

            renderer.idleSprite = medium;
            renderer.animationSprite = new[] { medium, large, medium, small };
            renderer.idle = false;
            renderer.loop = true;

            SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = medium;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, RevengeBombPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        Debug.Log("[ItemSheetPrefabAuthoring] MadBomberBomb.prefab now uses Itens.png cells (28,11), (27,11), (28,11), (29,11).");
    }

    private static bool IsAnimatedSpritePrefabConfigured(string prefabPath, bool idle, params string[] frameNames)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        AnimatedSpriteRenderer renderer = prefab != null ? prefab.GetComponent<AnimatedSpriteRenderer>() : null;
        if (renderer == null)
            return false;

        Sprite[] expectedFrames = frameNames.Select(LoadSprite).ToArray();
        return expectedFrames.All(sprite => sprite != null) &&
               renderer.idle == idle &&
               renderer.loop &&
               renderer.idleSprite == expectedFrames[0] &&
               HasMatchingFrames(renderer.animationSprite, expectedFrames);
    }

    private static bool IsExplosionPrefabConfigured()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefabPath);
        BombExplosion explosion = prefab != null ? prefab.GetComponent<BombExplosion>() : null;
        return explosion != null &&
               IsExplosionRendererConfigured(explosion.start, "ExplosionStart") &&
               IsExplosionRendererConfigured(explosion.middle, "ExplosionMiddle") &&
               IsExplosionRendererConfigured(explosion.end, "ExplosionEnd");
    }

    private static bool IsExplosionRendererConfigured(AnimatedSpriteRenderer renderer, string spritePrefix)
    {
        if (renderer == null)
            return false;

        Sprite[] baseFrames = Enumerable.Range(0, 4)
            .Select(index => LoadSprite($"{spritePrefix}{GetStrengthName(index)}"))
            .ToArray();
        Sprite[] expectedFrames =
        {
            baseFrames[0], baseFrames[1], baseFrames[2], baseFrames[3],
            baseFrames[3], baseFrames[2], baseFrames[1], baseFrames[0]
        };

        return baseFrames.All(sprite => sprite != null) &&
               !renderer.idle &&
               renderer.loop &&
               renderer.idleSprite == baseFrames[0] &&
               HasMatchingFrames(renderer.animationSprite, expectedFrames);
    }

    private static bool IsItemIconConfigured(ItemIconDefinition item, Sprite icon, Sprite[] borderFrames)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.prefabPath);
        if (prefab == null)
            return false;

        AnimatedSpriteRenderer iconRenderer = prefab.GetComponent<AnimatedSpriteRenderer>();
        SpriteRenderer iconSpriteRenderer = prefab.GetComponent<SpriteRenderer>();
        Transform borderTransform = prefab.transform.Find("BorderAnimation");
        if (iconRenderer == null || iconSpriteRenderer == null)
            return false;

        bool isIconConfigured = iconRenderer.idle &&
                                iconRenderer.loop &&
                                iconRenderer.idleSprite == icon &&
                                HasMatchingFrames(iconRenderer.animationSprite, new[] { icon }) &&
                                iconSpriteRenderer.sprite == icon;
        if (!isIconConfigured)
            return false;

        if (!item.usesAnimatedBorder)
            return borderTransform == null;

        if (borderTransform == null)
            return false;

        AnimatedSpriteRenderer borderRenderer = borderTransform.GetComponent<AnimatedSpriteRenderer>();
        SpriteRenderer borderSpriteRenderer = borderTransform.GetComponent<SpriteRenderer>();
        return borderRenderer != null &&
               borderSpriteRenderer != null &&
               !borderRenderer.idle &&
               borderRenderer.loop &&
               !borderRenderer.pingPong &&
               Mathf.Approximately(borderRenderer.animationTime, ItemBorderFrameSeconds) &&
               borderRenderer.idleSprite == borderFrames[0] &&
               HasMatchingFrames(borderRenderer.animationSprite, borderFrames) &&
               borderSpriteRenderer.sprite == borderFrames[0] &&
               borderSpriteRenderer.sortingLayerID == iconSpriteRenderer.sortingLayerID &&
               borderSpriteRenderer.sortingOrder == iconSpriteRenderer.sortingOrder - 1;
    }

    private static bool HasMatchingFrames(Sprite[] actualFrames, Sprite[] expectedFrames)
    {
        return actualFrames != null &&
               actualFrames.Length == expectedFrames.Length &&
               actualFrames.SequenceEqual(expectedFrames);
    }

    private readonly struct ItemIconDefinition
    {
        public readonly string prefabPath;
        public readonly string spriteName;
        public readonly string displayName;
        public readonly int column;
        public readonly int row;
        public readonly bool usesAnimatedBorder;

        public ItemIconDefinition(string prefabPath, string spriteName, string displayName, int column, int row, bool usesAnimatedBorder = true)
        {
            this.prefabPath = prefabPath;
            this.spriteName = spriteName;
            this.displayName = displayName;
            this.column = column;
            this.row = row;
            this.usesAnimatedBorder = usesAnimatedBorder;
        }
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
            CreateSpriteRect("RevengeBombLarge", 27, 11, existingSpriteRects),
            CreateSpriteRect("RevengeBombMedium", 28, 11, existingSpriteRects),
            CreateSpriteRect("RevengeBombSmall", 29, 11, existingSpriteRects),
            CreateSpriteRect("ExtraBombIcon", 32, 0, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("BlastRadiusIcon", 33, 0, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("SpeedUpIcon", 34, 0, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("OneUpIcon", 33, 5, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("BombKickIcon", 38, 3, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("BombPassIcon", 41, 0, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("BombPunchIcon", 40, 3, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("ControlBombIcon", 37, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("DestructiblePassIcon", 32, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("FullFireIcon", 32, 5, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("HeartIcon", 33, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("InvincibleSuitIcon", 36, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("MagnetBombIcon", 38, 2, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("PierceBombIcon", 38, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("PowerBombIcon", 40, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("PowerGloveIcon", 41, 3, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("RubberBombIcon", 39, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("SkullIcon", 34, 1, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("ClockIcon", 40, 6, existingSpriteRects, ItemIconSize, 1),
            CreateSpriteRect("ItemBorder1", 32, 8, existingSpriteRects),
            CreateSpriteRect("ItemBorder2", 33, 8, existingSpriteRects),
            CreateSpriteRect("ItemBorder3", 34, 8, existingSpriteRects),
            CreateSpriteRect("ItemBorder4", 35, 8, existingSpriteRects),
            CreateSpriteRect("ItemBorder5", 36, 8, existingSpriteRects),
            CreateSpriteRect("ItemBorder6", 37, 8, existingSpriteRects),
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

    private static SpriteRect CreateSpriteRect(
        string name,
        int column,
        int row,
        SpriteRect[] existingSpriteRects,
        int size = CellSize,
        int inset = 0)
    {
        Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        if (sheet == null)
            throw new InvalidOperationException($"Sprite sheet not available at {SheetPath}.");

        SpriteRect existing = existingSpriteRects.FirstOrDefault(spriteRect => spriteRect.name == name);

        return new SpriteRect
        {
            name = name,
            rect = new Rect(
                (column * CellSize) + inset,
                sheet.height - ((row + 1) * CellSize) + inset,
                size,
                size),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = existing != null ? existing.spriteID : GUID.Generate()
        };
    }

    private static Sprite LoadSprite(string name)
    {
        if (spritesByName == null)
        {
            spritesByName = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .GroupBy(sprite => sprite.name)
                .ToDictionary(group => group.Key, group => group.First());
        }

        return spritesByName.TryGetValue(name, out Sprite sprite) ? sprite : null;
    }
}
