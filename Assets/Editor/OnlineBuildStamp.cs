using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Android exports made through Build Profiles must carry the same identity as desktop/server builds.
public sealed class OnlineBuildStamp : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        string identity = EditorOnlineBuildIdentity.ResolveIdentity(Directory.GetParent(Application.dataPath).FullName);
        if (identity == "local-development")
            throw new BuildFailedException("Cannot determine the shared online build identity. Build from a Git checkout.");
        const string path = "Assets/Resources/OnlineBuildIdentity.txt";
        if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8).Trim() != identity)
        {
            File.WriteAllText(path, identity, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
