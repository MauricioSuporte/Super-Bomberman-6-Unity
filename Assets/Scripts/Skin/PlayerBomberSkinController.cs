using System.Collections.Generic;
using UnityEngine;

public class PlayerBomberSkinController : MonoBehaviour
{
    [Header("Sprite Settings")]
    [SerializeField] private string spritesResourcesPath = "Sprites/Bombers/Bomberman";

    readonly Dictionary<BomberSkin, Dictionary<string, Sprite>> skinMaps = new();

    public void ApplyFromIdentity()
    {
        var id = GetComponentInParent<PlayerIdentity>(true);
        if (id == null || id.playerId <= 0)
            return;

        int playerId = Mathf.Clamp(id.playerId, 1, 4);
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
        EnsureCache(skin);

        if (!skinMaps.TryGetValue(skin, out var targetMap) || targetMap.Count == 0)
            return;

        var animated = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < animated.Length; i++)
        {
            var asr = animated[i];
            if (asr == null) continue;

            if (IsInsideMountedLouie(asr))
                continue;

            asr.idleSprite = SwapBySuffix(asr.idleSprite, targetMap);

            if (asr.animationSprite != null)
            {
                for (int f = 0; f < asr.animationSprite.Length; f++)
                    asr.animationSprite[f] = SwapBySuffix(asr.animationSprite[f], targetMap);
            }

            asr.RefreshFrame();
        }

        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr == null) continue;

            if (IsInsideMountedLouie(sr))
                continue;

            if (sr.GetComponentInParent<AnimatedSpriteRenderer>() != null)
                continue;

            sr.sprite = SwapBySuffix(sr.sprite, targetMap);
        }
    }

    bool IsInsideMountedLouie(Component c)
    {
        return c != null && c.GetComponentInParent<LouieRidingVisual>(true) != null;
    }

    void EnsureCache(BomberSkin skin)
    {
        if (skinMaps.ContainsKey(skin))
            return;

        skinMaps[skin] = BuildSheetMap(GetSheetName(skin));
    }

    Dictionary<string, Sprite> BuildSheetMap(string sheetName)
    {
        var map = new Dictionary<string, Sprite>(256);

        string sheetPath = $"{spritesResourcesPath}/{sheetName}";
        var sprites = Resources.LoadAll<Sprite>(sheetPath);

        if (sprites == null || sprites.Length == 0)
            return map;

        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            if (s == null) continue;

            if (!map.ContainsKey(s.name))
                map.Add(s.name, s);
        }

        return map;
    }

    Sprite SwapBySuffix(Sprite current, Dictionary<string, Sprite> targetMap)
    {
        if (current == null)
            return null;

        if (!TryExtractSuffix(current.name, out var suffix))
            return current;

        foreach (var kv in targetMap)
        {
            if (kv.Key.EndsWith(suffix))
                return kv.Value;
        }

        return current;
    }

    bool TryExtractSuffix(string spriteName, out string suffix)
    {
        suffix = null;

        int idx = spriteName.LastIndexOf('_');
        if (idx < 0)
            return false;

        suffix = spriteName[idx..];
        return true;
    }

    string GetSheetName(BomberSkin skin)
    {
        return skin + "Bomber";
    }
}
