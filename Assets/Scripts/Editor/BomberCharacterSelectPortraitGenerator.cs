#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BomberCharacterSelectPortraitGenerator
{
    const string SourceRoot = "Assets/Sprites/CharacterSelect";
    const string OutputRoot = "Assets/Resources/Sprites/CharacterSelect";
    const string PalettePath = OutputRoot + "/SelectionPalette.png";
    const string LogPrefix = "[CharacterSelectPortraitGenerator]";
    const int Size = 32;
    const int MaxWriteAttempts = 10;

    static readonly Dictionary<string, byte[]> pendingWrites = new();
    static int retryAttempts;
    static double nextRetryTime;

    enum WriteResult { Created, Updated, Unchanged, Deferred }

    sealed class GenerationReport
    {
        public int Characters;
        public int SkippedCharacters;
        public int Created;
        public int Updated;
        public int Unchanged;
        public int Deferred;
        public int CursorCreated;
        public int CursorUpdated;
        public int CursorUnchanged;
        public int CursorDeferred;
        public int Errors;

        public void Record(WriteResult result, bool isCursor)
        {
            if (isCursor)
            {
                switch (result)
                {
                    case WriteResult.Created: CursorCreated++; break;
                    case WriteResult.Updated: CursorUpdated++; break;
                    case WriteResult.Unchanged: CursorUnchanged++; break;
                    case WriteResult.Deferred: CursorDeferred++; break;
                }
            }
            else
            {
                switch (result)
                {
                    case WriteResult.Created: Created++; break;
                    case WriteResult.Updated: Updated++; break;
                    case WriteResult.Unchanged: Unchanged++; break;
                    case WriteResult.Deferred: Deferred++; break;
                }
            }
        }
    }

    [MenuItem("Tools/Sprites/Generate Character Select Portraits")]
    public static void GenerateAll()
    {
        var report = new GenerationReport();
        Debug.Log($"{LogPrefix} Iniciando geração dos retratos da seleção de personagens.");

        if (!Directory.Exists(SourceRoot))
        {
            Error($"Pasta de origem não encontrada: '{SourceRoot}'. " +
                  "Crie a pasta e adicione os retratos originais em PNG (32x64).", report);
            LogSummary(report);
            return;
        }

        string[] sourcePaths;
        try
        {
            sourcePaths = Directory.GetFiles(SourceRoot, "*.png", SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception)
        {
            Error($"Não foi possível listar as imagens de '{SourceRoot}': {exception}", report);
            LogSummary(report);
            return;
        }

        if (sourcePaths.Length == 0)
        {
            Error($"Nenhum arquivo PNG encontrado em '{SourceRoot}'. " +
                  "Os retratos originais devem ficar diretamente nessa pasta.", report);
            LogSummary(report);
            return;
        }

        // O cursor pode ser gerado independentemente dos retratos e da paleta.
        GenerateCursorSprites(report);

        string[] characters = sourcePaths
            .Where(path => !string.Equals(Path.GetFileNameWithoutExtension(path),
                "SkinSelectCursor", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (characters.Length == 0)
        {
            Warn($"Nenhum PNG de personagem encontrado em '{SourceRoot}'. " +
                 "O único PNG localizado é o cursor, ou não há retratos de personagens.");
            LogSummary(report);
            return;
        }

        if (!File.Exists(PalettePath))
        {
            Error($"Paleta de seleção não encontrada: '{PalettePath}'. " +
                  "Sem ela, nenhum retrato de personagem pode ser recolorido ou gerado.", report);
            LogSummary(report);
            return;
        }

        Texture2D palette = null;
        try
        {
            try
            {
                palette = BomberSkinSheetGenerator.LoadTexture(PalettePath);
            }
            catch (Exception exception)
            {
                Error($"Falha ao carregar a paleta '{PalettePath}': {exception}", report);
                return;
            }

            if (palette == null)
            {
                Error($"Não foi possível decodificar a paleta '{PalettePath}'. " +
                      "Verifique se o PNG é válido.", report);
                return;
            }

            if (palette.width < 2 || palette.height < 1)
            {
                Error($"Paleta inválida ({palette.width}x{palette.height}): '{PalettePath}'. " +
                      "Ela precisa ter ao menos 2 colunas: cores originais e uma variação.", report);
                return;
            }

            var skins = BomberSkinResourceCatalog.BombermanSkins;
            if (skins == null || skins.Length == 0)
            {
                Error("BomberSkinResourceCatalog.BombermanSkins está vazio. " +
                      "Não existem skins cadastradas para gerar os retratos.", report);
                return;
            }

            int skinCount = Mathf.Min(palette.width - 1, skins.Length);
            if (skinCount < skins.Length)
                Warn($"A paleta tem {palette.width - 1} coluna(s) de variação, mas existem " +
                     $"{skins.Length} skins cadastradas. Apenas {skinCount} serão processadas.");

            foreach (string sourcePath in characters)
                GenerateCharacter(sourcePath, palette, skinCount, report);
        }
        finally
        {
            if (palette != null)
                UnityEngine.Object.DestroyImmediate(palette);

            LogSummary(report);
        }
    }

    static void GenerateCharacter(string sourcePath, Texture2D palette, int skinCount,
        GenerationReport report)
    {
        string character = Path.GetFileNameWithoutExtension(sourcePath);
        Texture2D source = null;

        try
        {
            source = BomberSkinSheetGenerator.LoadTexture(sourcePath);
            if (source == null)
            {
                report.SkippedCharacters++;
                Error($"'{character}': não foi possível decodificar '{sourcePath}' como PNG.", report);
                return;
            }

            if (source.width != Size || source.height != Size * 2)
            {
                report.SkippedCharacters++;
                Warn($"'{character}' ignorado: imagem com {source.width}x{source.height} pixels. " +
                     $"Esperado: {Size}x{Size * 2} (dois retratos {Size}x{Size}). Arquivo: '{sourcePath}'.");
                return;
            }

            // Se nenhuma cor da origem corresponde à coluna 0, os retratos serão
            // gravados, mas a troca de paleta não terá efeito visível.
            var referenceColors = BomberSkinSheetGenerator.BuildPaletteMap(palette, 1);
            bool hasMatchingColor = source.GetPixels32().Any(pixel =>
                pixel.a != 0 && referenceColors.ContainsKey(pixel));
            if (!hasMatchingColor)
                Warn($"'{character}': nenhuma cor opaca da imagem original coincide com " +
                     $"a primeira coluna de '{PalettePath}'. Os PNGs serão gerados, " +
                     "mas as variações de paleta podem ficar com as cores originais.");

            string folder = $"{OutputRoot}/{character}";
            Directory.CreateDirectory(folder);
            report.Characters++;

            for (int i = 0; i < skinCount; i++)
            {
                string sheet = null;
                Texture2D recolored = null;
                try
                {
                    sheet = BomberSkinResourceCatalog.GetSheetName(
                        character, BomberSkinResourceCatalog.BombermanSkins[i]);
                    var colorMap = BomberSkinSheetGenerator.BuildPaletteMap(palette, i + 1);

                    // Entradas de paleta não definidas preservam a cor original.
                    foreach (Color32 reference in colorMap.Keys.ToArray())
                        if (colorMap[reference].a == 0)
                            colorMap[reference] = reference;

                    recolored = BomberSkinSheetGenerator.Recolor(source, colorMap);
                    for (int frame = 0; frame < 2; frame++)
                    {
                        Texture2D portrait = null;
                        try
                        {
                            portrait = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                            portrait.SetPixels(recolored.GetPixels(
                                0, source.height - Size * (frame + 1), Size, Size));
                            portrait.Apply();

                            string output = $"{folder}/{sheet}_{frame}.png";
                            WriteGeneratedAsset(output, portrait.EncodeToPNG(), report);
                        }
                        finally
                        {
                            if (portrait != null)
                                UnityEngine.Object.DestroyImmediate(portrait);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Error($"'{character}', skin '{sheet ?? i.ToString()}': " +
                          $"falha durante a geração: {exception}", report);
                }
                finally
                {
                    if (recolored != null)
                        UnityEngine.Object.DestroyImmediate(recolored);
                }
            }
        }
        catch (Exception exception)
        {
            report.SkippedCharacters++;
            Error($"'{character}': falha ao processar '{sourcePath}': {exception}", report);
        }
        finally
        {
            if (source != null)
                UnityEngine.Object.DestroyImmediate(source);
        }
    }

    static void GenerateCursorSprites(GenerationReport report)
    {
        string sourcePath = SourceRoot + "/SkinSelectCursor.png";
        if (!File.Exists(sourcePath))
        {
            Warn($"Cursor não gerado: arquivo opcional ausente em '{sourcePath}'. " +
                 "Isso não impede a geração dos retratos dos personagens.");
            return;
        }

        Texture2D source = null;
        try
        {
            source = BomberSkinSheetGenerator.LoadTexture(sourcePath);
            if (source == null)
            {
                Error($"Cursor não gerado: não foi possível decodificar '{sourcePath}'.", report);
                return;
            }

            if (source.width != 77 || source.height != 56)
            {
                Warn($"Cursor não gerado: '{sourcePath}' tem {source.width}x{source.height} pixels; " +
                     "o esperado é 77x56.");
                return;
            }

            string[] names = { "1", "2", "3", "4", "5", "6", "P", "TopLeft", "TopRight", "BottomLeft", "BottomRight", "L", "R", "Plus", "Frame" };
            Vector2Int[] cells = { new(0, 0), new(1, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1), new(0, 2), new(3, 0), new(4, 0), new(3, 1), new(4, 1), new(1, 2), new(2, 2), new(3, 2), new(0, 3) };
            Directory.CreateDirectory(OutputRoot + "/Cursor");

            for (int i = 0; i < names.Length; i++)
            {
                Texture2D tile = null;
                try
                {
                    int tileSize = names[i] == "Frame" ? 32 : 8;
                    Color[] pixels = source.GetPixels(
                        cells[i].x * 8, source.height - cells[i].y * 8 - tileSize,
                        tileSize, tileSize);

                    for (int p = 0; p < pixels.Length; p++)
                    {
                        Color32 pixel = pixels[p];
                        if (pixel.r == 251 && pixel.g == 255 && pixel.b == 0 && pixel.a > 0)
                            pixels[p] = Color.white; // A UI aplica tint mantendo o contorno.
                    }

                    tile = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
                    tile.SetPixels(pixels);
                    tile.Apply();

                    string path = $"{OutputRoot}/Cursor/{names[i]}.png";
                    WriteGeneratedAsset(path, tile.EncodeToPNG(), report, isCursor: true);
                }
                catch (Exception exception)
                {
                    Error($"Falha ao gerar cursor '{names[i]}': {exception}", report);
                }
                finally
                {
                    if (tile != null)
                        UnityEngine.Object.DestroyImmediate(tile);
                }
            }
        }
        catch (Exception exception)
        {
            Error($"Erro ao ler ou processar o cursor '{sourcePath}': {exception}", report);
        }
        finally
        {
            if (source != null)
                UnityEngine.Object.DestroyImmediate(source);
        }
    }

    static void WriteGeneratedAsset(string path, byte[] bytes, GenerationReport report,
        bool isCursor = false)
    {
        try
        {
            WriteResult result = TryWriteGeneratedAsset(path, bytes, out string error);
            report.Record(result, isCursor);

            if (result != WriteResult.Deferred)
            {
                pendingWrites.Remove(path);
                if (result == WriteResult.Created)
                    Debug.Log($"{LogPrefix} Criado: {path}");
                else if (result == WriteResult.Updated)
                    Debug.Log($"{LogPrefix} Atualizado: {path}");
                return;
            }

            Warn($"Arquivo em uso; a gravação será repetida (até {MaxWriteAttempts} tentativas): " +
                 $"'{path}'. Motivo: {error}");
            if (pendingWrites.Count == 0)
                retryAttempts = 0;

            pendingWrites[path] = bytes;
            nextRetryTime = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update -= RetryPendingWrites;
            EditorApplication.update += RetryPendingWrites;
        }
        catch (Exception exception)
        {
            Error($"Não foi possível salvar/importar '{path}': {exception}", report);
        }
    }

    static WriteResult TryWriteGeneratedAsset(string path, byte[] bytes, out string error)
    {
        error = null;
        try
        {
            bool alreadyExists = File.Exists(path);
            if (alreadyExists)
            {
                byte[] existing = File.ReadAllBytes(path);
                // O encoder pode produzir bytes diferentes para pixels idênticos.
                if (existing.SequenceEqual(bytes) || SamePixels(existing, bytes))
                    return WriteResult.Unchanged;
            }

            // Não truncar um PNG que o Unity pode estar usando.
            // Substituir somente a imagem, preservando o .meta e o GUID.
            string temporary = Path.Combine(Path.GetDirectoryName(path),
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (alreadyExists)
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }

            AssetDatabase.ImportAsset(path);
            return alreadyExists ? WriteResult.Updated : WriteResult.Created;
        }
        catch (IOException exception) when (IsFileInUse(exception))
        {
            error = exception.Message;
            return WriteResult.Deferred;
        }
    }

    static bool IsFileInUse(IOException exception)
    {
        int code = exception.HResult & 0xffff;
        return code == 32 || code == 33 || code == 1224;
    }

    static bool SamePixels(byte[] left, byte[] right)
    {
        Texture2D a = new(2, 2, TextureFormat.RGBA32, false);
        Texture2D b = new(2, 2, TextureFormat.RGBA32, false);
        try
        {
            return a.LoadImage(left) && b.LoadImage(right) &&
                a.width == b.width && a.height == b.height &&
                a.GetPixels32().SequenceEqual(b.GetPixels32());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
        }
    }

    static void RetryPendingWrites()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < nextRetryTime)
            return;

        nextRetryTime = EditorApplication.timeSinceStartup + 0.5;
        retryAttempts++;

        foreach (var pending in pendingWrites.ToArray())
        {
            try
            {
                WriteResult result = TryWriteGeneratedAsset(pending.Key, pending.Value,
                    out string error);
                if (result != WriteResult.Deferred)
                {
                    pendingWrites.Remove(pending.Key);
                    Debug.Log($"{LogPrefix} Gravação após tentativa {retryAttempts} " +
                              $"concluída ({result}): {pending.Key}");
                }
                else if (retryAttempts >= MaxWriteAttempts)
                {
                    pendingWrites.Remove(pending.Key);
                    Debug.LogError($"{LogPrefix} Falha definitiva após {MaxWriteAttempts} " +
                                   $"tentativas ao gravar '{pending.Key}'. Motivo: {error}");
                }
            }
            catch (Exception exception)
            {
                pendingWrites.Remove(pending.Key);
                Debug.LogError($"{LogPrefix} Falha ao tentar gravar '{pending.Key}': {exception}");
            }
        }

        if (pendingWrites.Count == 0)
            EditorApplication.update -= RetryPendingWrites;
    }

    static void LogSummary(GenerationReport report)
    {
        string summary = $"{LogPrefix} Finalizado. Personagens processados: {report.Characters}; " +
                         $"personagens ignorados: {report.SkippedCharacters}. " +
                         $"RETRATOS: criados={report.Created}, atualizados={report.Updated}, " +
                         $"já corretos={report.Unchanged}, pendentes={report.Deferred}. " +
                         $"CURSOR: criados={report.CursorCreated}, atualizados={report.CursorUpdated}, " +
                         $"já corretos={report.CursorUnchanged}, pendentes={report.CursorDeferred}. " +
                         $"Erros={report.Errors}.";

        if (report.Created == 0 && report.Updated == 0 && report.Deferred == 0 &&
            report.Unchanged > 0 && report.Errors == 0 && report.SkippedCharacters == 0)
            Debug.Log(summary + " Nenhum retrato precisava ser regenerado: " +
                      "os arquivos existentes já possuem os pixels esperados.");
        else if (report.Created == 0 && report.Updated == 0 && report.Deferred == 0 &&
                 report.Unchanged == 0)
            Debug.LogWarning(summary + " Nenhum retrato foi gerado; verifique os avisos acima.");
        else
            Debug.Log(summary);
    }

    static void Warn(string message)
    {
        Debug.LogWarning($"{LogPrefix} {message}");
    }

    static void Error(string message, GenerationReport report)
    {
        report.Errors++;
        Debug.LogError($"{LogPrefix} {message}");
    }
}

sealed class BomberCharacterSelectPortraitImportProcessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Sprites/CharacterSelect/", StringComparison.Ordinal) &&
            !assetPath.StartsWith("Assets/Sprites/CharacterSelect/", StringComparison.Ordinal))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = assetPath.EndsWith("/SelectionPalette.png", StringComparison.Ordinal);
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            importer.ClearPlatformTextureSettings(platform);
    }
}
#endif
