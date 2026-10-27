using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyToolkit
{
    public static class ReleaseBuilder
    {
        public const string Version = "1.0.0-rc.4";
        public const string Root = "Assets/HierarchyToolkit";
        [MenuItem("Tools/HierarchyToolkit/Build unitypackage")]
        public static void Build()
        {
            string output = Path.GetFullPath("Artifacts/HierarchyToolkit-" + Version + ".unitypackage");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var paths = new List<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (path != Root && !path.StartsWith(Root + "/", StringComparison.Ordinal)) continue;
                if (path.StartsWith(Root + "/Tests", StringComparison.Ordinal) || path.Contains("/.git") || path.EndsWith(".unitypackage", StringComparison.Ordinal)) continue;
                paths.Add(path);
            }
            paths.Sort(StringComparer.Ordinal);
            AssetDatabase.ExportPackage(paths.ToArray(), output, ExportPackageOptions.Default);
            if (!File.Exists(output) || new FileInfo(output).Length == 0) throw new IOException("Unity did not produce a package.");
            Debug.Log("HierarchyToolkit package: " + output);
        }
    }
}
