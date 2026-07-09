using System.Collections.Generic;
using UnityEngine;

public class PlayerBomberSkinController : MonoBehaviour
{
    const string LogPrefix = "[PlayerBomberSkinController]";

    [Header("Sprite Settings")]
    [SerializeField] private BomberCharacter character = BomberCharacter.Bomberman;
    [SerializeField] private string spritesResourcesPath = BomberSkinResourceCatalog.BombermanGeneratedResourcesPath;
    [SerializeField] private bool enableSurgicalLogs;

    readonly Dictionary<BomberSkin, Dictionary<int, Sprite>> skinFrameMaps = new();

    static readonly int[] WalkFramePattern = { -1, -2, -1, 0, 1, 2, 1, 0 };
    static readonly int[] EndStageFrames = { 105, 104, 106, 104, 105, 106 };

    readonly struct WalkDefinition
    {
        public readonly string RendererName;
        public readonly int IdleFrame;

        public WalkDefinition(string rendererName, int idleFrame)
        {
            RendererName = rendererName;
            IdleFrame = idleFrame;
        }
    }

    static readonly WalkDefinition[] WalkDefinitions =
    {
        new("Down", 2),
        new("Right", 25),
        new("Left", 48),
        new("Up", 72)
    };

    public void ApplyFromIdentity()
    {
        var id = GetComponentInParent<PlayerIdentity>(true);
        if (id == null || id.playerId <= 0)
            return;

        int playerId = Mathf.Clamp(id.playerId, 1, 6);
        var skin = PlayerPersistentStats.Get(playerId).Skin;

        Apply(skin);
    }

    public void SetSkin(int playerId, BomberSkin skin)
    {
        PlayerPersistentStats.Get(playerId).Skin = skin;
        Apply(skin);
    }

    public void Apply(BomberSkin skin)
    {
        skin = BomberSkinResourceCatalog.NormalizeGeneratedSkin(character, skin);
        EnsureCache(skin);

        if (!skinFrameMaps.TryGetValue(skin, out var targetMap) || targetMap.Count == 0)
        {
            SLog($"Apply skipped | skin={skin} no sprites loaded from {spritesResourcesPath}");
            return;
        }

        for (int i = 0; i < WalkDefinitions.Length; i++)
        {
            WalkDefinition definition = WalkDefinitions[i];
            AnimatedSpriteRenderer renderer = FindAnimatedRenderer(definition.RendererName);
            ApplyWalkDefinition(renderer, definition, targetMap, skin);
        }

        ApplyFrameSequence(
            FindAnimatedRenderer("EndStage"),
            "EndStage",
            EndStageFrames[0],
            EndStageFrames,
            targetMap,
            skin
        );
    }

    bool IsInsideMountedLouie(Component c)
    {
        return c != null && c.GetComponentInParent<MountVisualController>(true) != null;
    }

    void EnsureCache(BomberSkin skin)
    {
        if (skinFrameMaps.ContainsKey(skin))
            return;

        skinFrameMaps[skin] = BuildFrameMap(BomberSkinResourceCatalog.GetSheetName(character, skin));
    }

    Dictionary<int, Sprite> BuildFrameMap(string sheetName)
    {
        var map = new Dictionary<int, Sprite>(256);

        string sheetPath = $"{spritesResourcesPath}/{sheetName}";
        var sprites = Resources.LoadAll<Sprite>(sheetPath);

        SLog($"BuildFrameMap | sheetPath={sheetPath} sprites={(sprites != null ? sprites.Length : 0)}");

        if (sprites == null || sprites.Length == 0)
            return map;

        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            if (s == null) continue;

            if (!TryExtractFrameIndex(s.name, out int frameIndex))
                continue;

            if (!map.ContainsKey(frameIndex))
                map.Add(frameIndex, s);
        }

        return map;
    }

    AnimatedSpriteRenderer FindAnimatedRenderer(string rendererName)
    {
        var animated = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < animated.Length; i++)
        {
            AnimatedSpriteRenderer renderer = animated[i];
            if (renderer == null)
                continue;

            if (!IsRendererNameMatch(renderer.gameObject.name, rendererName))
                continue;

            if (IsInsideMountedLouie(renderer))
                continue;

            return renderer;
        }

        return null;
    }

    static bool IsRendererNameMatch(string actualName, string expectedName)
    {
        if (actualName == expectedName)
            return true;

        return expectedName == "Right" && actualName == "Rigth";
    }

    void ApplyWalkDefinition(
        AnimatedSpriteRenderer renderer,
        WalkDefinition definition,
        Dictionary<int, Sprite> targetMap,
        BomberSkin skin)
    {
        if (renderer == null)
        {
            SLog($"ApplyWalkDefinition skipped | skin={skin} renderer={definition.RendererName} missing");
            return;
        }

        if (!targetMap.TryGetValue(definition.IdleFrame, out Sprite idleSprite))
        {
            SLog($"ApplyWalkDefinition skipped | skin={skin} renderer={definition.RendererName} idleFrame={definition.IdleFrame} missing");
            return;
        }

        Sprite[] animation = new Sprite[WalkFramePattern.Length];
        for (int i = 0; i < WalkFramePattern.Length; i++)
        {
            int frame = definition.IdleFrame + WalkFramePattern[i];
            if (!targetMap.TryGetValue(frame, out Sprite sprite))
            {
                SLog($"ApplyWalkDefinition skipped | skin={skin} renderer={definition.RendererName} frame={frame} missing");
                return;
            }

            animation[i] = sprite;
        }

        renderer.idleSprite = idleSprite;
        renderer.animationSprite = animation;
        renderer.loop = true;
        renderer.pingPong = false;
        renderer.RefreshFrame();

        if (renderer.TryGetComponent<SpriteRenderer>(out var spriteRenderer) && spriteRenderer != null)
            spriteRenderer.sprite = renderer.idleSprite;

        SLog(
            $"ApplyWalkDefinition | skin={skin} renderer={definition.RendererName} " +
            $"idle={definition.IdleFrame} frames={string.Join(",", GetWalkFrames(definition.IdleFrame))}"
        );
    }

    void ApplyFrameSequence(
        AnimatedSpriteRenderer renderer,
        string rendererName,
        int idleFrame,
        int[] frames,
        Dictionary<int, Sprite> targetMap,
        BomberSkin skin)
    {
        if (renderer == null)
        {
            SLog($"ApplyFrameSequence skipped | skin={skin} renderer={rendererName} missing");
            return;
        }

        if (!targetMap.TryGetValue(idleFrame, out Sprite idleSprite))
        {
            SLog($"ApplyFrameSequence skipped | skin={skin} renderer={rendererName} idleFrame={idleFrame} missing");
            return;
        }

        Sprite[] animation = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            int frame = frames[i];
            if (!targetMap.TryGetValue(frame, out Sprite sprite))
            {
                SLog($"ApplyFrameSequence skipped | skin={skin} renderer={rendererName} frame={frame} missing");
                return;
            }

            animation[i] = sprite;
        }

        renderer.idleSprite = idleSprite;
        renderer.animationSprite = animation;
        renderer.loop = true;
        renderer.pingPong = false;
        renderer.RefreshFrame();

        if (renderer.TryGetComponent<SpriteRenderer>(out var spriteRenderer) && spriteRenderer != null)
            spriteRenderer.sprite = renderer.idleSprite;

        SLog($"ApplyFrameSequence | skin={skin} renderer={rendererName} idle={idleFrame} frames={string.Join(",", frames)}");
    }

    static int[] GetWalkFrames(int idleFrame)
    {
        int[] frames = new int[WalkFramePattern.Length];
        for (int i = 0; i < WalkFramePattern.Length; i++)
            frames[i] = idleFrame + WalkFramePattern[i];

        return frames;
    }

    static bool TryExtractFrameIndex(string spriteName, out int frameIndex)
    {
        frameIndex = -1;

        if (string.IsNullOrWhiteSpace(spriteName))
            return false;

        int idx = spriteName.LastIndexOf('_');
        if (idx < 0 || idx >= spriteName.Length - 1)
            return false;

        return int.TryParse(spriteName[(idx + 1)..], out frameIndex);
    }

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LogPrefix} {message}", this);
    }

    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(spritesResourcesPath) ||
            spritesResourcesPath == "Sprites/Bombers/Bomberman")
        {
            spritesResourcesPath = BomberSkinResourceCatalog.GetGeneratedResourcesPath(character);
        }
    }
}
