#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the project's pixel-art defaults whenever a texture is imported
/// anywhere under Assets.
/// </summary>
sealed class PixelArtTextureImportProcessor : AssetPostprocessor
{
    const float PixelsPerUnit = 16f;

    void OnPreprocessTexture()
    {
        TextureImporter importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
#endif
