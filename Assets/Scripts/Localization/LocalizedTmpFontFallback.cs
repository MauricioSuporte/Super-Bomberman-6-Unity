using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class LocalizedTmpFontFallback
{
    private const char JapaneseProbeCharacter = '日';
    private const int SamplingPointSize = 90;

    private static readonly string[] FallbackOsFontNames =
    {
        "Yu Gothic",
        "Meiryo",
        "MS Gothic",
        "Noto Sans CJK JP",
        "Noto Sans JP",
        "Segoe UI",
        "Arial",
        "Liberation Sans"
    };

    private static readonly List<TMP_FontAsset> RuntimeFallbacks = new();
    private static Font runtimeLegacyJapaneseFont;
    private static bool warnedMissingFallback;

    public static void Apply(TMP_Text text)
    {
        if (text == null || text.font == null)
            return;

        if (SaveSystem.GetLanguage() != GameLanguage.Japanese)
            return;

        TMP_FontAsset fallback = ResolveJapaneseFallback();
        if (fallback == null)
            return;

        List<TMP_FontAsset> fallbacks = text.font.fallbackFontAssetTable;
        if (fallbacks == null)
        {
            fallbacks = new List<TMP_FontAsset>();
            text.font.fallbackFontAssetTable = fallbacks;
        }

        if (!fallbacks.Contains(fallback))
            fallbacks.Add(fallback);
    }

    public static void Apply(UnityEngine.UI.Text text)
    {
        if (text == null || SaveSystem.GetLanguage() != GameLanguage.Japanese)
            return;

        Font fallback = ResolveLegacyJapaneseFallback();
        if (fallback != null)
            text.font = fallback;
    }

    private static Font ResolveLegacyJapaneseFallback()
    {
        if (runtimeLegacyJapaneseFont != null && runtimeLegacyJapaneseFont.HasCharacter(JapaneseProbeCharacter))
            return runtimeLegacyJapaneseFont;

        runtimeLegacyJapaneseFont = Font.CreateDynamicFontFromOSFont(FallbackOsFontNames, SamplingPointSize);
        if (runtimeLegacyJapaneseFont != null && runtimeLegacyJapaneseFont.HasCharacter(JapaneseProbeCharacter))
            return runtimeLegacyJapaneseFont;

        return null;
    }

    private static TMP_FontAsset ResolveJapaneseFallback()
    {
        for (int i = 0; i < RuntimeFallbacks.Count; i++)
        {
            TMP_FontAsset fallback = RuntimeFallbacks[i];
            if (fallback != null && fallback.HasCharacter(JapaneseProbeCharacter, false, true))
                return fallback;
        }

        TMP_FontAsset created = CreateFallbackFromOsFont();
        if (created != null)
        {
            RuntimeFallbacks.Add(created);
            return created;
        }

        if (!warnedMissingFallback)
        {
            warnedMissingFallback = true;
            Debug.LogWarning("[Localization] Could not find a Japanese fallback font. Install Yu Gothic, Meiryo, MS Gothic, or Noto Sans CJK JP.");
        }

        return null;
    }

    private static TMP_FontAsset CreateFallbackFromOsFont()
    {
        for (int i = 0; i < FallbackOsFontNames.Length; i++)
        {
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                FallbackOsFontNames[i],
                "Regular",
                SamplingPointSize);

            if (fontAsset == null)
                continue;

            fontAsset.name = "Runtime Japanese Fallback - " + FallbackOsFontNames[i];
            fontAsset.atlasPopulationMode = AtlasPopulationMode.DynamicOS;
            fontAsset.isMultiAtlasTexturesEnabled = true;

            if (fontAsset.HasCharacter(JapaneseProbeCharacter, false, true))
                return fontAsset;

            Object.Destroy(fontAsset);
        }

        return null;
    }
}
