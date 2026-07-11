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
    // Sprite-sheet animation indices. Update this block when the shared bomber template changes.
    static readonly int[] BombermanAfkFrames = { 130, 129, 128, 129, 130, 131, 132, 131 };
    static readonly int[] LadyBomberAfkFrames = { 126, 125, 124, 123, 122, 125 };
    static readonly int[] TinyBomberAfkFrames =
    {
        130, 129, 128, 129, 128, 129, 128, 129,
        130, 131, 132, 131, 132, 131, 132, 131
    };
    static readonly int[] MinerBomberAfkFrames = { 128, 129, 130, 131, 132, 132, 131, 130, 129 };
    static readonly int[] DismountedAfk2Frames = { 19, 20, 65, 66, 89, 90, 42, 43 };
    static readonly int[] BombermanCorneredFrames = { 103, 104, 105, 106, 107, 105, 106, 107 };
    static readonly int[] LadyBomberCorneredFrames = { 100, 101, 102, 103, 104, 102, 103, 104 };
    const int CorneredLoopStartFrame = 5;
    static readonly int[] BombermanDeathFrames = BuildSmoothedDeathFrames(BuildDeathFrames(112));
    static readonly int[] LadyBomberDeathFrames = BuildSmoothedDeathFrames(BuildDeathFrames(106));
    static readonly int[] DeathJumpTileHeights = { 0, 0, 1, 2, 3, 2, 1, 0 };
    const float DeathSequenceDuration = 1f;
    const float DeathJumpTileWorldSize = 0.8f;
    const int OriginalDeathFrameCount = 26;
    const int OriginalDeathJumpFrameCount = 8;
    static readonly int[] BombermanEndStageFrames = { 108, 109, 108, 110, 108, 109, 108, 110 };
    static readonly int[] TinyBomberEndStageFrames = { 108, 109, 110, 133, 134, 136, 137, 138, 139, 140 };
    static readonly int[] LadyBomberEndStageFrames =
    {
        128, 129, 130, 131, 132, 133, 134, 135, 136,
        137, 138, 139, 140, 141, 142, 143, 144
    };
    static readonly int[] MinerBomberEndStageFrames = { 2, 108, 109, 110, 133 };

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

    static readonly FrameSequenceDefinition[] PowerGlovePickupDefinitions =
    {
        new("PickBombDown", new[] { 7, 6, 5 }),
        new("PickBombRight", new[] { 31, 30, 29, 28 }),
        new("PickBombLeft", new[] { 54, 53, 52, 51 }),
        new("PickBombUp", new[] { 78, 77, 76, 75 })
    };

    static readonly FrameSequenceDefinition[] PowerGloveCarryDefinitions =
    {
        new("CarryBombDown", new[] { 8, 5, 9, 5 }),
        new("CarryBombRight", new[] { 32, 28, 33, 28 }),
        new("CarryBombLeft", new[] { 55, 51, 56, 51 }),
        new("CarryBombUp", new[] { 79, 75, 80, 75 })
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

        int[] stunFrames = character == BomberCharacter.LadyBomber
            ? new[] { 96, 97, 98, 99 }
            : new[] { 99, 100, 101, 102 };
        ApplyFrameSequence(FindAnimatedRenderer("Stun"), "Stun", stunFrames[0], stunFrames, targetMap, skin, loop: true);
        ApplySingleFrame(FindAnimatedRenderer("HeadOnlyDown"), "HeadOnlyDown", GetLastFrame(targetMap, 0), targetMap, skin);
        ApplySingleFrame(FindAnimatedRenderer("HeadOnlyLeft"), "HeadOnlyLeft", GetLastFrame(targetMap, 1), targetMap, skin);
        ApplySingleFrame(FindAnimatedRenderer("HeadOnlyRight"), "HeadOnlyRight", GetLastFrame(targetMap, 2), targetMap, skin);
        ApplySingleFrame(FindAnimatedRenderer("HeadOnlyUp"), "HeadOnlyUp", GetLastFrame(targetMap, 3), targetMap, skin);

        ApplyDirectionFrames("MountAscend", new[] { 16, 39, 62, 86 }, targetMap, skin);
        ApplyDirectionFrames("MountDescend", new[] { 17, 40, 63, 87 }, targetMap, skin);
        ApplyDirectionFrames("Mounted", new[] { 14, 37, 60, 84 }, targetMap, skin);
        ApplyDirectionFrames("SpringLookUp", new[] { 15, 38, 61, 85 }, targetMap, skin);

        int[] corneredFrames = GetCorneredFrames(character);
        ApplyFrameSequence(
            FindAnimatedRenderer("Cornered"),
            "Cornered",
            corneredFrames[0],
            corneredFrames,
            targetMap,
            skin,
            loop: true,
            loopStartFrame: CorneredLoopStartFrame
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

        ApplyFrameSequenceDefinitions(PowerGlovePickupDefinitions, targetMap, skin, loop: false);
        ApplyFrameSequenceDefinitions(
            PowerGloveCarryDefinitions,
            targetMap,
            skin,
            loop: true,
            idleFrameIndex: 1
        );
    }

    static int GetLastFrame(Dictionary<int, Sprite> frames, int directionOffset)
    {
        int max = -1;
        foreach (int frame in frames.Keys)
            if (frame > max) max = frame;

        return max - 3 + directionOffset;
    }

    void ApplyDirectionFrames(string rendererPrefix, int[] downRightLeftUp, Dictionary<int, Sprite> targetMap, BomberSkin skin)
    {
        ApplySingleFrame(FindAnimatedRenderer(rendererPrefix + "Down"), rendererPrefix + "Down", downRightLeftUp[0], targetMap, skin);
        ApplySingleFrame(FindAnimatedRenderer(rendererPrefix + "Right"), rendererPrefix + "Right", downRightLeftUp[1], targetMap, skin);
        ApplySingleFrame(FindAnimatedRenderer(rendererPrefix + "Left"), rendererPrefix + "Left", downRightLeftUp[2], targetMap, skin);
        ApplySingleFrame(FindAnimatedRenderer(rendererPrefix + "Up"), rendererPrefix + "Up", downRightLeftUp[3], targetMap, skin);
    }

    void ApplySingleFrame(AnimatedSpriteRenderer renderer, string rendererName, int frame, Dictionary<int, Sprite> targetMap, BomberSkin skin)
    {
        ApplyFrameSequence(renderer, rendererName, frame, new[] { frame }, targetMap, skin);
    }

    void ApplyFrameSequenceDefinitions(
        FrameSequenceDefinition[] definitions,
        Dictionary<int, Sprite> targetMap,
        BomberSkin skin,
        bool loop,
        int idleFrameIndex = 0)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            FrameSequenceDefinition definition = definitions[i];
            int idleIndex = Mathf.Clamp(idleFrameIndex, 0, definition.Frames.Length - 1);
            ApplyFrameSequence(
                FindAnimatedRenderer(definition.RendererName),
                definition.RendererName,
                definition.Frames[idleIndex],
                definition.Frames,
                targetMap,
                skin,
                loop: loop
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

    public static int[] GetEndStageFrames(BomberCharacter character)
    {
        if (BomberSkinResourceCatalog.GetCharacterFolderName(character) == "MinerBomber")
            return MinerBomberEndStageFrames;
        return character switch
        {
            BomberCharacter.LadyBomber => LadyBomberEndStageFrames,
            BomberCharacter.TinyBomber => TinyBomberEndStageFrames,
            _ => BombermanEndStageFrames
        };
    }

    static int[] GetAfkFrames(BomberCharacter character)
    {
        if (BomberSkinResourceCatalog.GetCharacterFolderName(character) == "MinerBomber")
            return MinerBomberAfkFrames;
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

    static int[] GetCorneredFrames(BomberCharacter character)
    {
        return character == BomberCharacter.LadyBomber
            ? LadyBomberCorneredFrames
            : BombermanCorneredFrames;
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
        float[] frameDurations = null,
        int loopStartFrame = 0)
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
        renderer.loopStartFrame = Mathf.Clamp(loopStartFrame, 0, frames.Length - 1);
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
