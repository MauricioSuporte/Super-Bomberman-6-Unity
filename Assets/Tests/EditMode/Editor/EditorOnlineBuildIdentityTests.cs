using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;

public sealed class EditorOnlineBuildIdentityTests
{
    [Test]
    public void ReleaseIdentityRequiresUnchangedPlayerInputs()
    {
        string root = Path.Combine(Path.GetTempPath(), "SB6-EditorIdentity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Git(root, "init -q");
            string script = Path.Combine(root, "Assets/Scripts/Player.cs");
            Write(script, "baseline");
            Git(root, "add .");
            Git(root, "-c user.name=IdentityTest -c user.email=test@example.invalid commit -qm baseline");
            string commit = Git(root, "rev-parse HEAD").Trim();
            Assert.AreEqual(commit, EditorOnlineBuildIdentity.ResolveIdentity(root));

            Write(Path.Combine(root, "Assets/Editor/Helper.cs"), "editor-only change");
            Write(Path.Combine(root, "Docs/note.md"), "documentation");
            Assert.AreEqual(commit, EditorOnlineBuildIdentity.ResolveIdentity(root));

            Write(script, "modified runtime");
            Assert.AreEqual("local-development", EditorOnlineBuildIdentity.ResolveIdentity(root));
            Write(script, "baseline");
            Assert.AreEqual(commit, EditorOnlineBuildIdentity.ResolveIdentity(root));

            string asset = Path.Combine(root, "Assets/new-asset.txt");
            Write(asset, "untracked player asset");
            Assert.AreEqual("local-development", EditorOnlineBuildIdentity.ResolveIdentity(root));
            File.Delete(asset);

            Write(Path.Combine(root, "Packages/manifest.json"), "{}");
            Git(root, "add Packages");
            Assert.AreEqual("local-development", EditorOnlineBuildIdentity.ResolveIdentity(root));
        }
        finally
        {
            // Delete only this fixture's verified temporary directory, including Git's read-only files.
            string resolved = Path.GetFullPath(root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("SB6-EditorIdentity-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected identity test directory.");
            foreach (string file in Directory.GetFiles(resolved, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(resolved, true);
        }
    }

    static void Write(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    static string Git(string root, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        });
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(3000)) { process.Kill(); Assert.Fail("Git setup timed out."); }
        Assert.AreEqual(0, process.ExitCode, errors.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
}
