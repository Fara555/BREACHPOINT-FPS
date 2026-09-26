using System.IO;
using UnityEditor;
using UnityEngine;

namespace Breachpoint.EditorTools
{
    public static class ConfigureSelectedFbxAnimations
    {
        private const string MenuPath =
            "Tools/Breachpoint/Configure Selected FBX Animations";

        [MenuItem(MenuPath)]
        private static void ConfigureSelected()
        {
            Object[] selectedObjects = Selection.objects;

            int configuredCount = 0;
            int skippedCount = 0;

            foreach (Object selectedObject in selectedObjects)
            {
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);

                if (string.IsNullOrWhiteSpace(assetPath) ||
                    !assetPath.EndsWith(".fbx"))
                {
                    skippedCount++;
                    continue;
                }

                ModelImporter importer =
                    AssetImporter.GetAtPath(assetPath) as ModelImporter;

                if (importer == null)
                {
                    skippedCount++;
                    continue;
                }

                ModelImporterClipAnimation[] clips =
                    importer.clipAnimations;

                if (clips == null || clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }

                if (clips == null || clips.Length == 0)
                {
                    Debug.LogWarning(
                        $"No animation clips found in '{assetPath}'.",
                        selectedObject);

                    skippedCount++;
                    continue;
                }

                string fileName =
                    Path.GetFileNameWithoutExtension(assetPath);

                for (int i = 0; i < clips.Length; i++)
                {
                    ModelImporterClipAnimation clip = clips[i];

                    clip.name = clips.Length == 1
                        ? fileName
                        : $"{fileName}_{i + 1}";

                    clip.loopTime = true;
                    clip.loopPose = true;

                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;

                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;

                    clip.lockRootPositionXZ = false;
                    clip.keepOriginalPositionXZ = false;

                    clips[i] = clip;
                }

                importer.clipAnimations = clips;

                importer.SaveAndReimport();

                configuredCount++;
            }

            Debug.Log(
                $"FBX animation configuration finished. " +
                $"Configured: {configuredCount}, Skipped: {skippedCount}.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateConfigureSelected()
        {
            foreach (Object selectedObject in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);

                if (!string.IsNullOrWhiteSpace(assetPath) &&
                    assetPath.EndsWith(".fbx"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}