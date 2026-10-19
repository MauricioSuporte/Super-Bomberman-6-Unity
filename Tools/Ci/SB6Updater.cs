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

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length == 3 && args[0] == "--check-path") { SafePath(args[1], args[2]); return 0; }
            if (args.Length == 4 && args[0] == "--validate-package") { ValidatePackage(args[1], args[2], args[3]); return 0; }
            bool applying = args.Length >= 2 && args[0] == "--apply";
            int waitForGame = 0;
            int waitIndex = Array.IndexOf(args, "--wait-for-game");
            if (waitIndex >= 0 && (waitIndex + 1 >= args.Length || !int.TryParse(args[waitIndex + 1], out waitForGame) || waitForGame <= 0))
                throw new ArgumentException("Identificador do jogo inválido.");
            if (!applying)
            {
                string install = AppDomain.CurrentDomain.BaseDirectory;
                if (!File.Exists(Path.Combine(install, Game))) throw new IOException("Coloque este atualizador na pasta do jogo.");
                string helper = Path.Combine(Path.GetTempPath(), "SB6Updater-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(helper);
                string copy = Path.Combine(helper, "Updater.exe");
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, copy);
                Process.Start(new ProcessStartInfo(copy, "--apply \"" + install.TrimEnd(Path.DirectorySeparatorChar) + "\"" + (waitForGame > 0 ? " --wait-for-game " + waitForGame : ""))
                    { UseShellExecute = false, WorkingDirectory = helper });
                return 0;
            }
            if (waitForGame > 0)
            {
                try
                {
                    using (var game = Process.GetProcessById(waitForGame))
                        if (!game.WaitForExit(15000)) throw new IOException("Feche o jogo antes de atualizar.");
                }
                catch (ArgumentException) { } // The game may already have exited.
            }
            Update(Path.GetFullPath(args[1]));
            Console.WriteLine("Pressione ENTER para fechar."); Console.ReadLine();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Não foi possível atualizar: " + error.Message);
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
        if (!File.Exists(Path.Combine(install, Game))) throw new IOException("Pasta do jogo inválida.");
        Console.WriteLine("Verificando a versão online publicada...");
        Dictionary<string, object> release;
        using (var client = Client()) release = Json.Deserialize<Dictionary<string, object>>(
            client.DownloadString("https://api.github.com/repos/" + Repository + "/releases/tags/online-v0.5.0"));
        string body = (string)release["body"];
        const string marker = "<!-- sb6-update:";
        int start = body.IndexOf(marker, StringComparison.Ordinal);
        int end = start < 0 ? -1 : body.IndexOf(" -->", start, StringComparison.Ordinal);
        if (start < 0 || end < 0) throw new IOException("Esta publicação ainda não tem um pacote de atualização.");
        var update = Json.Deserialize<Dictionary<string, object>>(body.Substring(start + marker.Length, end - start - marker.Length));
        string commit = (string)update["sourceCommit"];
        string info = Path.Combine(install, "build-info.json");
        if (File.Exists(info) && (string)Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(info, Encoding.UTF8))["sourceCommit"] == commit)
        { Console.WriteLine("O jogo já está na versão online mais recente."); return; }
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Game)))
            using (process)
                if (string.Equals(process.MainModule.FileName, Path.Combine(install, Game), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Feche o jogo e execute o atualizador novamente.");
        var url = new Uri((string)update["url"]);
        if (url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal))
            throw new IOException("Endereço do pacote inválido.");
        string work = AppDomain.CurrentDomain.BaseDirectory;
        string archive = Path.Combine(work, "download.zip");
        Console.WriteLine("Baixando a atualização...");
        using (var client = Client()) client.DownloadFile(url, archive);
        string folder = (string)update["folder"];
        ValidatePackage(archive, (string)update["sha256"], folder);
        string stage = Path.Combine(work, "stage"); Directory.CreateDirectory(stage);
        using (var zip = ZipFile.OpenRead(archive))
            foreach (var entry in zip.Entries)
            {
                string destination = SafePath(stage, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(destination);
                else { Directory.CreateDirectory(Path.GetDirectoryName(destination)); entry.ExtractToFile(destination); }
            }
        string source = SafePath(stage, folder);
        var downloaded = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(source, "build-info.json"), Encoding.UTF8));
        if ((string)downloaded["sourceCommit"] != commit) throw new IOException("O pacote não corresponde à publicação.");
        Install(source, install, Path.Combine(work, "backup"));
        Console.WriteLine("Jogo atualizado com sucesso. Versão " + update["version"] + ".");
        Console.WriteLine("Backup e arquivos temporários: " + work);
    }

    static string SafePath(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains(":")) throw new IOException("Caminho inválido no pacote.");
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(root, relative));
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Caminho fora da pasta do jogo.");
        return target;
    }

    static void ValidatePackage(string path, string expected, string folder)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase))
                throw new IOException("A verificação SHA-256 do download falhou.");
        string root = Path.Combine(Path.GetTempPath(), "SB6-Package-Validation");
        string package = SafePath(root, folder);
        bool executable = false, manifest = false;
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var zip = ZipFile.OpenRead(path))
            foreach (var entry in zip.Entries)
            {
                string target = SafePath(root, entry.FullName);
                if (!target.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Estrutura de pacote inválida.");
                if (!entries.Add(target)) throw new IOException("Arquivo duplicado no pacote.");
                executable |= target == Path.Combine(package, Game);
                manifest |= target == Path.Combine(package, "build-info.json");
            }
        if (!executable || !manifest) throw new IOException("O pacote está incompleto.");
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
        try
        {
            foreach (string relative in incoming.Concat(obsolete).Distinct(StringComparer.OrdinalIgnoreCase))
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
                    File.Copy(SafePath(source, relative), destination, true);
                }
                else if (File.Exists(destination)) File.Delete(destination);
            }
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
            if (errors.Count > 1) throw new IOException("Falha na atualização. Backup para recuperação: " + backup, new AggregateException(errors));
            throw;
        }
    }
}
