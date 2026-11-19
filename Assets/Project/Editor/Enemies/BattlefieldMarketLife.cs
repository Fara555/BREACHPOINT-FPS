using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Breachpoint.Gameplay.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Breachpoint.Editor.Enemies
{
    public static class BattlefieldMarketLife
    {
        internal const string Folder = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Life";
        internal const string RootName = "15 Art - Market daily use";
        internal const string Output = "Logs/BattlefieldMarketLife";
        private const string Market = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom";

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Add Market Daily Use")]
        public static void Create()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout();
            if (FindRoot() != null) { Validate(); Capture(); return; }
            Directory.CreateDirectory(Output);
            string gameplay = BattlefieldCornerArt.GameplayFingerprint(scene);
            if (gameplay != File.ReadAllText(Market + "/GameplayAfter.sha256.txt"))
                throw new InvalidOperationException("Inspect gameplay changes before adding the visual props.");
            File.Copy(scene.path, Output + "/scene-before-life.unity", true);
            File.WriteAllText(Folder + "/GameplayBefore.txt", gameplay);
            File.WriteAllText(Folder + "/AcceptedArtBefore.txt", AcceptedArt());
            AssetDatabase.ImportAsset(Folder + "/GameplayBefore.txt", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Folder + "/AcceptedArtBefore.txt", ImportAssetOptions.ForceSynchronousImport);
            Material("LifePaper", new Color(.42f, .40f, .33f), .12f);
            Material("LifeDrink", new Color(.015f, .009f, .004f), .7f);
            Configure("DailyUse"); Configure("FanRotor");
            var parent = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "Blockout B - Service Quarter");
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(root, "Add market daily use");
            var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/DailyUse.fbx"), root.transform);
            body.name = "Daily use props and guarded fan body";
            foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            var movement = new GameObject("Desk fan movement"); movement.transform.SetParent(root.transform, false);
            movement.transform.position = new Vector3(-11.65f, 1.51f, 43.32f);
            var pivot = new GameObject("Fan rotor pivot").transform; pivot.SetParent(movement.transform, false);
            var rotor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/FanRotor.fbx"), pivot);
            rotor.name = "Swept fan blades"; rotor.transform.localPosition = Vector3.zero;
            var fan = movement.AddComponent<AmbientFan>();
            var data = new SerializedObject(fan); data.FindProperty("_rotor").objectReferenceValue = pivot;
            data.FindProperty("_degreesPerSecond").floatValue = 160; data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Folder + "/PF_MarketDailyUse.prefab", InteractionMode.AutomatedAction);
            Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Daily use scene could not be saved.");
            Capture();
        }

        public static void Validate()
        {
            RequireIdle();
            var scene = BattlefieldBlockout.RequireBlockout(); var root = FindRoot();
            if (root == null) throw new InvalidOperationException("Create market daily use first.");
            if (!MarketServiceDoorAuthoring.MatchesGameplay(scene, File.ReadAllText(Folder + "/GameplayBefore.txt")))
                throw new InvalidOperationException("Daily-use art changed gameplay, actor wiring or navigation bytes.");
            if (File.ReadAllText(Folder + "/AcceptedArtBefore.txt") != AcceptedArt())
                throw new InvalidOperationException("Previously accepted market art files changed.");
            if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Daily-use props must add no colliders or lights.");
            var fans = root.GetComponentsInChildren<AmbientFan>();
            if (fans.Length != 1 || !fans[0].enabled) throw new InvalidOperationException("Exactly one enabled fan is expected.");
            var rotor = (Transform)new SerializedObject(fans[0]).FindProperty("_rotor").objectReferenceValue;
            if (rotor == null || !rotor.IsChildOf(root.transform)) throw new InvalidOperationException("Fan rotor wiring is missing.");
            Bounds bounds = new Bounds(rotor.position, Vector3.zero);
            foreach (var renderer in rotor.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.gameObject.isStatic) throw new InvalidOperationException("Rotating fan blades must not be static.");
                bounds.Encapsulate(renderer.bounds);
            }
            Vector3 center=Vector3.zero;int vertices=0;
            foreach (var filter in rotor.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices) {center+=filter.transform.TransformPoint(vertex);vertices++;}
            center/=Mathf.Max(1,vertices);
            if (bounds.size.x > .04f || bounds.size.y < .21f || bounds.size.z < .21f ||
                bounds.size.y > .29f || bounds.size.z > .29f || Vector3.Distance(center, rotor.position) > .01f)
                throw new InvalidOperationException("Fan rotor axis/center differs from the guard.");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterials.Any(m => m == null)) throw new InvalidOperationException("Missing daily-use material.");
                Bounds b = renderer.bounds;
                if (b.min.x < -12.65f || b.max.x > -11.3f || b.min.y < 1.14f || b.max.y > 1.71f)
                    throw new InvalidOperationException("Daily-use props left their counter envelopes: " + renderer.name);
            }
            int triangles = root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 18000) throw new InvalidOperationException("Daily-use mesh budget exceeded.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/validation.txt", "PASS accepted art files and exact gameplay/navigation fingerprint unchanged.\n" +
                "PASS counter envelopes, fan axis/center, serialized rotor, no added lights/colliders, dynamic rotor flags.\n" +
                "Daily-use triangles=" + triangles + "\n" + EnemyValidationRunner.ConsoleCounts());
        }

        public static void Capture()
        {
            var scene = BattlefieldBlockout.RequireBlockout();
            View("checkout-daily-use", new Vector3(-13.15f, 1.96f, 44.30f), new Vector3(-12, 1.42f, 44.25f));
            View("packing-daily-use", new Vector3(-13.6f, 1.90f, 38), new Vector3(-12, 1.22f, 36.9f));
            View("guarded-fan", new Vector3(-12.45f, 1.70f, 43.10f), new Vector3(-11.65f, 1.48f, 43.32f));
            View("interior-daily-use", new Vector3(-14.5f, 1.7f, 36), new Vector3(-14.2f, 1.9f, 45.5f));
            void View(string name, Vector3 p, Vector3 target) => BattlefieldBaseline.Capture(scene, name, p, Quaternion.LookRotation(target-p), false, Output, temporalAntialiasing:true);
        }

        internal static GameObject FindRoot() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects()
            .SelectMany(o => o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == RootName)?.gameObject;

        private static void Material(string name, Color color, float smoothness)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("HDRP/Lit")) {name=name}; AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_EnableGeometricSpecularAA",1); HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void Configure(string name)
        {
            string path = Folder + "/Models/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;importer.addCollider=false;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    var target=new[] {Folder+"/Materials/",Market+"/Materials/"}.Select(p=>AssetDatabase.LoadAssetAtPath<Material>(p+material.name+".mat")).FirstOrDefault(m=>m!=null);
                    if (target==null) throw new InvalidOperationException("Missing daily-use material "+material.name);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material),target);
                }
            importer.SaveAndReimport();
        }

        private static string AcceptedArt()
        {
            using var hash=SHA256.Create();var text=new StringBuilder();
            foreach (string path in Directory.GetFiles(Market,"*",SearchOption.AllDirectories)
                .Where(p=>!p.Replace('\\','/').StartsWith(Folder+"/",StringComparison.Ordinal)&&!p.EndsWith(".meta")).OrderBy(p=>p))
                text.AppendLine(path+":"+Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
            return text.ToString();
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before daily-use authoring.");
        }
    }
}
