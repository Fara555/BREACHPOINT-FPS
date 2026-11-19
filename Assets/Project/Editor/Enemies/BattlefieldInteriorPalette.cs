using System;
using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldInteriorPalette
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Materials/";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Compose Interior Color Hierarchy")]
        public static void Apply()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before composing the interior palette.");
            BattlefieldWallSurface.Bake();
            CreateMaterial("WallWarm", new Color(.52f, .29f, .06f), .15f);
            CreateMaterial("InteriorNeutral", new Color(.55f, .53f, .44f), .20f);
            CreateMaterial("InteriorQuietFrame", new Color(.08f, .09f, .095f), .45f);
            CreateMaterial("InteriorCeiling", new Color(.035f, .04f, .043f), .15f);
            BattlefieldWallSurface.BakeAccentFinishes();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/InteriorLighting.asset");
            if (!profile.TryGet<Exposure>(out var exposure)) throw new InvalidOperationException("Interior exposure is missing.");
            Undo.RecordObject(exposure, "Balance readable interior exposure");
            exposure.fixedExposure.Override(6.8f); EditorUtility.SetDirty(exposure);
            if (!profile.TryGet<GradientSky>(out var sky))
            {
                sky = profile.Add<GradientSky>(true);
                AssetDatabase.AddObjectToAsset(sky, profile);
            }
            Undo.RecordObject(sky, "Replace inherited blue outdoor ambience");
            sky.skyIntensityMode.Override(SkyIntensityMode.Exposure);
            sky.exposure.Override(11);
            sky.top.Override(new Color(.14f, .15f, .16f));
            sky.middle.Override(new Color(.22f, .215f, .20f));
            sky.bottom.Override(new Color(.07f, .075f, .08f));
            EditorUtility.SetDirty(sky); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>())
                .Single(t => t.name == "12 Art - Interior walls ceiling and equipment");
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                Undo.RecordObject(light, "Compose interior fixture colors");
                Undo.RecordObject(light.transform, "Aim interior fixture");
                bool gallery = light.transform.position.x > 0 && Mathf.Abs(light.transform.position.z - 42) < 1;
                bool workshop = light.transform.position.x < 0 && Mathf.Abs(light.transform.position.z - 42) < 1;
                light.color = gallery ? new Color(.87f, .95f, 1) : workshop ? new Color(1, .88f, .72f) : new Color(1, .96f, .89f);
                light.intensity = 18000;
                if (gallery) light.transform.rotation = Quaternion.LookRotation(new Vector3(17.5f, 3.4f, 43) - light.transform.position);
                else if (workshop) light.transform.rotation = Quaternion.LookRotation(new Vector3(-11, 2.7f, 41) - light.transform.position);
                else light.transform.rotation = Quaternion.Euler(90, 0, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
            }
            var report = new StringBuilder();
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                var data = light.GetComponent<HDAdditionalLightData>();
                report.AppendLine(light.name + " enabled=" + light.enabled + " active=" + light.gameObject.activeInHierarchy +
                    " position=" + light.transform.position + " forward=" + light.transform.forward + " intensity=" + light.intensity +
                    " unit=" + light.lightUnit + " dimmer=" + data.lightDimmer + " diffuse=" + data.affectDiffuse +
                    " layers=" + light.renderingLayerMask + " color=" + light.color);
            }
            File.WriteAllText("Logs/BattlefieldInteriorFinish/palette-lighting.txt", report.ToString());
            var fixture = AssetDatabase.LoadAssetAtPath<Material>(Folder + "InteriorFixture.mat");
            Undo.RecordObject(fixture, "Neutralize ceiling diffuser color");
            fixture.SetColor("_EmissiveColor", new Color(.65f, .62f, .56f));
            HDMaterial.ValidateMaterial(fixture); EditorUtility.SetDirty(fixture); AssetDatabase.SaveAssetIfDirty(fixture);
            BattlefieldInteriorFinish.UpdateWalls();
        }

        private static void CreateMaterial(string name, Color color, float metal)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder + "WallDark.mat")) { name = name };
            else Undo.RecordObject(material, "Compose owned interior material");
            material.SetTexture("_BaseColorMap", null); material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal); material.SetFloat("_MetallicRemapMin", metal); material.SetFloat("_MetallicRemapMax", metal);
            material.SetFloat("_SmoothnessRemapMin", .24f); material.SetFloat("_SmoothnessRemapMax", .38f);
            material.SetFloat("_NormalScale", .35f);
            HDMaterial.ValidateMaterial(material);
            if (create) AssetDatabase.CreateAsset(material, path);
            else { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        }
    }
}
