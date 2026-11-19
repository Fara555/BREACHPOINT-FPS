using System;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Player.Input;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    // Read-only scene evidence for the first battlefield design milestone.
    public static partial class BattlefieldBaseline
    {
        internal const string ArenaPath = "Assets/Project/Scenes/EnemyArena/EnemyArena.unity";
        internal const string Output = "Logs/BattlefieldBaseline";

        [Serializable] private sealed class SceneEvidence
        {
            public string scene;
            public string utc;
            public Vector3 playerStart;
            public Geometry[] geometry;
            public Actor[] actors;
            public Cover[] covers;
        }

        [Serializable] private sealed class Geometry
        {
            public string path;
            public Vector3 center;
            public Vector3 size;
        }

        [Serializable] private sealed class Actor
        {
            public string name;
            public Vector3 position;
            public string squad;
            public string encounter;
            public string prefab;
        }

        [Serializable] private sealed class Cover
        {
            public string name;
            public string kind;
            public Vector3 protectedPosition;
            public Vector3 exposurePosition;
            public bool usable;
            public bool protectedReachable;
            public bool exposureReachable;
        }

        [MenuItem("Breachpoint/Level Design/Capture Current Arena Baseline")]
        public static void Audit()
        {
            RequireIdleEditor();
            Directory.CreateDirectory(Output);
            Scene prior = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ArenaPath);
            bool temporary = !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Additive);
            ulong priorMask = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                SceneManager.SetActiveScene(scene);
                Physics.SyncTransforms();
                GameObject[] roots = scene.GetRootGameObjects();
                PlayerInputReader player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerInputReader>()).Single();
                EnemyCoverPoint[] covers = roots.SelectMany(r => r.GetComponentsInChildren<EnemyCoverPoint>()).ToArray();
                EnemyActor[] actors = roots.SelectMany(r => r.GetComponentsInChildren<EnemyActor>()).ToArray();
                var evidence = new SceneEvidence
                {
                    scene = scene.path,
                    utc = DateTime.UtcNow.ToString("O"),
                    playerStart = player.transform.position,
                    geometry = roots.SelectMany(r => r.GetComponentsInChildren<Collider>())
                        .Where(c => !c.isTrigger && c.GetComponentInParent<EnemyActor>() == null &&
                            c.GetComponentInParent<PlayerInputReader>() == null)
                        .Select(c => new Geometry { path = HierarchyPath(c.transform), center = c.bounds.center, size = c.bounds.size }).ToArray(),
                    actors = actors.Select(a => new Actor { name = a.name, position = a.transform.position,
                        squad = a.SquadId, encounter = a.EncounterId, prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(a.gameObject) }).ToArray(),
                    covers = covers.Select(c => new Cover { name = c.name, kind = c.Kind.ToString(),
                        protectedPosition = c.ProtectedPosition, exposurePosition = c.ExposurePosition, usable = c.IsUsable,
                        protectedReachable = Reachable(player.transform.position, c.ProtectedPosition),
                        exposureReachable = Reachable(player.transform.position, c.ExposurePosition) }).ToArray()
                };
                File.WriteAllText(Output + "/scene.json", JsonUtility.ToJson(evidence, true));
                var report = new StringBuilder("Arena baseline | " + evidence.utc + "\n");
                report.AppendLine("Scene: " + scene.path + " | start=" + player.transform.position);
                report.AppendLine("Actors=" + actors.Length + ", squads=" + actors.Select(a => a.SquadId).Distinct().Count() +
                    ", cover points=" + covers.Length + ", usable=" + evidence.covers.Count(c => c.usable));
                report.AppendLine("Reachable protected/exposure=" + evidence.covers.Count(c => c.protectedReachable) + "/" +
                    evidence.covers.Count(c => c.exposureReachable) + " of " + covers.Length + " each (existing bake; no rebake).");
                foreach (var actor in actors)
                    report.AppendLine("ACTOR " + actor.name + " | " + actor.transform.position + " | squad=" + actor.SquadId + " | encounter=" + actor.EncounterId);
                foreach (var surface in roots.SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>()))
                    report.AppendLine("NAV " + HierarchyPath(surface.transform) + " | data=" + AssetDatabase.GetAssetPath(surface.navMeshData) +
                        " | collection=" + surface.collectObjects + " | geometry=" + surface.useGeometry + " | agent=" + surface.agentTypeID);
                var scope = roots.SelectMany(r => r.GetComponentsInChildren<GameLifetimeScope>()).Single();
                var assigned = new SerializedObject(scope).FindProperty("_coverPoints");
                report.AppendLine("Composition cover references=" + assigned.arraySize);
                foreach (var item in evidence.covers.Where(c => !c.usable || !c.protectedReachable || !c.exposureReachable))
                    report.AppendLine("CHECK " + item.name + " | usable=" + item.usable + " | protected=" + item.protectedReachable + " | exposure=" + item.exposureReachable);
                report.AppendLine(EnemyValidationRunner.ConsoleCounts());
                report.AppendLine(EnemyValidationRunner.ConsoleDetails());
                File.WriteAllText(Output + "/scene-audit.txt", report.ToString());
                Capture(scene, "overview", new Vector3(0, 75, 0), Quaternion.Euler(90, 0, 0), true);
                Capture(scene, "entry", player.GetComponentInChildren<Camera>().transform.position, Quaternion.Euler(0, 0, 0), false);
                Capture(scene, "west-lane", new Vector3(-23, 1.65f, -17), Quaternion.Euler(0, 20, 0), false);
                Capture(scene, "overlook", new Vector3(-23, 3.15f, 20), Quaternion.Euler(4, 100, 0), false);
                File.AppendAllText(Output + "/scene-audit.txt", "Captures completed.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
            }
            finally
            {
                EditorSceneManager.SetSceneCullingMask(scene, priorMask);
                if (prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior);
                if (temporary) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool Reachable(Vector3 start, Vector3 destination)
        {
            if (!NavMesh.SamplePosition(start, out NavMeshHit from, 2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(destination, out NavMeshHit to, .15f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        internal static void Capture(Scene scene, string name, Vector3 position, Quaternion rotation, bool overhead, string outputFolder = Output, float orthographicSize = 37, bool temporalAntialiasing = false)
        {
            var cameraObject = new GameObject("Temporary baseline camera") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            RenderTexture prior = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = 75;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 200;
                camera.orthographic = overhead;
                camera.orthographicSize = orthographicSize;
                var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
                if (temporalAntialiasing) cameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("HDRP render requests unavailable.");
                // Warm camera history before exporting fixed-exposure evidence.
                for (int i = 0; i < (temporalAntialiasing ? 24 : 3); i++) RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(outputFolder + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prior;
                if (image != null) Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        internal static void RequireIdleEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation/import before baseline work.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save unsaved scene work before switching scenes for the baseline.");
        }

        private static string HierarchyPath(Transform value) => value.parent == null ? value.name : HierarchyPath(value.parent) + "/" + value.name;
    }
}
