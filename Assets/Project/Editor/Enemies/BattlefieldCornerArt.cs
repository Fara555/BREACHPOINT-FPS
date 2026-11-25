using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Player.Input;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class BattlefieldCornerArt
    {
        private const string RootName = "07 Art sample - Energy corner";
        private const string Folder = BattlefieldCornerMaterials.Folder;
        private const string PrefabPath = Folder + "/PF_EnergyCorner_Art.prefab";
        private const string BaselinePath = Folder + "/GameplayBaseline.txt";
        private const string Output = "Logs/BattlefieldCornerArt";
        private static readonly Vector3 _cornerPosition = new Vector3(4, 0, 36);

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Create Energy Corner Sample")]
        public static void Create()
        {
            BattlefieldBaseline.RequireIdleEditor();
            Scene scene = BattlefieldBlockout.RequireBlockout();
            Transform parent = BattlefieldBlockout.FindInScene<NavMeshSurface>(scene).Single().transform.parent;
            if (parent.Find(RootName) != null)
            {
                if (!File.Exists(BaselinePath)) SaveBaseline(File.ReadAllText(Output + "/gameplay-before.sha256"), scene);
                Validate(); Capture(); return;
            }
            string before = GameplayFingerprint(scene);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/gameplay-before.sha256", before);
            if (File.Exists(PrefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(existing, parent);
                instance.name = RootName;
            }
            else
            {
                if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Incomplete art sample assets already exist; inspect them before creating more.");
                foreach (string path in new[] { Folder, Folder + "/Textures", Folder + "/Materials", Folder + "/Meshes" })
                    BattlefieldCornerMaterials.EnsureFolder(path);
                Build(parent);
            }
            if (GameplayFingerprint(scene) != before) throw new InvalidOperationException("Art sample changed gameplay geometry or actor wiring.");
            SaveBaseline(before, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Art sample scene save failed.");
            Validate();
            Capture();
        }

        private static void SaveBaseline(string fingerprint, Scene scene)
        {
            if (GameplayFingerprint(scene) != fingerprint) throw new InvalidOperationException("Cannot store an unverified gameplay baseline.");
            if (File.Exists(BaselinePath)) return;
            File.WriteAllText(BaselinePath, fingerprint);
            AssetDatabase.ImportAsset(BaselinePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void Build(Transform parent)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.position = _cornerPosition;
            Material concrete = BattlefieldCornerMaterials.Surface("Concrete", new Color(.54f, .52f, .47f), 0, .28f, 11, .7f);
            Material paint = BattlefieldCornerMaterials.Surface("CoatedMetal", new Color(.16f, .22f, .24f), .24f, .43f, 29, .8f);
            Material steel = BattlefieldCornerMaterials.Surface("BrushedSteel", new Color(.34f, .38f, .4f), .75f, .57f, 47, .5f);
            Material dark = BattlefieldCornerMaterials.Plain("Recess", new Color(.025f, .038f, .043f), .1f, .3f);
            Material copper = BattlefieldCornerMaterials.Plain("OxidizedCopper", new Color(.29f, .15f, .07f), .6f, .34f);
            Material cyan = BattlefieldCornerMaterials.Plain("CyanIndicator", new Color(.2f, .8f, .91f), .1f, .55f, 8);
            Material amber = BattlefieldCornerMaterials.Plain("WarmIndicator", new Color(1, .57f, .18f), .1f, .5f, 5);
            Material stencil = BattlefieldCornerMaterials.Plain("MarkingPaint", new Color(.7f, .64f, .44f), 0, .3f);
            Material glass = BattlefieldCornerMaterials.Plain("DisplayGlass", new Color(.02f, .05f, .065f), .15f, .8f);

            foreach (bool east in new[] { false, true })
            {
                float[] centers = east ? new[] { 37f, 39f, 41f } : new[] { -1f, 1f, 3f };
                foreach (float u in centers)
                {
                    FaceBox(root, east, u, .32f, .035f, 1.98f, .64f, .06f, concrete);
                    FaceBox(root, east, u, 2.1f, .035f, 1.96f, 2.82f, .06f, dark);
                    FaceBox(root, east, u, 2.1f, .07f, 1.82f, 2.68f, .04f, paint);
                    FaceBox(root, east, u, 3.94f, .035f, 1.98f, .7f, .06f, paint);
                    foreach (float du in new[] { -.82f, .82f })
                        foreach (float y in new[] { .92f, 3.27f })
                            Bolt(root, east, u + du, y, steel);
                }
                for (int i = 0; i <= 3; i++)
                    FaceBox(root, east, east ? 36 + i * 2 : -2 + i * 2, 2.1f, .08f, .055f, 4.2f, .035f, steel);
                FaceBox(root, east, east ? 39 : 1, 4.34f, .07f, 6.05f, .13f, .08f, steel);
                FaceBox(root, east, east ? 39 : 1, .72f, .07f, 6.05f, .09f, .06f, steel);
            }

            // Front elevation: identity, ventilated service bay and a closed electrical cabinet.
            Label(root, "07", new Vector3(-1, 2.1f, 35.885f), Quaternion.identity, 10, new Vector2(1.8f, 1.6f), new Color(.83f, .86f, .8f));
            Label(root, "ENERGY NODE", new Vector3(-1, 1.03f, 35.88f), Quaternion.identity, 1.5f, new Vector2(1.65f, .3f), new Color(.72f, .76f, .73f));
            Label(root, "ION // GRID SYSTEMS", new Vector3(1, 3.96f, 35.88f), Quaternion.identity, 4.5f, new Vector2(5.5f, .5f), new Color(.77f, .89f, .9f));
            FaceBox(root, false, 1, 2.15f, .095f, 1.5f, 1.68f, .04f, dark);
            for (int i = 0; i < 14; i++) FaceBox(root, false, 1, 1.4f + i * .115f, .12f, 1.4f, .052f, .035f, steel);
            FaceBox(root, false, 1, 3.2f, .11f, .65f, .06f, .02f, amber);
            FaceBox(root, false, 3, 2.05f, .1f, 1.35f, 2.1f, .035f, steel);
            FaceBox(root, false, 3, 2.62f, .122f, 1.12f, .65f, .018f, glass);
            Label(root, "LOAD 72%\nSYSTEM ONLINE", new Vector3(3, 2.62f, 35.86f), Quaternion.identity, 1.2f, new Vector2(.99f, .5f), new Color(.47f, .85f, .83f));
            FaceBox(root, false, 3, 1.73f, .122f, 1.1f, .7f, .018f, dark);
            for (int i = 0; i < 5; i++) FaceBox(root, false, 2.64f + i * .18f, 1.73f, .137f, .055f, .35f, .012f, i == 4 ? amber : cyan);
            Label(root, "HIGH VOLTAGE", new Vector3(3, 1.11f, 35.86f), Quaternion.identity, 1.4f, new Vector2(1.1f, .25f), new Color(.91f, .65f, .28f));

            // East elevation: short conduits, access panels and functional wayfinding.
            FaceBox(root, true, 39, 2.1f, .09f, 1.5f, 1.6f, .025f, dark);
            for (int i = 0; i < 10; i++) FaceBox(root, true, 39, 1.4f + i * .15f, .115f, 1.4f, .06f, .025f, steel);
            Label(root, "DISTRIBUTION / 07", new Vector3(4.12f, 3.96f, 39), Quaternion.Euler(0, -90, 0), 3.5f, new Vector2(5.6f, .45f), new Color(.77f, .89f, .9f));
            Label(root, "SERVICE ACCESS", new Vector3(4.115f, 1.05f, 37), Quaternion.Euler(0, -90, 0), 1.5f, new Vector2(1.7f, .35f), new Color(.72f, .76f, .73f));
            Pipe(root, new Vector3(4.08f, .85f, 40.75f), new Vector3(4.08f, 3.45f, 40.75f), .028f, copper);
            Pipe(root, new Vector3(4.08f, .85f, 41.05f), new Vector3(4.08f, 3.45f, 41.05f), .028f, copper);
            Pipe(root, new Vector3(4.08f, 3.45f, 40.75f), new Vector3(4.08f, 3.45f, 37), .028f, copper);
            foreach (float z in new[] { 37f, 38.5f, 40f, 41f }) FaceBox(root, true, z, 3.45f, .112f, .09f, .16f, .016f, steel);

            // A small finished surface patch and gallery fascia establish material scale.
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 4; z++)
                    Cube(root, "Concrete paving", new Vector3(5 + x * 2, .006f, 32 + z * 2), new Vector3(1.98f, .012f, 1.98f), concrete);
            for (int i = 0; i < 7; i++)
                Cube(root, "Loading lane dash", new Vector3(12.3f, .014f, 32 + i), new Vector3(.1f, .008f, .6f), stencil);
            for (int i = 0; i < 8; i++)
            {
                var stripe = Cube(root, "Hazard corner marking", new Vector3(4.55f + i * .23f, .015f, 34.5f), new Vector3(.09f, .008f, .8f), stencil);
                stripe.rotation = Quaternion.Euler(0, -30, 0);
            }
            Cube(root, "Gallery start fascia", new Vector3(12.975f, .82f, 41.6f), new Vector3(.05f, 1.25f, 3.2f), paint);
            for (int i = 0; i < 4; i++) Cube(root, "Gallery fascia rib", new Vector3(12.94f, .8f, 40.15f + i), new Vector3(.035f, 1.38f, .04f), steel);
            Cube(root, "Gallery task-light housing", new Vector3(13.25f, 2.82f, 40.15f), new Vector3(.18f, .15f, 1.45f), steel);
            Cube(root, "Gallery warm task light", new Vector3(13.14f, 2.8f, 40.15f), new Vector3(.025f, .07f, 1.25f), amber);
            Cube(root, "Energy status strip", new Vector3(4.095f, 3.48f, 37.3f), new Vector3(.025f, .045f, 1.8f), cyan);
            AddLight(root, "Cabinet spill", new Vector3(4.6f, 2.9f, 36.9f), new Color(.45f, .8f, 1), 170, 5);
            AddLight(root, "Gallery task spill", new Vector3(12.8f, 2.75f, 40.1f), new Color(1, .7f, .4f), 120, 4);

            CombineGeometry(root);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create energy corner art sample");
        }

        private static Transform Cube(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            SceneManager.MoveGameObjectToScene(part, root.gameObject.scene);
            part.transform.SetParent(root, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        private static void FaceBox(Transform root, bool east, float u, float y, float offset, float width, float height, float depth, Material material)
        {
            Cube(root, material.name + " panel", east ? new Vector3(4 + offset, y, u) : new Vector3(u, y, 36 - offset),
                east ? new Vector3(depth, height, width) : new Vector3(width, height, depth), material);
        }

        private static void Bolt(Transform root, bool east, float u, float y, Material material)
        {
            Vector3 center = east ? new Vector3(4.1f, y, u) : new Vector3(u, y, 35.9f);
            Pipe(root, center - (east ? Vector3.right : Vector3.forward) * .009f,
                center + (east ? Vector3.right : Vector3.forward) * .009f, .023f, material);
        }

        private static void Pipe(Transform root, Vector3 start, Vector3 end, float radius, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = "Conduit / fastener";
            SceneManager.MoveGameObjectToScene(part, root.gameObject.scene);
            part.transform.SetParent(root, false);
            part.transform.SetPositionAndRotation((start + end) * .5f, Quaternion.FromToRotation(Vector3.up, (end - start).normalized));
            part.transform.localScale = new Vector3(radius * 2, Vector3.Distance(start, end) * .5f, radius * 2);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Label(Transform root, string text, Vector3 position, Quaternion rotation, float size, Vector2 bounds, Color color)
        {
            var label = new GameObject(text).AddComponent<TextMeshPro>();
            SceneManager.MoveGameObjectToScene(label.gameObject, root.gameObject.scene);
            label.transform.SetParent(root, false);
            label.transform.SetPositionAndRotation(position, rotation);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = bounds;
        }

        private static void AddLight(Transform root, string name, Vector3 position, Color color, float candela, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, root.gameObject.scene);
            light.transform.SetParent(root, false);
            light.transform.position = position;
            light.type = LightType.Point;
            if (light.GetComponent<HDAdditionalLightData>() == null) light.gameObject.AddComponent<HDAdditionalLightData>();
            light.lightUnit = LightUnit.Candela;
            light.intensity = candela;
            light.color = color;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CombineGeometry(Transform root, string meshPrefix = "SM_")
        {
            MeshFilter[] parts = root.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMP_Text>() == null).ToArray();
            foreach (var group in parts.GroupBy(f => f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var mesh = new Mesh { name = meshPrefix + group.Key.name };
                mesh.CombineMeshes(group.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "/Meshes/" + mesh.name + ".asset");
                var combined = new GameObject(group.Key.name);
                combined.transform.SetParent(root, false);
                combined.AddComponent<MeshFilter>().sharedMesh = mesh;
                combined.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                GameObjectUtility.SetStaticEditorFlags(combined, StaticEditorFlags.BatchingStatic);
            }
            foreach (var part in parts) Object.DestroyImmediate(part.gameObject);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Refine Sample Surface Maps")]
        public static void RefineSurfaceMaps()
        {
            BattlefieldBaseline.RequireIdleEditor();
            BattlefieldBlockout.RequireBlockout();
            BattlefieldCornerMaterials.Surface("Concrete", new Color(.54f, .52f, .47f), 0, .28f, 11, .7f);
            BattlefieldCornerMaterials.Surface("CoatedMetal", new Color(.16f, .22f, .24f), .24f, .43f, 29, .8f);
            BattlefieldCornerMaterials.Surface("BrushedSteel", new Color(.34f, .38f, .4f), .75f, .57f, 47, .5f);
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Add Weathered Cable Detail Layer")]
        public static void AddWeatheredDetails()
        {
            BattlefieldBaseline.RequireIdleEditor();
            BattlefieldBlockout.RequireBlockout();
            RefineSurfaceMaps();
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            Vector3 originalPosition = prefab.transform.position;
            try
            {
                if (prefab.transform.Find("Cable and repair layer") == null)
                {
                    prefab.transform.position = _cornerPosition;
                    var detail = new GameObject("Cable and repair layer").transform;
                    SceneManager.MoveGameObjectToScene(detail.gameObject, prefab.scene);
                    detail.SetParent(prefab.transform, false);
                    Material rubber = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Recess.mat");
                    Material steel = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/BrushedSteel.mat");
                    Material paint = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/CoatedMetal.mat");
                    Material copper = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/OxidizedCopper.mat");
                    for (int cable = 0; cable < 4; cable++)
                    {
                        float y = 3.56f + cable * .065f;
                        Pipe(detail, new Vector3(-1.9f, y, 35.78f), new Vector3(3.98f, y, 35.78f), .025f, rubber);
                        Vector3 previous = new Vector3(3.98f, y, 35.78f);
                        for (int i = 1; i <= 6; i++)
                        {
                            float angle = i * Mathf.PI / 12;
                            Vector3 next = new Vector3(3.98f + Mathf.Sin(angle) * .24f, y, 36.02f - Mathf.Cos(angle) * .24f);
                            Pipe(detail, previous, next, .025f, rubber);
                            previous = next;
                        }
                        float z = 41.35f + cable * .09f;
                        Pipe(detail, previous, new Vector3(4.22f, y, z), .025f, rubber);
                        Pipe(detail, new Vector3(4.22f, y, z), new Vector3(4.22f, .85f, z), .025f, rubber);
                    }
                    foreach (float x in new[] { -1.7f, .2f, 2f, 3.6f })
                    {
                        Cube(detail, "Cable saddle", new Vector3(x, 3.66f, 35.75f), new Vector3(.09f, .36f, .035f), steel);
                        Pipe(detail, new Vector3(x, 3.82f, 35.75f), new Vector3(x, 3.82f, 35.71f), .022f, copper);
                    }
                    foreach (float z in new[] { 36.4f, 38f, 39.8f })
                        Cube(detail, "Side cable saddle", new Vector3(4.25f, 3.66f, z), new Vector3(.035f, .36f, .09f), steel);
                    foreach (float y in new[] { 1.15f, 2.2f, 3.2f })
                        Cube(detail, "Downlead clamp", new Vector3(4.255f, y, 41.48f), new Vector3(.035f, .09f, .52f), copper);
                    Cube(detail, "Junction backplate", new Vector3(4.13f, 2.38f, 37), new Vector3(.05f, .94f, .92f), steel);
                    Cube(detail, "Junction box", new Vector3(4.19f, 2.38f, 37), new Vector3(.1f, .8f, .78f), paint);
                    for (int i = 0; i < 4; i++)
                        Cube(detail, "Junction cooling slit", new Vector3(4.248f, 2.2f + i * .105f, 37), new Vector3(.012f, .035f, .56f), rubber);
                    Pipe(detail, new Vector3(4.16f, 2.85f, 37), new Vector3(4.16f, 3.5f, 37), .034f, copper);
                    Cube(detail, "Field repair plate", new Vector3(-1.14f, 1.32f, 35.86f), new Vector3(.62f, .31f, .025f), steel);
                    foreach (float x in new[] { -1.39f, -.89f })
                        Pipe(detail, new Vector3(x, 1.32f, 35.85f), new Vector3(x, 1.32f, 35.82f), .025f, copper);
                    CombineGeometry(detail, "SM_Detail_");
                    prefab.transform.position = originalPosition;
                    PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        internal static string GameplayFingerprint(Scene scene, BoxCollider revisedCollider = null, Vector3? previousPosition = null, Vector3? previousSize = null)
        {
            var text = new StringBuilder();
            void Position(Vector3 p) => text.Append(p.ToString("F4", CultureInfo.InvariantCulture));
            var navigation = BattlefieldBlockout.FindInScene<NavMeshSurface>(scene).Single();
            foreach (var collider in navigation.GetComponentsInChildren<Collider>().OrderBy(c => c.name))
            {
                text.Append(collider.name).Append(collider.GetType().Name).Append(collider.enabled).Append(collider.isTrigger).Append(collider.gameObject.layer);
                Position(collider == revisedCollider && previousPosition.HasValue ? previousPosition.Value : collider.transform.position); Position(collider.transform.eulerAngles); Position(collider.transform.lossyScale);
                if (collider is BoxCollider box) { Position(box.center); Position(collider == revisedCollider && previousSize.HasValue ? previousSize.Value : box.size); }
                if (collider is MeshCollider mesh) { Position(mesh.sharedMesh.bounds.size); text.Append(mesh.sharedMesh.vertexCount); }
            }
            foreach (var actor in BattlefieldBlockout.FindInScene<EnemyActor>(scene).OrderBy(a => a.name))
            {
                text.Append(actor.name).Append(actor.SquadId).Append(actor.EncounterId).Append(AssetDatabase.GetAssetPath(actor.GetComponent<EnemyLifetimeScope>().Archetype));
                Position(actor.transform.position); Position(actor.transform.eulerAngles);
                for (int i = 0; i < actor.Route.Count; i++) { actor.Route.TryGetPoint(i, out Vector3 point); Position(point); }
            }
            foreach (var point in BattlefieldBlockout.FindInScene<EnemyCoverPoint>(scene).OrderBy(p => p.name))
            { text.Append(point.name).Append(point.Kind).Append(point.Protection.name); Position(point.ProtectedPosition); Position(point.ExposurePosition); Position(point.Normal); }
            Position(BattlefieldBlockout.FindInScene<PlayerInputReader>(scene).Single().transform.position);
            text.Append(Convert.ToBase64String(File.ReadAllBytes(AssetDatabase.GetAssetPath(navigation.navMeshData))));
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Validate Energy Corner Sample")]
        public static void Validate()
        {
            Scene scene = BattlefieldBlockout.RequireBlockout();
            var root = BattlefieldBlockout.FindInScene<NavMeshSurface>(scene).Single().transform.parent.Find(RootName);
            if (root == null) throw new InvalidOperationException("Create the art sample first.");
            ValidateDetailedModel(BattlefieldBlockout.LegacyMeshValidationRoot(root));
            if (root.GetComponentsInChildren<Collider>(true).Length != 0) throw new InvalidOperationException("Art sample must not introduce colliders.");
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) != PrefabPath) throw new InvalidOperationException("Art sample prefab connection is missing.");
            if (GameplayFingerprint(scene) != File.ReadAllText(BaselinePath)) throw new InvalidOperationException("Gameplay fingerprint differs from the pre-art baseline.");
            var lights = root.GetComponentsInChildren<Light>();
            if (lights.Length != 1 || lights.Any(l => l.shadows != LightShadows.None || l.type != LightType.Point))
                throw new InvalidOperationException("Art sample must contain only the attached unshadowed cabinet point light.");
            var labels = root.GetComponentsInChildren<TextMeshPro>(true);
            if (labels.Length != 3 || !labels.Select(label => label.text).OrderBy(text => text).SequenceEqual(new[] { "07", "72%", "ION" }))
                throw new InvalidOperationException("Art sample must contain only its three equipment labels.");
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node != root && node.childCount == 0 && node.GetComponents<Component>().Length == 1)
                    throw new InvalidOperationException("Empty leftover art node: " + node.name);
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 20 || renderers.Any(r => r.sharedMaterial == null || r.sharedMaterial.shader.name.Contains("InternalError")))
                throw new InvalidOperationException("Art sample renderer/material budget failed.");
            BattlefieldBlockout.Validate();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/validation.txt", "PASS unchanged gameplay fingerprint, connected art prefab, no new colliders, one attached cabinet light, three labels, no empty leftover nodes.\n" +
                "Renderers=" + renderers.Length + " | Combined meshes=" + root.GetComponentsInChildren<MeshFilter>().Count(f => f.GetComponent<TMP_Text>() == null) +
                "\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Art/Capture Energy Corner Sample")]
        public static void Capture()
        {
            Scene scene = BattlefieldBlockout.RequireBlockout();
            Directory.CreateDirectory(Output);
            ulong prior = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                foreach (var label in BattlefieldBlockout.FindInScene<TextMeshPro>(scene)) label.ForceMeshUpdate();
                View("corner", new Vector3(9.5f, 1.75f, 31.5f), new Vector3(1, 2.2f, 37.5f));
                View("front-detail", new Vector3(1, 1.7f, 32.8f), new Vector3(1, 2, 36));
                View("gallery-start", new Vector3(10, 1.7f, 37), new Vector3(14.2f, 1.8f, 41));
                View("deck-detail", new Vector3(7.2f, 1.65f, 31.7f), new Vector3(8.1f, 0, 34.7f));
                View("deck-hatch-close", new Vector3(9, .85f, 32.7f), new Vector3(9, .012f, 34));
                View("deck-hatch-grazing", new Vector3(9.04f, .30f, 32.7f), new Vector3(9, .012f, 34));
                void View(string name, Vector3 position, Vector3 lookAt) => BattlefieldBaseline.Capture(scene, name, position,
                    Quaternion.LookRotation(lookAt - position), false, Output);
            }
            finally { EditorSceneManager.SetSceneCullingMask(scene, prior); }
        }
    }
}
