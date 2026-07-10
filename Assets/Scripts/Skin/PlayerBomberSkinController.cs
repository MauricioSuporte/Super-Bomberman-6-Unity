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
    readonly Dictionary<AnimatedSpriteRenderer, float> baseAnimationTimes = new();

    const float LadyBomberEndStageSpeedMultiplier = 1.5f;
    static readonly int[] WalkFramePattern = { -1, -2, -1, 0, 1, 2, 1, 0 };
    static readonly int[] BombermanAfkFrames = { 126, 125, 124, 125, 126, 127, 128, 127 };
    static readonly int[] LadyBomberAfkFrames = { 122, 121, 120, 119, 118, 121 };
    static readonly int[] TinyBomberAfkFrames =
    {
        126, 125, 124, 125, 124, 125, 124, 125,
        126, 127, 128, 127, 128, 127, 128, 127
    };
    static readonly int[] DismountedAfk2Frames = { 19, 20, 65, 66, 89, 90, 42, 43 };
    static readonly int[] BombermanDeathFrames = BuildSmoothedDeathFrames(BuildDeathFrames(108));
    static readonly int[] LadyBomberDeathFrames = BuildSmoothedDeathFrames(BuildDeathFrames(102));
    static readonly int[] DeathJumpTileHeights = { 0, 0, 1, 2, 3, 2, 1, 0 };
    const float DeathSequenceDuration = 1f;
    const float DeathJumpTileWorldSize = 0.8f;
    const int OriginalDeathFrameCount = 26;
    const int OriginalDeathJumpFrameCount = 8;
    static readonly int[] BombermanEndStageFrames = { 105, 104, 106, 104, 105, 106 };
    static readonly int[] TinyBomberEndStageFrames = { 104, 105, 106, 129, 130, 132, 133, 134, 135, 136 };
    static readonly int[] LadyBomberEndStageFrames =
    {
        124, 125, 126, 127, 128, 129, 130, 131, 132,
        133, 134, 135, 136, 137, 138, 139, 140
    };

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

    readonly struct FrameSequenceDefinition
    {
        public readonly string RendererName;
        public readonly int[] Frames;

        public FrameSequenceDefinition(string rendererName, int[] frames)
        {
            RendererName = rendererName;
            Frames = frames;
        }
    }

    static readonly FrameSequenceDefinition[] PunchDefinitions =
    {
        new("PunchDown", new[] { 21, 22 }),
        new("PunchRight", new[] { 44, 45 }),
        new("PunchLeft", new[] { 67, 68 }),
        new("PunchUp", new[] { 91, 92 })
    };

    public void ApplyFromIdentity()
    {
        var id = GetComponentInParent<PlayerIdentity>(true);
        if (id == null || id.playerId <= 0)
            return;

        int playerId = Mathf.Clamp(id.playerId, 1, 6);
        var state = PlayerPersistentStats.Get(playerId);

        Apply(state.Character, state.Skin);
    }

    public void SetSkin(int playerId, BomberSkin skin)
    {
        var state = PlayerPersistentStats.Get(playerId);
        state.Skin = skin;
        Apply(state.Character, skin);
    }

    public void SetCharacterAndSkin(int playerId, BomberCharacter selectedCharacter, BomberSkin skin)
    {
        var state = PlayerPersistentStats.Get(playerId);
        state.Character = selectedCharacter;
        state.Skin = skin;
        Apply(selectedCharacter, skin);
    }

    public void Apply(BomberSkin skin)
    {
        Apply(character, skin);
    }

    public void Apply(BomberCharacter selectedCharacter, BomberSkin skin)
    {
        if (character != selectedCharacter)
        {
            character = selectedCharacter;
            spritesResourcesPath = BomberSkinResourceCatalog.GetGeneratedResourcesPath(character);
            skinFrameMaps.Clear();
        }
        else if (string.IsNullOrWhiteSpace(spritesResourcesPath) ||
                 spritesResourcesPath != BomberSkinResourceCatalog.GetGeneratedResourcesPath(character))
        {
            spritesResourcesPath = BomberSkinResourceCatalog.GetGeneratedResourcesPath(character);
            skinFrameMaps.Clear();
        }

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

        int[] afkFrames = GetAfkFrames(character);
        ApplyFrameSequence(
            FindAnimatedRenderer("Afk"),
            "Afk",
            afkFrames[0],
            afkFrames,
            targetMap,
            skin
        );

        ApplyFrameSequence(
            FindAnimatedRenderer("Afk2"),
            "Afk2",
            DismountedAfk2Frames[0],
            DismountedAfk2Frames,
            targetMap,
            skin
        );

        int[] deathFrames = GetDeathFrames(character);
        ApplyFrameSequence(
            FindAnimatedRenderer("Death"),
            "Death",
            deathFrames[0],
            deathFrames,
            targetMap,
            skin,
            loop: false,
            sequenceDuration: DeathSequenceDuration,
            frameOffsets: BuildDeathFrameOffsets(deathFrames.Length),
            frameDurations: BuildDeathFrameDurations(deathFrames.Length)
        );

        int[] endStageFrames = GetEndStageFrames(character);
        ApplyFrameSequence(
            FindAnimatedRenderer("EndStage"),
            "EndStage",
            endStageFrames[0],
            endStageFrames,
            targetMap,
            skin,
            loop: ShouldLoopEndStage(character),
            speedMultiplier: GetEndStageSpeedMultiplier(character)
        );

        for (int i = 0; i < PunchDefinitions.Length; i++)
        {
            FrameSequenceDefinition definition = PunchDefinitions[i];
            ApplyFrameSequence(
                FindAnimatedRenderer(definition.RendererName),
                definition.RendererName,
                definition.Frames[0],
                definition.Frames,
                targetMap,
                skin,
                loop: false
            );
        }
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

        if (expectedName == "Right" && actualName == "Rigth")
            return true;

        return expectedName.EndsWith("Right", System.StringComparison.Ordinal) &&
               actualName == expectedName.Replace("Right", "Rigth");
    }

    static int[] GetEndStageFrames(BomberCharacter character)
    {
        return character switch
        {
            BomberCharacter.LadyBomber => LadyBomberEndStageFrames,
            BomberCharacter.TinyBomber => TinyBomberEndStageFrames,
            _ => BombermanEndStageFrames
        };
    }

    static int[] GetAfkFrames(BomberCharacter character)
    {
        return character switch
        {
            BomberCharacter.LadyBomber => LadyBomberAfkFrames,
            BomberCharacter.TinyBomber => TinyBomberAfkFrames,
            _ => BombermanAfkFrames
        };
    }

    static int[] GetDeathFrames(BomberCharacter character)
    {
        return character == BomberCharacter.LadyBomber
            ? LadyBomberDeathFrames
            : BombermanDeathFrames;
    }

    static int[] BuildDeathFrames(int firstFrame)
    {
        const int distinctFrameCount = 16;
        const int alternatingCycles = 3;
        const int finalFrameHoldCount = 6;

        int penultimateFrame = firstFrame + distinctFrameCount - 2;
        int finalFrame = firstFrame + distinctFrameCount - 1;
        int leadingFrameCount = distinctFrameCount - 2;
        int[] frames = new int[
            leadingFrameCount + alternatingCycles * 2 + finalFrameHoldCount];

        int index = 0;
        for (int frame = firstFrame; frame < penultimateFrame; frame++)
            frames[index++] = frame;

        for (int cycle = 0; cycle < alternatingCycles; cycle++)
        {
            frames[index++] = penultimateFrame;
            frames[index++] = finalFrame;
        }

        for (int hold = 0; hold < finalFrameHoldCount; hold++)
            frames[index++] = finalFrame;

        return frames;
    }

    static int[] BuildSmoothedDeathFrames(int[] sourceFrames)
    {
        int jumpFrameCount = Mathf.Min(OriginalDeathJumpFrameCount, sourceFrames.Length);
        int smoothedJumpFrameCount = Mathf.Max(1, jumpFrameCount * 2 - 1);
        int[] frames = new int[smoothedJumpFrameCount + sourceFrames.Length - jumpFrameCount];
        int index = 0;

        for (int i = 0; i < jumpFrameCount - 1; i++)
        {
            frames[index++] = sourceFrames[i];
            frames[index++] = sourceFrames[i];
        }

        frames[index++] = sourceFrames[jumpFrameCount - 1];

        for (int i = jumpFrameCount; i < sourceFrames.Length; i++)
            frames[index++] = sourceFrames[i];

        return frames;
    }

    static Vector2[] BuildDeathFrameOffsets(int frameCount)
    {
        Vector2[] offsets = new Vector2[frameCount];
        int originalJumpFrameCount = DeathJumpTileHeights.Length;
        int index = 0;

        for (int i = 0; i < originalJumpFrameCount - 1 && index < frameCount; i++)
        {
            float currentHeight = DeathJumpTileHeights[i];
            float nextHeight = DeathJumpTileHeights[i + 1];
            offsets[index++] = Vector2.up * (currentHeight * DeathJumpTileWorldSize);

            if (index < frameCount)
            {
                float intermediateHeight = (currentHeight + nextHeight) * 0.5f;
                offsets[index++] = Vector2.up * (intermediateHeight * DeathJumpTileWorldSize);
            }
        }

        if (index < frameCount)
            offsets[index] = Vector2.up * (DeathJumpTileHeights[^1] * DeathJumpTileWorldSize);

        return offsets;
    }

    static float[] BuildDeathFrameDurations(int frameCount)
    {
        float originalFrameDuration = DeathSequenceDuration / OriginalDeathFrameCount;
        float jumpDuration = originalFrameDuration * OriginalDeathJumpFrameCount;
        int smoothedJumpFrameCount = OriginalDeathJumpFrameCount * 2 - 1;
        float smoothedJumpFrameDuration = jumpDuration / smoothedJumpFrameCount;
        float[] durations = new float[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            durations[i] = i < smoothedJumpFrameCount
                ? smoothedJumpFrameDuration
                : originalFrameDuration;
        }

        return durations;
    }

    static bool ShouldLoopEndStage(BomberCharacter character)
    {
        return character == BomberCharacter.LadyBomber;
    }

    static float GetEndStageSpeedMultiplier(BomberCharacter character)
    {
        return character == BomberCharacter.LadyBomber
            ? LadyBomberEndStageSpeedMultiplier
            : 1f;
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
        BomberSkin skin,
        bool loop = true,
        float speedMultiplier = 1f,
        float sequenceDuration = 0f,
        Vector2[] frameOffsets = null,
        float[] frameDurations = null)
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
        renderer.loop = loop;
        renderer.pingPong = false;

        if (sequenceDuration > 0f)
        {
            renderer.useSequenceDuration = true;
            renderer.sequenceDuration = sequenceDuration;
            renderer.animationTime = sequenceDuration / Mathf.Max(1, frames.Length);
        }
        else
        {
            ApplyAnimationSpeed(renderer, speedMultiplier);
        }

        if (frameOffsets != null)
            renderer.frameOffsets = frameOffsets;

        renderer.frameDurations = frameDurations;

        renderer.RefreshFrame();

        if (renderer.TryGetComponent<SpriteRenderer>(out var spriteRenderer) && spriteRenderer != null)
            spriteRenderer.sprite = renderer.idleSprite;

        SLog($"ApplyFrameSequence | skin={skin} renderer={rendererName} idle={idleFrame} frames={string.Join(",", frames)}");
    }

    void ApplyAnimationSpeed(AnimatedSpriteRenderer renderer, float speedMultiplier)
    {
        if (renderer == null)
            return;

        if (!baseAnimationTimes.TryGetValue(renderer, out float baseTime) || baseTime <= 0f)
        {
            baseTime = Mathf.Max(0.0001f, renderer.animationTime);
            baseAnimationTimes[renderer] = baseTime;
        }

        renderer.animationTime = baseTime / Mathf.Max(0.0001f, speedMultiplier);
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
