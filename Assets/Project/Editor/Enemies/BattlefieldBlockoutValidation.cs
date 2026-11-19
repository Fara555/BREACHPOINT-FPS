using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Player.Input;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Breachpoint.Editor.Enemies
{
    public static partial class BattlefieldBlockout
    {
        public static void ValidateGuards()
        {
            BattlefieldBaseline.RequireIdleEditor();
            Scene scene = RequireBlockout();
            string originalHash = Hash(BattlefieldBaseline.ArenaPath);
            string blockoutHash = Hash(ScenePath);
            string navigationMeta = File.ReadAllText(Folder + "/BlockoutNavMesh.asset.meta");
            int roots = scene.rootCount;
            bool creationRejected = false;
            try { Create(); }
            catch (InvalidOperationException exception) { creationRejected = exception.Message.StartsWith("The blockout already exists", StringComparison.Ordinal); }
            foreach (int invalid in new[] { -1, 3 })
            {
                bool rejected = false;
                try { BattlefieldBlockoutPlaytest.Start(invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Invalid blockout playtest route was accepted.");
            }
            BattlefieldBlockoutPlaytest.Stop();
            if (!creationRejected || originalHash != Hash(BattlefieldBaseline.ArenaPath) || blockoutHash != Hash(ScenePath) ||
                navigationMeta != File.ReadAllText(Folder + "/BlockoutNavMesh.asset.meta") || scene.rootCount != roots || scene.isDirty ||
                EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Breachpoint.BlockoutPlaytest", false))
                throw new InvalidOperationException("Authoring/playtest guards modified scene assets or editor ownership.");
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/guard-checks.txt", "PASS existing prototype cannot be replaced; invalid playtest routes are rejected; unowned Stop is a no-op.\n" +
                "Original scene, prototype bytes and navigation metadata preserved.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Validate Saved Prototype")]
        public static void Validate()
        {
            Scene scene = RequireBlockout();
            Directory.CreateDirectory(ReportFolder);
            var report = new StringBuilder("Blockout B scene validation | " + DateTime.UtcNow.ToString("O") + "\n");
            int checks = 0;
            void Check(bool passed, string description)
            {
                report.AppendLine((passed ? "PASS " : "FAIL ") + description);
                if (!passed)
                {
                    File.WriteAllText(ReportFolder + "/scene-validation.txt", report.ToString());
                    throw new InvalidOperationException(description);
                }
                checks++;
            }
            Physics.SyncTransforms();
            var actors = FindInScene<EnemyActor>(scene);
            var covers = FindInScene<EnemyCoverPoint>(scene);
            var surface = FindInScene<NavMeshSurface>(scene).Single();
            var player = FindInScene<PlayerInputReader>(scene).Single();
            Check(actors.Length == EnemyCount, "Exactly " + EnemyCount + " active Rifleman instances");
            Check(actors.Count(a => a.SquadId == MainSquad) == 4 && actors.Count(a => a.SquadId == SupportSquad) == 2,
                "Four main-squad and two supporting Rifleman");
            Check(FindInScene<GameLifetimeScope>(scene).Length == 1, "One game composition root");
            Check(Vector3.Distance(player.transform.position, Entry + Vector3.up * 1.09f) < .05f, "Player starts in entry pocket");
            Check(AssetDatabase.GetAssetPath(surface.navMeshData) == BattlefieldMarketRoom.ExpectedNavigationPath(Folder + "/BlockoutNavMesh.asset"), "Separate persisted NavMesh data for the accepted layout revision");
            Check(covers.Length == 14, "Fourteen authored cover anchors");
            var scope = new SerializedObject(FindInScene<GameLifetimeScope>(scene).Single());
            var references = scope.FindProperty("_coverPoints");
            Check(references.arraySize == covers.Length, "Cover registry size matches authored points");
            for (int i = 0; i < references.arraySize; i++)
                Check(covers.Contains(references.GetArrayElementAtIndex(i).objectReferenceValue as EnemyCoverPoint), "Cover registry entry " + i + " belongs to this scene");
            foreach (var actor in actors)
            {
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject) == RiflemanPath, actor.name + " retains the production prefab link");
                Check((actor.SquadId == MainSquad || actor.SquadId == SupportSquad) && actor.EncounterId == "Blockout B", actor.name + " has local squad/encounter identity");
                Check(actor.GetComponent<EnemyLifetimeScope>().Archetype.IsValid, actor.name + " keeps the existing valid archetype");
                Check(Path(Entry, actor.transform.position, out float length), actor.name + " reachable from entry, length=" + length.ToString("F1"));
                Check(actor.Route != null && actor.Route.Count == 3, actor.name + " has a local patrol route");
                for (int i = 0; i < actor.Route.Count; i++)
                    Check(actor.Route.TryGetPoint(i, out Vector3 point) && Path(Entry, point, out _), actor.name + " waypoint " + i + " reachable");
            }
            foreach (var point in covers)
            {
                Check(point.IsUsable, point.name + " configured");
                Check(Path(Entry, point.ProtectedPosition, out _), point.name + " protected position reachable");
                Check(Path(point.ProtectedPosition, point.ExposurePosition, out _), point.name + " exposure reachable");
                float height = point.Kind == EnemyCoverKind.High ? 1.7f : 1.05f;
                var hits = WorldHits(surface.transform, point.ProtectedPosition + Vector3.up * height, point.Normal, 20);
                Check(hits.Length > 0 && hits[0].collider == point.Protection, point.name + " has physical protection in its authored direction");
                var exposureHits = WorldHits(surface.transform, point.ExposurePosition + Vector3.up * 1.4f, point.Normal, 20);
                Check(!exposureHits.Any(hit => hit.collider == point.Protection), point.name + " standing muzzle clears its protection");
            }

            foreach (var route in ReviewRoutes())
            {
                float total = 0;
                for (int i = 1; i < route.Points.Length; i++)
                {
                    bool reachable = Path(route.Points[i - 1], route.Points[i], out float length);
                    Check(reachable, route.Name + " segment " + i + " has a complete path");
                    total += length;
                }
                report.AppendLine("ROUTE " + route.Name + " length=" + total.ToString("F1") + "m");
            }
            Check(!ClearWorldLine(surface.transform, Entry + Vector3.up * 1.7f, new Vector3(6, 1.7f, 40)), "Entry screen blocks the initial direct firing axis");
            Check(!ClearWorldLine(surface.transform, new Vector3(11, 1.7f, 41), new Vector3(-8.5f, 1.7f, 41)), "Energy module separates east and west firing lanes");
            Check(!ClearWorldLine(surface.transform, new Vector3(15.5f, 3.2f, 44), new Vector3(-8.5f, 1.7f, 41)), "Gallery cannot see through the module into west passage");
            Check(!ClearWorldLine(surface.transform, Exit + Vector3.up * 1.7f, new Vector3(7, 1.7f, 51)), "Exit pocket breaks the main direct firing axis");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "No missing scripts: " + transform.name);
            report.AppendLine("RESULT: PASS | " + checks + " scene checks");
            report.AppendLine(EnemyValidationRunner.ConsoleCounts());
            File.WriteAllText(ReportFolder + "/scene-validation.txt", report.ToString());
        }

        internal sealed class ReviewRoute
        {
            internal readonly string Name;
            internal readonly Vector3[] Points;
            internal ReviewRoute(string name, params Vector3[] points) { Name = name; Points = points; }
        }

        internal static ReviewRoute[] ReviewRoutes() => new[]
        {
            new ReviewRoute("East approach", Entry, new Vector3(11.5f, 0, 26), new Vector3(11.5f, 0, 33),
                new Vector3(11.5f, 0, 47), new Vector3(10, 0, 53), new Vector3(-4, 0, 54), Exit),
            new ReviewRoute("West flank", Entry, new Vector3(2.5f, 0, 24), new Vector3(2.5f, 0, 33),
                new Vector3(-8.5f, 0, 33), new Vector3(-8.5f, 0, 54), new Vector3(-4, 0, 55), Exit),
            new ReviewRoute("Gallery both ramps", Entry, new Vector3(11.5f, 0, 26), new Vector3(11.5f, 0, 31),
                new Vector3(15.5f, 0, 31), new Vector3(15.5f, 1.5f, 44), new Vector3(15.5f, 0, 57),
                new Vector3(12, 0, 60), Exit)
        };

        internal static bool Path(Vector3 start, Vector3 end, out float length)
        {
            length = 0;
            if (!NavMesh.SamplePosition(start, out NavMeshHit a, .2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(end, out NavMeshHit b, .2f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return true;
        }

        private static RaycastHit[] WorldHits(Transform architecture, Vector3 origin, Vector3 direction, float length) =>
            Physics.RaycastAll(origin, direction, length, ~0, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider.transform.IsChildOf(architecture)).OrderBy(hit => hit.distance).ToArray();

        private static bool ClearWorldLine(Transform architecture, Vector3 origin, Vector3 destination)
        {
            Vector3 delta = destination - origin;
            return WorldHits(architecture, origin, delta.normalized, delta.magnitude).Length == 0;
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Capture Views")]
        public static void Capture()
        {
            Scene scene = RequireBlockout();
            Directory.CreateDirectory(ReportFolder);
            ulong priorMask = EditorSceneManager.GetSceneCullingMask(scene);
            try
            {
                EditorSceneManager.SetSceneCullingMask(scene, 1UL << 60);
                BattlefieldBaseline.Capture(scene, "overview", new Vector3(0, 75, 42), Quaternion.Euler(90, 0, 0), true, ReportFolder, 25);
                BattlefieldBaseline.Capture(scene, "entry", Entry + Vector3.up * 1.7f, Quaternion.identity, false, ReportFolder);
                BattlefieldBaseline.Capture(scene, "east-approach", new Vector3(11, 1.7f, 33), Quaternion.identity, false, ReportFolder);
                BattlefieldBaseline.Capture(scene, "west-flank", new Vector3(-8.5f, 1.7f, 35), Quaternion.identity, false, ReportFolder);
                BattlefieldBaseline.Capture(scene, "gallery", new Vector3(15.5f, 3.2f, 44), Quaternion.Euler(2, -90, 0), false, ReportFolder);
            }
            finally { EditorSceneManager.SetSceneCullingMask(scene, priorMask); }
        }
    }
}
