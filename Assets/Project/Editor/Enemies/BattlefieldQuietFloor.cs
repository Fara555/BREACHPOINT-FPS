using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldQuietFloor
    {
        private const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/QuietFloor";
        private const string RootName = "13 Art - Quiet industrial floor";
        private const string Output = "Logs/BattlefieldQuietFloor";
        private const string Art = "Assets/Project/Art/Environment/ServiceQuarter";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Replace Noisy Floor")]
        public static void Create()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            BattlefieldInteriorFinish.ValidateVisuals();
            var parent = Find("Blockout B - Service Quarter");
            if (parent.Find(RootName) != null) { Validate(); Capture(); return; }
            string protectedAssets = ProtectedAssets();
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            File.Copy(scene.path, Output + "/scene-before-floor.unity", true);
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            CreateOrUpdateMaterial("FloorField", new Color(.28f, .30f, .31f), .2f);
            CreateOrUpdateMaterial("FloorJoint", new Color(.23f, .245f, .25f), .05f);
            CreateOrUpdateMaterial("FloorAccess", new Color(.30f, .315f, .32f), .2f);
            BakeFloorSurface();
            foreach (string model in new[] { "QuietFloor", "CornerBody", "GalleryBody" }) Configure(model);
            var corner = Find("07 Art sample - Energy corner").Find("Detailed energy module - Blender");
            var gallery = Find("09 Art - Service gallery").Find("Service gallery - Blender");
            var entry = Find("08 Art - Entry service zone");
            var loop = Find("11 Art - Energy service loop deck");
            if (corner == null || gallery == null) throw new InvalidOperationException("Inspect original corner/gallery model roots before replacing the deck.");
            if (parent.lossyScale != Vector3.one || parent.rotation != Quaternion.identity) throw new InvalidOperationException("Floor requires the existing unscaled blockout root.");
            var root = new GameObject(RootName); root.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(root, "Replace floor visual assembly");
            Add(Folder + "/Models/QuietFloor.fbx", "Calm floor and gallery - Blender", root.transform, null);
            Add(Folder + "/Models/CornerBody.fbx", "Retained energy housing - no deck", root.transform, corner);
            Add(Folder + "/Models/GalleryBody.fbx", "Retained gallery fascia and parapet", root.transform, gallery);
            Add(Art + "/EntryZone/Models/Entry_Wall.fbx", "Retained entry screen - no deck", root.transform, entry);
            var archive = new StringBuilder();
            foreach (var old in new[] { corner, entry, gallery, loop })
                foreach (var renderer in old.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<TMP_Text>() == null))
                {
                    archive.AppendLine(HierarchyPath(renderer.transform) + " | enabled=" + renderer.enabled);
                    Undo.RecordObject(renderer, "Archive superseded noisy deck renderers");
                    renderer.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            File.WriteAllText(Output + "/archived-renderers.txt", archive.ToString());
            ArchivePrimitiveFloorRenderers();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Folder + "/PF_QuietFloor.prefab", InteractionMode.AutomatedAction);
            File.WriteAllText(Folder + "/ProtectedArtBefore.sha256.txt", protectedAssets);
            File.WriteAllText(Folder + "/GameplayBefore.sha256.txt", gameplay);
            AssetDatabase.ImportAsset(Folder + "/ProtectedArtBefore.sha256.txt");
            AssetDatabase.ImportAsset(Folder + "/GameplayBefore.sha256.txt");
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Floor scene could not be saved.");
            Capture();
        }

        private static void CreateOrUpdateMaterial(string name, Color color, float metal)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(Shader.Find("HDRP/Lit")) { name = name };
            else Undo.RecordObject(material, "Tune quiet floor coating");
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", name == "FloorJoint" ? .10f : .17f);
            material.SetFloat("_EnableGeometricSpecularAA", 1);
            material.SetFloat("_SpecularAAScreenSpaceVariance", .1f); material.SetFloat("_SpecularAAThreshold", .2f);
            HDMaterial.ValidateMaterial(material);
            if (create) AssetDatabase.CreateAsset(material, path);
            else { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Update Quiet Floor Visuals")]
        public static void UpdateVisuals()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            if (ProtectedAssets() != File.ReadAllText(Folder + "/ProtectedArtBefore.sha256.txt"))
                throw new InvalidOperationException("Protect accepted wall/equipment assets before updating the floor.");
            CreateOrUpdateMaterial("FloorField", new Color(.28f, .30f, .31f), .2f);
            CreateOrUpdateMaterial("FloorJoint", new Color(.23f, .245f, .25f), .05f);
            CreateOrUpdateMaterial("FloorAccess", new Color(.30f, .315f, .32f), .2f);
            BakeFloorSurface();
            foreach (string model in new[] { "QuietFloor", "CornerBody", "GalleryBody" }) Configure(model);
            ArchivePrimitiveFloorRenderers();
            Validate(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Quiet floor update could not be saved.");
            Capture();
        }

        private static void BakeFloorSurface()
        {
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            const int size = 512;
            var normals = new Color[size * size];
            var masks = new Color[normals.Length];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Grain(x + 1, y) - Grain(x - 1, y);
                float dy = Grain(x, y + 1) - Grain(x, y - 1);
                var n = new Vector3(-dx * .16f, -dy * .16f, 1).normalized;
                int index = y * size + x;
                normals[index] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                // HDRP packing: metallic, AO, detail mask, smoothness. No albedo mottling.
                masks[index] = new Color(.2f, 1, 1, .16f + (PeriodicNoise(x / 16f, y / 16f, 32) - .5f) * .05f);
            }
            var normal = SaveSurfaceMap("FloorFineNormal", normals, true, size);
            var mask = SaveSurfaceMap("FloorSurfaceMask", masks, false, size);
            foreach (string name in new[] { "FloorField", "FloorAccess" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
                Undo.RecordObject(material, "Refine floor powder coating");
                material.SetTexture("_NormalMap", normal); material.SetFloat("_NormalScale", .8f);
                material.SetTexture("_MaskMap", mask);
                material.SetFloat("_MetallicRemapMin", 0); material.SetFloat("_MetallicRemapMax", 1);
                material.SetFloat("_AORemapMin", 0); material.SetFloat("_AORemapMax", 1);
                material.SetFloat("_SmoothnessRemapMin", name == "FloorAccess" ? .02f : 0);
                material.SetFloat("_SmoothnessRemapMax", 1);
                HDMaterial.ValidateMaterial(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static readonly string[] _primitiveFloorNames = { "Ground 36 x 44", "Gallery 1.5m", "Gallery south ramp", "Gallery north ramp" };

        private static void ArchivePrimitiveFloorRenderers()
        {
            Directory.CreateDirectory(Output);
            string path = Output + "/primitive-renderers-before.txt";
            var archive = new StringBuilder();
            foreach (string name in _primitiveFloorNames)
            {
                var renderer = Find(name).GetComponent<MeshRenderer>();
                if (renderer == null) throw new InvalidOperationException("Missing original floor renderer: " + name);
                archive.AppendLine(HierarchyPath(renderer.transform) + " | enabled=" + renderer.enabled);
                Undo.RecordObject(renderer, "Remove redundant floor backing from rendering");
                renderer.enabled = false;
                if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (!File.Exists(path)) File.WriteAllText(path, archive.ToString());
        }

        private static Texture2D SaveSurfaceMap(string name, Color[] pixels, bool normal, int size)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try { texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
            importer.maxTextureSize = size; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Grain(float x, float y) => .75f * PeriodicNoise(x / 4, y / 4, 128) + .25f * PeriodicNoise(x / 16, y / 16, 32);

        private static float PeriodicNoise(float x, float y, int period)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float u = x - ix, v = y - iy;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, period), Hash(ix + 1, iy, period), u),
                Mathf.Lerp(Hash(ix, iy + 1, period), Hash(ix + 1, iy + 1, period), u), v);
        }

        private static float Hash(int x, int y, int period)
        {
            x = (x % period + period) % period; y = (y % period + period) % period;
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + 8039);
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                return (value ^ (value >> 16)) / (float)uint.MaxValue;
            }
        }

        private static void Configure(string name)
        {
            string path = Folder + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Export quiet-floor Blender models first: " + name);
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    var target = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + material.name + ".mat") ??
                        AssetDatabase.LoadAssetAtPath<Material>(Art + "/CornerSample/Materials/" + material.name + ".mat");
                    if (target == null) throw new InvalidOperationException("Unknown floor/body material: " + material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), target);
                }
            importer.SaveAndReimport();
        }

        private static void Add(string path, string name, Transform parent, Transform source)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Missing floor/body model " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            if (source != null)
            {
                instance.transform.SetPositionAndRotation(source.position, source.rotation);
                instance.transform.localScale = source.lossyScale;
            }
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Quiet Floor Visuals")]
        public static void Validate()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            var root = Find(RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != Folder + "/PF_QuietFloor.prefab")
                throw new InvalidOperationException("Quiet floor prefab link is missing.");
            if (ProtectedAssets() != File.ReadAllText(Folder + "/ProtectedArtBefore.sha256.txt"))
                throw new InvalidOperationException("Earlier accepted art, floor archives or lighting changed.");
            if (!BattlefieldMarketRoom.MatchesGameplay(scene, Folder + "/GameplayBefore.sha256.txt"))
                throw new InvalidOperationException("Floor visuals changed gameplay/collision/navigation data.");
            if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Quiet floor must contain only visual mesh components.");
            if (_primitiveFloorNames.Any(name => Find(name).GetComponent<MeshRenderer>().enabled))
                throw new InvalidOperationException("Redundant primitive floor still renders underneath the joints.");
            foreach (string old in new[] { "08 Art - Entry service zone", "11 Art - Energy service loop deck" })
                if (Find(old).GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled)) throw new InvalidOperationException("Old noisy deck still renders: " + old);
            foreach (string old in new[] { "07 Art sample - Energy corner", "09 Art - Service gallery" })
                if (Find(old).GetComponentsInChildren<MeshRenderer>(true).Any(r => r.GetComponent<TMP_Text>() == null && r.enabled))
                    throw new InvalidOperationException("Old mixed deck still renders: " + old);
            int total = 0, floorTriangles = 0;
            var floor = root.Find("Calm floor and gallery - Blender");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount ||
                    renderer == null || !renderer.enabled || renderer.sharedMaterials.Any(m => m == null || m.shader.name != "HDRP/Lit"))
                    throw new InvalidOperationException("Floor/body mesh or HDRP references are incomplete: " + filter.name);
                total += mesh.triangles.Length / 3;
                if (!filter.transform.IsChildOf(floor)) continue;
                floorTriangles += mesh.triangles.Length / 3;
                foreach (var v in mesh.vertices)
                {
                    var p = filter.transform.TransformPoint(v);
                    float h = p.x >= 12.999f && p.z >= 32 && p.z <= 56 ? GalleryHeight(p.z) : 0;
                    if (p.x < -18.001f || p.x > 18.001f || p.z < 19.999f || p.z > 64.001f || p.y - h < -.001f || p.y - h > .0161f)
                        throw new InvalidOperationException("Quiet floor surface envelope failed: " + p);
                }
            }
            if (floorTriangles > 10000 || total > 120000) throw new InvalidOperationException("Quiet-floor geometry budget exceeded.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/visual-validation.txt", "PASS legacy assets/accepted walls/lighting and gameplay checksum unchanged.\n" +
                "Old detailed deck renderers disabled; body-only energy/gallery and entry screen retained. No new collider/light/runtime script.\n" +
                "Floor triangles=" + floorTriangles + "; assembly incl retained bodies=" + total + "\n" +
                "Two service covers, six large gallery/ramp sheets, 40mm low-contrast joints, 14mm visual surface offset.\nNo combat/AI/pathfinding tests.\n" + EnemyValidationRunner.ConsoleCounts());
            BattlefieldInteriorFinish.ValidateVisuals();
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Quiet Floor")]
        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout(); Directory.CreateDirectory(Output);
            BattlefieldInteriorFinish.Capture();
            foreach (string path in Directory.GetFiles("Logs/BattlefieldInteriorFinish", "*.png")) File.Copy(path, Output + "/" + System.IO.Path.GetFileName(path), true);
            View("floor-close", new Vector3(7, 1.7f, 33.5f), new Vector3(8, .014f, 37.5f));
            View("gallery-floor", new Vector3(16.8f, 3.1f, 40.5f), new Vector3(15.2f, 1.51f, 46));
            View("ramp-transition", new Vector3(15.8f, 1.8f, 30), new Vector3(15.6f, .6f, 35));
            View("service-cover", new Vector3(-7.6f, 1.5f, 38), new Vector3(-8.7f, .014f, 40));
            View("floor-material", new Vector3(7.6f, .38f, 35), new Vector3(7.9f, .014f, 35.7f));
            void View(string name, Vector3 p, Vector3 target) => BattlefieldBaseline.Capture(scene, name, p, Quaternion.LookRotation(target - p), false, Output, temporalAntialiasing: true);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Floor Camera Sweep")]
        public static void CaptureMotion()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            string output = Output + "/CameraSweep"; Directory.CreateDirectory(output);
            var obj = new GameObject("Temporary floor inspection camera") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(obj, scene);
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var prior = RenderTexture.active;
            Texture2D frame = null;
            try
            {
                var camera = obj.AddComponent<Camera>(); camera.enabled = false;
                camera.fieldOfView = 75; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                obj.AddComponent<HDAdditionalCameraData>().antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                target.Create(); frame = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("HDRP camera sweep unavailable.");
                obj.transform.SetPositionAndRotation(new Vector3(7, 1.7f, 33.5f), Quaternion.LookRotation(new Vector3(1.5f, -1.686f, 5.5f)));
                for (int i = 0; i < 24; i++) RenderPipeline.SubmitRenderRequest(camera, request);
                for (int i = 0; i < 48; i++)
                {
                    Vector3 p = new Vector3(7 + i * .025f, 1.7f, 33.5f + i * .04f);
                    obj.transform.SetPositionAndRotation(p, Quaternion.LookRotation(new Vector3(8.5f, .014f, 39) - p));
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target; frame.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); frame.Apply();
                    File.WriteAllBytes(output + "/frame-" + i.ToString("D3") + ".png", frame.EncodeToPNG());
                }
                File.WriteAllText(output + "/capture.txt", "48 persistent-camera HDRP/TAA frames, eye height1.7m, forward/side translation. Editor-only; no Play Mode, actors or collision changes.");
            }
            finally
            {
                RenderTexture.active = prior;
                if (frame != null) Object.DestroyImmediate(frame);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(obj);
            }
        }

        private static float GalleryHeight(float z) => z < 40 ? (z - 32) * .1875f : z <= 48 ? 1.5f : (56 - z) * .1875f;
        private static Transform Find(string name) => BattlefieldBlockout.RequireBlockout().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == name);
        private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before floor authoring.");
        }

        private static string ProtectedAssets()
        {
            using var sha = SHA256.Create();
            var text = new StringBuilder();
            foreach (string folder in Directory.GetDirectories(Art).Where(p => !p.EndsWith("QuietFloor", StringComparison.Ordinal) && !p.EndsWith("MarketRoom", StringComparison.Ordinal)).OrderBy(p => p))
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta")).OrderBy(p => p))
                    text.AppendLine(path + ":" + Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))));
            const string ground = "Assets/Project/Scenes/BattlefieldBlockout/Materials/Ground.mat";
            text.AppendLine(ground + ":" + Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(ground))));
            return text.ToString();
        }
    }
}
