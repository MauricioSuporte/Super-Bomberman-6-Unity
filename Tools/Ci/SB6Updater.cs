// Standalone updater, built with the .NET Framework included with Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal static class SB6Updater
{
    const string Repository = "MauricioSuporte/Super-Bomberman-6-Unity";
    const string Game = "Super Bomberman 6.exe";
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    static int language;
    static bool progressEnabled;
    static readonly Stopwatch UpdateTimer = new Stopwatch();
    static string progressStage;
    static double lastProgressAt;

    static string Text(string en, string ja, string es, string pt)
    {
        return language == 1 ? ja : language == 2 ? es : language == 3 ? pt : en;
    }

    static int ReadLanguage(string savePath)
    {
        try
        {
            if (!File.Exists(savePath)) return 0;
            var saved = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(savePath, Encoding.UTF8));
            object value;
            int selected;
            return saved != null && saved.TryGetValue("language", out value) && int.TryParse(Convert.ToString(value), out selected) && selected >= 0 && selected <= 3 ? selected : 0;
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
        catch (ArgumentException) { return 0; }
        catch (InvalidOperationException) { return 0; }
    }

    static string ProgressText(double fraction, double elapsed)
    {
        int percent = (int)(Math.Max(0, Math.Min(1, fraction)) * 100);
        int filled = percent * 24 / 100;
        string estimate = Text("estimating...", "計算中...", "calculando...", "calculando...");
        if (fraction >= 1) estimate = "00:00";
        else if (fraction > 0 && elapsed >= .5)
        {
            double remaining = Math.Min(359999, elapsed * (1 - fraction) / fraction);
            var time = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, remaining)));
            estimate = time.TotalHours >= 1 ? ((int)time.TotalHours).ToString("00") + time.ToString(@"\:mm\:ss") : time.ToString(@"mm\:ss");
        }
        return "[" + new string('#', filled) + new string('-', 24 - filled) + "] " +
            (fraction < 0 ? "?" : percent.ToString()) + "% | " + Text("ETA: ", "残り時間: ", "Restante: ", "Tempo restante: ") + estimate;
    }

    static void Report(string stage, double start, double span, long done, long total)
    {
        if (!progressEnabled) return;
        double now = UpdateTimer.Elapsed.TotalSeconds;
        if (stage != progressStage)
        {
            FinishProgress();
            progressStage = stage;
            Console.WriteLine(stage);
        }
        else if (done != total && now - lastProgressAt < .1) return;
        lastProgressAt = now;
        double fraction = total > 0 ? start + span * Math.Min(1, (double)done / total) : -1;
        Console.Write("\r" + ProgressText(fraction, now).PadRight(78));
    }

    static void FinishProgress()
    {
        if (progressStage == null) return;
        Console.WriteLine();
        progressStage = null;
    }

    static void Download(Uri url, string archive)
    {
        string stage = Text("Downloading update...", "更新をダウンロード中...", "Descargando actualización...", "Baixando atualização...");
        Report(stage, .01, .74, 0, 1);
        using (var client = Client())
        using (var input = client.OpenRead(url))
        using (var output = File.Create(archive))
        {
            long total;
            long.TryParse(client.ResponseHeaders[HttpResponseHeader.ContentLength], out total);
            long received = 0;
            byte[] buffer = new byte[65536];
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                received += count;
                Report(stage, .01, .74, received, total);
            }
            if (total > 0 && received != total) throw new IOException(Text("Incomplete download.", "ダウンロードが不完全です。", "Descarga incompleta.", "Download incompleto."));
            Report(stage, .01, .74, 1, 1);
        }
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string saveDirectory = Environment.GetEnvironmentVariable("SB6_SAVE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(saveDirectory))
            saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "MauricioSuporte", "Super Bomberman 6", "SaveData");
        language = ReadLanguage(Path.Combine(saveDirectory, "save.dat"));
        int languageIndex = Array.IndexOf(args, "--language");
        int selectedLanguage;
        if (languageIndex >= 0 && languageIndex + 1 < args.Length && int.TryParse(args[languageIndex + 1], out selectedLanguage) && selectedLanguage >= 0 && selectedLanguage <= 3)
            language = selectedLanguage;
        try
        {
            if (args.Length == 3 && args[0] == "--check-path") { SafePath(args[1], args[2]); return 0; }
            if (args.Length == 4 && args[0] == "--validate-package") { ValidatePackage(args[1], args[2], args[3]); return 0; }
            bool applying = args.Length >= 2 && args[0] == "--apply";
            int waitForGame = 0;
            int waitIndex = Array.IndexOf(args, "--wait-for-game");
            if (waitIndex >= 0 && (waitIndex + 1 >= args.Length || !int.TryParse(args[waitIndex + 1], out waitForGame) || waitForGame <= 0))
                throw new ArgumentException(Text("Invalid game process identifier.", "ゲームのプロセスIDが無効です。", "Identificador de proceso inválido.", "Identificador do jogo inválido."));
            if (!applying)
            {
                string install = AppDomain.CurrentDomain.BaseDirectory;
                if (!File.Exists(Path.Combine(install, Game))) throw new IOException(Text("Place this updater in the game folder.", "更新プログラムをゲームのフォルダーに置いてください。", "Coloca el actualizador en la carpeta del juego.", "Coloque este atualizador na pasta do jogo."));
                string helper = Path.Combine(Path.GetTempPath(), "SB6Updater-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(helper);
                string copy = Path.Combine(helper, "Updater.exe");
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, copy);
                Process.Start(new ProcessStartInfo(copy, "--apply \"" + install.TrimEnd(Path.DirectorySeparatorChar) + "\" --language " + language + (waitForGame > 0 ? " --wait-for-game " + waitForGame : ""))
                    { UseShellExecute = false, WorkingDirectory = helper });
                return 0;
            }
            if (waitForGame > 0)
            {
                try
                {
                    using (var game = Process.GetProcessById(waitForGame))
                        if (!game.WaitForExit(15000)) throw new IOException(Text("Close the game before updating.", "更新前にゲームを終了してください。", "Cierra el juego antes de actualizar.", "Feche o jogo antes de atualizar."));
                }
                catch (ArgumentException) { } // The game may already have exited.
            }
            Update(Path.GetFullPath(args[1]));
            Console.WriteLine(Text("Press ENTER to close.", "ENTERで閉じます。", "Pulsa ENTER para cerrar.", "Pressione ENTER para fechar.")); Console.ReadLine();
            return 0;
        }
        catch (Exception error)
        {
            FinishProgress();
            Console.Error.WriteLine(Text("Could not update: ", "更新できませんでした: ", "No se pudo actualizar: ", "Não foi possível atualizar: ") + error.Message);
            if (args.Length == 0 || args[0] == "--apply") Console.ReadLine();
            return 1;
        }
    }

    static WebClient Client()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var client = new WebClient();
        client.Headers[HttpRequestHeader.UserAgent] = "SB6-Windows-Updater";
        client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
        client.Encoding = Encoding.UTF8;
        return client;
    }

    static void Update(string install)
    {
        progressEnabled = true;
        UpdateTimer.Restart();
        if (!File.Exists(Path.Combine(install, Game))) throw new IOException(Text("Invalid game folder.", "ゲームのフォルダーが無効です。", "Carpeta del juego inválida.", "Pasta do jogo inválida."));
        Console.WriteLine(Text("Checking the published online version...", "公開中のオンラインバージョンを確認中...", "Comprobando la versión online publicada...", "Verificando a versão online publicada..."));
        Dictionary<string, object> release;
        using (var client = Client()) release = Json.Deserialize<Dictionary<string, object>>(
            client.DownloadString("https://api.github.com/repos/" + Repository + "/releases/tags/online-v0.5.0"));
        string body = (string)release["body"];
        const string marker = "<!-- sb6-update:";
        int start = body.IndexOf(marker, StringComparison.Ordinal);
        int end = start < 0 ? -1 : body.IndexOf(" -->", start, StringComparison.Ordinal);
        if (start < 0 || end < 0) throw new IOException(Text("This release has no update package yet.", "このリリースには更新パッケージがありません。", "Esta publicación aún no tiene un paquete de actualización.", "Esta publicação ainda não tem um pacote de atualização."));
        var update = Json.Deserialize<Dictionary<string, object>>(body.Substring(start + marker.Length, end - start - marker.Length));
        string commit = (string)update["sourceCommit"];
        string info = Path.Combine(install, "build-info.json");
        if (File.Exists(info) && (string)Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(info, Encoding.UTF8))["sourceCommit"] == commit)
        { Console.WriteLine(Text("The game is already up to date.", "ゲームは最新です。", "El juego ya está actualizado.", "O jogo já está na versão online mais recente.")); return; }
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Game)))
            using (process)
                if (string.Equals(process.MainModule.FileName, Path.Combine(install, Game), StringComparison.OrdinalIgnoreCase))
                    throw new IOException(Text("Close the game and run the updater again.", "ゲームを終了し、更新プログラムを再実行してください。", "Cierra el juego y ejecuta el actualizador de nuevo.", "Feche o jogo e execute o atualizador novamente."));
        var url = new Uri((string)update["url"]);
        if (url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal))
            throw new IOException(Text("Invalid package URL.", "パッケージのURLが無効です。", "Dirección del paquete inválida.", "Endereço do pacote inválido."));
        string work = AppDomain.CurrentDomain.BaseDirectory;
        string archive = Path.Combine(work, "download.zip");
        Download(url, archive);
        string folder = (string)update["folder"];
        ValidatePackage(archive, (string)update["sha256"], folder);
        string stage = Path.Combine(work, "stage"); Directory.CreateDirectory(stage);
        using (var zip = ZipFile.OpenRead(archive))
        {
            long total = zip.Entries.Sum(entry => entry.Length);
            long extracted = 0;
            string extracting = Text("Extracting files...", "ファイルを展開中...", "Extrayendo archivos...", "Extraindo arquivos...");
            Report(extracting, .82, .10, 0, Math.Max(1, total));
            foreach (var entry in zip.Entries)
            {
                string destination = SafePath(stage, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(destination);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = entry.Open())
                    using (var output = File.Create(destination))
                        CopyWithProgress(input, output, extracting, .82, .10, ref extracted, Math.Max(1, total));
                }
                Report(extracting, .82, .10, extracted, Math.Max(1, total));
            }
        }
        string source = SafePath(stage, folder);
        var downloaded = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(source, "build-info.json"), Encoding.UTF8));
        if ((string)downloaded["sourceCommit"] != commit) throw new IOException(Text("Package does not match the release.", "パッケージがリリースと一致しません。", "El paquete no corresponde a la publicación.", "O pacote não corresponde à publicação."));
        Install(source, install, Path.Combine(work, "backup"));
        FinishProgress();
        Console.WriteLine(Text("Game updated successfully. Version ", "更新が完了しました。バージョン ", "Juego actualizado. Versión ", "Jogo atualizado com sucesso. Versão ") + update["version"] + ".");
        Console.WriteLine(Text("Backup and temporary files: ", "バックアップと一時ファイル: ", "Copia de seguridad y archivos temporales: ", "Backup e arquivos temporários: ") + work);
    }

    static string SafePath(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains(":")) throw new IOException(Text("Invalid package path.", "パッケージ内のパスが無効です。", "Ruta inválida en el paquete.", "Caminho inválido no pacote."));
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(root, relative));
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException(Text("Path outside the game folder.", "ゲームのフォルダー外のパスです。", "Ruta fuera de la carpeta del juego.", "Caminho fora da pasta do jogo."));
        return target;
    }

    static void ValidatePackage(string path, string expected, string folder)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
        {
            string verifying = Text("Verifying download...", "ダウンロードを検証中...", "Verificando descarga...", "Verificando download...");
            Report(verifying, .75, .07, 0, Math.Max(1, stream.Length));
            byte[] buffer = new byte[65536];
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha.TransformBlock(buffer, 0, count, buffer, 0);
                Report(verifying, .75, .07, stream.Position, Math.Max(1, stream.Length));
            }
            sha.TransformFinalBlock(buffer, 0, 0);
            if (!string.Equals(BitConverter.ToString(sha.Hash).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase))
                throw new IOException(Text("Download SHA-256 verification failed.", "ダウンロードのSHA-256検証に失敗しました。", "Falló la verificación SHA-256 de la descarga.", "A verificação SHA-256 do download falhou."));
        }
        string root = Path.Combine(Path.GetTempPath(), "SB6-Package-Validation");
        string package = SafePath(root, folder);
        bool executable = false, manifest = false;
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var zip = ZipFile.OpenRead(path))
            foreach (var entry in zip.Entries)
            {
                string target = SafePath(root, entry.FullName);
                if (!target.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException(Text("Invalid package structure.", "パッケージの構造が無効です。", "Estructura del paquete inválida.", "Estrutura de pacote inválida."));
                if (!entries.Add(target)) throw new IOException(Text("Duplicate file in package.", "パッケージ内のファイルが重複しています。", "Archivo duplicado en el paquete.", "Arquivo duplicado no pacote."));
                executable |= target == Path.Combine(package, Game);
                manifest |= target == Path.Combine(package, "build-info.json");
            }
        if (!executable || !manifest) throw new IOException(Text("Package is incomplete.", "パッケージが不完全です。", "El paquete está incompleto.", "O pacote está incompleto."));
    }

    static void CopyWithProgress(Stream input, Stream output, string stage, double start, double span, ref long done, long total)
    {
        byte[] buffer = new byte[65536];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, count);
            done += count;
            Report(stage, start, span, done, total);
        }
    }

    static void Install(string source, string target, string backup)
    {
        Directory.CreateDirectory(backup);
        var changed = new List<string>();
        var original = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var incoming = new HashSet<string>(Directory.GetFiles(source, "*", SearchOption.AllDirectories)
            .Select(file => file.Substring(source.Length + 1)), StringComparer.OrdinalIgnoreCase);
        string previousManifest = Path.Combine(target, "client-files.json");
        string[] previous = File.Exists(previousManifest)
            ? Json.Deserialize<string[]>(File.ReadAllText(previousManifest, Encoding.UTF8)) : new string[0];
        var obsolete = previous.Where(file => !incoming.Contains(file.Replace('/', Path.DirectorySeparatorChar))).ToArray();
        string[] files = incoming.Concat(obsolete).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string installing = Text("Installing update...", "更新をインストール中...", "Instalando actualización...", "Instalando atualização...");
        long installed = 0;
        long installSize = Math.Max(1, incoming.Sum(file => new FileInfo(SafePath(source, file)).Length) + obsolete.Length);
        Report(installing, .92, .08, 0, installSize);
        try
        {
            foreach (string relative in files)
            {
                string destination = SafePath(target, relative);
                if (File.Exists(destination))
                {
                    string saved = SafePath(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)); File.Copy(destination, saved);
                    original.Add(relative);
                }
                changed.Add(relative);
                if (incoming.Contains(relative))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = File.OpenRead(SafePath(source, relative)))
                    using (var output = File.Create(destination))
                        CopyWithProgress(input, output, installing, .92, .08, ref installed, installSize);
                }
                else
                {
                    if (File.Exists(destination)) File.Delete(destination);
                    installed++;
                }
                Report(installing, .92, .08, installed, installSize);
            }
            Report(installing, .92, .08, 1, 1);
        }
        catch (Exception installError)
        {
            var errors = new List<Exception> { installError };
            foreach (string relative in changed.AsEnumerable().Reverse())
            {
                try
                {
                    string destination = SafePath(target, relative);
                    if (original.Contains(relative)) File.Copy(SafePath(backup, relative), destination, true);
                    else if (File.Exists(destination)) File.Delete(destination);
                }
                catch (Exception rollbackError) { errors.Add(rollbackError); }
            }
            if (errors.Count > 1) throw new IOException(Text("Update failed. Recovery backup: ", "更新失敗。復元用バックアップ: ", "Actualización fallida. Copia de recuperación: ", "Falha na atualização. Backup para recuperação: ") + backup, new AggregateException(errors));
            throw;
        }
    }
}
