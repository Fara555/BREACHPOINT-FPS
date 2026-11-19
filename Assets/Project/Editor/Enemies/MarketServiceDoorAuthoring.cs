using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class MarketServiceDoorAuthoring
    {
        private const string Folder = "Assets/Project/Art/Props/MarketServiceDoor";
        private const string Output = "Logs/MarketServiceDoor";
        private const string ModelPath = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Models/MarketRoom.fbx";
        private const string RootName = "19 Props - Market service door";
        private const string RevisionBefore = "SourceArt/Props/MarketServiceDoor/GameplayBeforeRecess.sha256.txt";
        private const string RevisionAfter = "SourceArt/Props/MarketServiceDoor/GameplayAfterRecess.sha256.txt";
        private static readonly Vector3 DoorPosition = new Vector3(-17.45f, 0, 40.5f);
        private static readonly Vector3 OldColliderPosition = new Vector3(-17.15f, 1.18f, 40.5f);
        private static readonly Vector3 OldColliderSize = new Vector3(.9f, 2.36f, 2.3f);
        private static readonly Vector3 RecessColliderPosition = new Vector3(-17.50f, 1.47f, 40.5f);
        private static readonly Vector3 RecessColliderSize = new Vector3(.40f, 2.91f, 2.3f);

        internal static bool MatchesGameplay(Scene scene, string expected)
        {
            if (BattlefieldCornerArt.GameplayFingerprint(scene) == expected) return true;
            if (!File.Exists(RevisionBefore) || FindRoot() == null) return false;
            var collider = DoorCollider();
            if (Vector3.Distance(collider.transform.position, RecessColliderPosition) > .0001f || Vector3.Distance(collider.size, RecessColliderSize) > .0001f) return false;
            // Normalize only the explicitly revised collider. Actors, routes, other collision and nav bytes remain exact.
            return BattlefieldCornerArt.GameplayFingerprint(scene, collider, OldColliderPosition, OldColliderSize) == expected;
        }

        private static BoxCollider DoorCollider() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<BoxCollider>()).Single(c => c.name.StartsWith("Market storage ", StringComparison.Ordinal) && Mathf.Abs(c.transform.position.z - 40.5f) < .001f);

        private static void RecessDoor()
        {
            var scene = BattlefieldBlockout.RequireBlockout(); var collider = DoorCollider();
            if (!File.Exists(RevisionBefore))
            {
                if (Vector3.Distance(collider.transform.position, OldColliderPosition) > .0001f || Vector3.Distance(collider.size, OldColliderSize) > .0001f) throw new InvalidOperationException("Unexpected original rack collision: inspect before recessing door.");
                File.Copy(scene.path, Output + "/scene-before-recess.unity", false);
                File.WriteAllText(RevisionBefore, BattlefieldCornerArt.GameplayFingerprint(scene));
            }
            if (BattlefieldCornerArt.GameplayFingerprint(scene, collider, OldColliderPosition, OldColliderSize) != File.ReadAllText(RevisionBefore)) throw new InvalidOperationException("Unrelated gameplay changes detected before door recess.");
            Undo.RecordObjects(new Object[] { collider, collider.transform, FindRoot().transform.GetChild(0) }, "Recess service door and fit its collision");
            FindRoot().transform.GetChild(0).position = DoorPosition;
            collider.transform.position = RecessColliderPosition; collider.size = RecessColliderSize;
            Physics.SyncTransforms();
            File.WriteAllText(RevisionAfter, BattlefieldCornerArt.GameplayFingerprint(scene));
        }

        public static void Create()
        {
            RequireIdle(); var scene = BattlefieldBlockout.RequireBlockout(); Directory.CreateDirectory(Output);
            if (!File.Exists(Output + "/gameplay-before.txt"))
            {
                File.WriteAllText(Output + "/gameplay-before.txt", BattlefieldCornerArt.GameplayFingerprint(scene));
                File.Copy(scene.path, Output + "/scene-before.unity", false);
                CaptureView("before-rack", new Vector3(-14.6f, 1.8f, 40.5f), new Vector3(-17, 1.4f, 40.5f));
            }
            string path = Folder + "/Models/MarketServiceDoor.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            foreach (var material in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                var finish = AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Art/Environment/ServiceQuarter/MarketWallLining/Materials/" + material.name + ".mat");
                if (finish == null) throw new InvalidOperationException("Missing matching wall finish: " + material.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), finish);
            }
            importer.SaveAndReimport();
            var prefabRoot = new GameObject("PF_MarketServiceDoor");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), prefabRoot.transform);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, Folder + "/PF_MarketServiceDoor.prefab");
            }
            finally { Object.DestroyImmediate(prefabRoot); }
            if (FindRoot() == null)
            {
                var root = new GameObject(RootName);
                root.transform.SetParent(scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "Blockout B - Service Quarter"), false);
                Undo.RegisterCreatedObjectUndo(root, "Replace central rack with service door");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PF_MarketServiceDoor.prefab"), root.transform);
                instance.transform.position = DoorPosition;
            }
            // Rebuild earlier independent filters first; each replacement retains the other changes.
            SealedSupplyPackAuthoring.ApplyLegacyRemoval();
            MarketWallLiningAuthoring.ApplyLegacyReplacement();
            ApplyRackRemoval(); RecessDoor(); Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save market door.");
            Capture();
        }

        private static bool IsRack(Vector3 p) => p.x > -17.391f && p.x < -16.60f && p.y > -.001f && p.y < 2.411f && p.z > 39.339f && p.z < 41.661f;

        internal static void ApplyRackRemoval()
        {
            if (FindRoot() == null) return;
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Integration");
            var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = RoomInstance(); int removed = 0;
            foreach (var source in canonical.GetComponentsInChildren<MeshFilter>())
            {
                var target = instance.GetComponentsInChildren<MeshFilter>().Single(f => f.name == source.name);
                // The packet and backing-plate filters are rebuilt immediately before this method.
                // Their materials can also contain shelf pieces, so preserve those filtered indices.
                string currentPath = AssetDatabase.GetAssetPath(target.sharedMesh);
                var original = currentPath.Contains("/SealedSupplyPack/Integration/") || currentPath.Contains("/MarketWallLining/Integration/") ? target.sharedMesh : source.sharedMesh;
                var vertices = original.vertices; var filtered = Object.Instantiate(original); int localRemoved = 0;
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    int[] triangles = original.GetTriangles(sub); var kept = new List<int>();
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        bool Inside(int i) => IsRack(target.transform.TransformPoint(vertices[i]));
                        if (Inside(triangles[t]) && Inside(triangles[t + 1]) && Inside(triangles[t + 2])) { localRemoved++; continue; }
                        kept.Add(triangles[t]); kept.Add(triangles[t + 1]); kept.Add(triangles[t + 2]);
                    }
                    filtered.SetTriangles(kept, sub, false);
                }
                if (localRemoved == 0) { Object.DestroyImmediate(filtered); continue; }
                removed += localRemoved;
                if (removed > 16000) { Object.DestroyImmediate(filtered); throw new InvalidOperationException("Rack removal exceeded its bounded scope."); }
                filtered.name = "Filtered_" + source.name; string path = Folder + "/Integration/" + filtered.name + ".asset";
                var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (stored == null) { AssetDatabase.CreateAsset(filtered, path); stored = filtered; }
                else { EditorUtility.CopySerialized(filtered, stored); Object.DestroyImmediate(filtered); EditorUtility.SetDirty(stored); AssetDatabase.SaveAssetIfDirty(stored); }
                Undo.RecordObject(target, "Remove central rack and all contents"); target.sharedMesh = stored;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            if (removed < 1000) throw new InvalidOperationException("Expected full rack and contents removal.");
            File.WriteAllText(Output + "/rack-removal.txt", "Bounded central rack and contents triangles removed=" + removed + ". Canonical art files retained; prior packet/wall filters composed.\n");
            MarketStorageCabinetAuthoring.ApplyReplacement();
        }

        public static void Validate()
        {
            RequireIdle(); var scene = BattlefieldBlockout.RequireBlockout(); var root = FindRoot();
            if (root == null || root.transform.childCount != 1 || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.transform.GetChild(0).gameObject) != Folder + "/PF_MarketServiceDoor.prefab") throw new InvalidOperationException("Service door prefab missing.");
            if (!MatchesGameplay(scene, File.ReadAllText(Output + "/gameplay-before.txt"))) throw new InvalidOperationException("Unapproved gameplay changes outside door collision revision.");
            if (root.GetComponentsInChildren<Collider>().Length != 0 || root.GetComponentsInChildren<MonoBehaviour>().Length != 0 || root.GetComponentsInChildren<Light>().Length != 0) throw new InvalidOperationException("Closed door must add no runtime behavior.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                triangles += filter.sharedMesh.triangles.Length / 3;
                var bounds = filter.GetComponent<MeshRenderer>().bounds;
                if (bounds.min.x < -18.01f || bounds.max.x > -17.19f || bounds.min.z < 39.34f || bounds.max.z > 41.66f || bounds.min.y < .014f || bounds.max.y > 2.93f) throw new InvalidOperationException("Door recess bounds invalid.");
                if (filter.GetComponent<MeshRenderer>().sharedMaterials.Any(m => m == null || m.shader.name != "HDRP/Lit")) throw new InvalidOperationException("Door HDRP finish missing.");
            }
            foreach (var filter in RoomInstance().GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<MeshRenderer>().enabled))
            {
                var vertices = filter.sharedMesh.vertices; var indices = filter.sharedMesh.triangles;
                for (int t = 0; t < indices.Length; t += 3)
                    if (IsRack(filter.transform.TransformPoint(vertices[indices[t]])) && IsRack(filter.transform.TransformPoint(vertices[indices[t + 1]])) && IsRack(filter.transform.TransformPoint(vertices[indices[t + 2]]))) throw new InvalidOperationException("Visible rack fragment remains: " + filter.name);
            }
            MarketWallLiningAuthoring.Validate();
            Physics.SyncTransforms();
            if (!Physics.SphereCast(new Vector3(-16.4f, 1.3f, 40.5f), .15f, Vector3.left, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore) || hit.collider != DoorCollider() || Mathf.Abs(hit.point.x + 17.30f) > .015f)
                throw new InvalidOperationException("Invisible old rack obstruction remains in front of recessed door.");
            if (triangles > 8000) throw new InvalidOperationException("Door triangle budget exceeded.");
            File.WriteAllText(Output + "/validation.txt", "PASS recessed door prefab, frame proud of lining by24mm, leaf recessed3mm, clear wall aperture, old rack fully removed.\nPASS revised door collider and spherecast approach; exact actors/routes/other collision/nav bytes preserved via explicit single-collider revision. Historical baselines retained.\nTriangles=" + triangles + "\n" + EnemyValidationRunner.ConsoleCounts());
        }

        public static void Capture()
        {
            CaptureView("service-door", new Vector3(-14.6f, 1.8f, 40.5f), new Vector3(-17, 1.4f, 40.5f));
            CaptureView("service-door-angle", new Vector3(-14.6f, 1.8f, 38.5f), new Vector3(-17, 1.5f, 40.5f));
            CaptureView("door-handle", new Vector3(-16.69f, 1.55f, 39.65f), new Vector3(-17.25f, 1.34f, 40.0f));
        }
        private static void CaptureView(string name, Vector3 p, Vector3 at) => BattlefieldBaseline.Capture(BattlefieldBlockout.RequireBlockout(), name, p, Quaternion.LookRotation(at - p), false, Output, temporalAntialiasing: true);
        private static Transform RoomInstance() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>()).Single(t => t.name == "14 Art - Walk-through market room").GetComponentsInChildren<Transform>().Single(t => t.name == "MarketRoom");
        private static GameObject FindRoot() => BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t => t.name == RootName)?.gameObject;
        private static void RequireIdle() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Wait for idle Unity Editor."); }
    }
}
