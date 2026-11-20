using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void SkinCatalogColdDiscoveryAndPaletteChecksDoNotLoadTextures()
    {
        var before = new HashSet<Texture2D>(Resources.FindObjectsOfTypeAll<Texture2D>());
        typeof(BomberSkinResourceCatalog).GetMethod("ResetCharacterCatalog", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var characters = BomberSkinResourceCatalog.GetAvailableCharacters();
        Assert.Greater(characters.Length, 3);
        var metadata = Resources.Load<TextAsset>(BomberSkinResourceCatalog.MetadataResourcePath);
        Assert.IsNotNull(metadata);
        var rows = new HashSet<string>(metadata.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        int checkedSkins = 0;
        foreach (var character in characters)
            foreach (var skin in BomberSkinResourceCatalog.BombermanSkins)
            {
                string path = BomberSkinResourceCatalog.GetGeneratedResourcesPath(character) + "/" + BomberSkinResourceCatalog.GetSheetName(character, skin);
                Assert.AreEqual(rows.Contains("skin:" + path), BomberSkinResourceCatalog.IsGeneratedSkin(character, skin), path);
                checkedSkins++;
            }
        Assert.Greater(checkedSkins, 0);
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
            Assert.IsTrue(before.Contains(texture), "Catalog queries materialized texture: " + texture.name);
    }

    [TestCase(100861339, "Bookworm")]
    [TestCase(502647613, "KamikazeBomber")]
    [TestCase(363224332, "MechaBomber")]
    [TestCase(891550803, "MinerBomber")]
    public void SkinCatalogKeepsExistingCustomCharacterIds(int id, string folder)
    {
        Assert.IsTrue(BomberSkinResourceCatalog.IsAvailableCharacter((BomberCharacter)id));
        Assert.AreEqual(folder, BomberSkinResourceCatalog.GetCharacterFolderName((BomberCharacter)id));
        Assert.IsTrue(BomberSkinResourceCatalog.IsGeneratedSkin((BomberCharacter)id, BomberSkin.Palette1));
        Assert.IsFalse(BomberSkinResourceCatalog.IsGeneratedSkin((BomberCharacter)id, (BomberSkin)9999));
        Assert.AreEqual(BomberSkin.Palette1, BomberSkinResourceCatalog.NormalizeGeneratedSkin((BomberCharacter)id, (BomberSkin)9999));
    }

    [Test]
    public void SkinCatalogGenerationUsesImporterMetadataAndIsStable()
    {
        var before = new HashSet<Texture2D>(Resources.FindObjectsOfTypeAll<Texture2D>());
        BomberSkinSheetGenerator.GenerateResourceCatalog();
        const string path = "Assets/Resources/BomberSkinCatalog.txt";
        string first = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
        Assert.IsTrue(first.StartsWith(BomberSkinResourceCatalog.MetadataHeader + "\n", StringComparison.Ordinal));
        Assert.IsTrue(first.Contains("skin:Sprites/Bombers/Bomberman/Generated/Bomberman/Bomberman1\n"));
        BomberSkinSheetGenerator.GenerateResourceCatalog();
        Assert.AreEqual(first, System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8));
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
            Assert.IsTrue(before.Contains(texture), "Metadata generation loaded texture: " + texture.name);
    }
}
