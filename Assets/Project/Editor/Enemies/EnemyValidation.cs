using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    [InitializeOnLoad]
    public static class EnemyValidation
    {
        private const string RunningKey = "Breachpoint.EnemyValidation.Running";
        private const string BatchKey = "Breachpoint.EnemyValidation.Batch";
        private static IEnumerator _routine;
        private static readonly List<string> Results = new List<string>();
        private static double _deadline;
        private static bool _failed;
        private static string ReportPath => Path.GetFullPath("Logs/EnemyValidation.txt");
        static EnemyValidation()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            if (SessionState.GetBool(RunningKey, false) && EditorApplication.isPlaying)
                EditorApplication.delayCall += StartRuntime;
        }
        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            Run();
        }
        [MenuItem("Breachpoint/Enemies/Run validation")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before validation.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs"); File.WriteAllText(ReportPath, "Enemy validation: " + DateTime.UtcNow.ToString("O") + "\n");
            Results.Clear(); _failed = false;
            try
            {
                StateMachineTests();
                EnemyAssetBuilder.CreateAssets();
                ValidatePrefabs();
                EditorSceneManager.OpenScene(EnemyAssetBuilder.ScenePath);
                SessionState.SetBool(RunningKey, true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); Finish(); }
        }
        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false)) StartRuntime();
            if (mode == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(BatchKey, false))
            {
                SessionState.SetBool(BatchKey, false);
                if (!File.ReadAllText(ReportPath).Contains("FAIL"))
                {
                    try
                    {
                        typeof(EnemyValidation).Assembly.GetType("Breachpoint.Editor.Enemies.EnemyGameplaySetup")?
                            .GetMethod("AddPlayerToArena")?.Invoke(null, null);
                    }
                    catch (Exception exception) { Fail(exception); }
                }
                EditorApplication.Exit(File.ReadAllText(ReportPath).Contains("FAIL") ? 1 : 0);
            }
        }
        private static void StartRuntime()
        {
            if (_routine != null) return;
            Application.logMessageReceived += Log;
            _routine = RuntimeTests(); _deadline = EditorApplication.timeSinceStartup + 100;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Runtime test deadline exceeded.");
                if (!_routine.MoveNext()) Finish();
            }
            catch (Exception exception) { Fail(exception); Finish(); }
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { _failed = true; File.AppendAllText(ReportPath, "FAIL Console: " + message + "\n" + stack + "\n"); }
        }
        private static void Finish()
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; _routine = null;
            SessionState.SetBool(RunningKey, false);
            File.AppendAllText(ReportPath, _failed ? "RESULT: FAIL\n" : "RESULT: PASS\n");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else if (SessionState.GetBool(BatchKey, false)) { SessionState.SetBool(BatchKey, false); EditorApplication.Exit(_failed ? 1 : 0); }
        }
        private static void Fail(Exception exception) { _failed = true; File.AppendAllText(ReportPath, "FAIL " + exception + "\n"); Debug.LogException(exception); }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            Results.Add(description); File.AppendAllText(ReportPath, "PASS " + description + "\n");
        }
        private sealed class Probe : IEnemyState
        {
            public EnemyStateId Id { get; }
            public int Enters, Exits, Ticks;
            public Action OnEnter;
            public Probe(EnemyStateId id) => Id = id;
            public void Enter() { Enters++; OnEnter?.Invoke(); }
            public void Tick(float dt) => Ticks++;
            public void Exit() => Exits++;
        }
        private static void StateMachineTests()
        {
            var machine = new EnemyStateMachine(); var idle = new Probe(EnemyStateId.Idle);
            var combat = new Probe(EnemyStateId.Combat); var dead = new Probe(EnemyStateId.Dead);
            machine.Register(idle); machine.Register(combat); machine.Register(dead);
            machine.Change(idle.Id, "start"); machine.Change(idle.Id, "duplicate"); machine.Tick(1f);
            Check(idle.Enters == 1 && idle.Ticks == 1 && idle.Exits == 0, "State enters once; duplicate transitions ignored");
            combat.OnEnter = () => machine.Change(idle.Id, "reentrant");
            machine.Change(combat.Id, "threat");
            Check(machine.Current == EnemyStateId.Combat && idle.Exits == 1 && combat.Enters == 1, "Reentrant transition rejected; balanced Exit/Enter");
            machine.Change(dead.Id, "death"); machine.Change(combat.Id, "invalid resurrection");
            Check(machine.Current == EnemyStateId.Dead && dead.Enters == 1 && combat.Exits == 1, "Death is terminal and entered once");
            machine.Reset(); machine.Change(idle.Id, "spawn");
            Check(machine.Current == EnemyStateId.Idle && dead.Exits == 1, "Explicit full reset permits respawn");
            combat.OnEnter = () => machine.Change(EnemyStateId.Dead, "death callback");
            machine.Change(EnemyStateId.Combat, "reentrant death test");
            Check(machine.Current == EnemyStateId.Dead, "Death requested inside a transition retains highest priority");
            Check(Factions.AreHostile(Faction.Hostile, Faction.Player) && !Factions.AreHostile(Faction.Friendly, Faction.Player) &&
                !Factions.AreHostile(Faction.Hostile, Faction.Hostile) && !Factions.AreHostile(Faction.Neutral, Faction.Hostile), "Faction rules prevent friendly fire");
            var memory = new EnemyBlackboard { HasContact = true, HasNoise = true, Alert = 1, PatrolIndex = 8, StunnedUntil = 99 };
            memory.Reset(); Check(!memory.HasContact && !memory.HasNoise && memory.Alert == 0 && memory.PatrolIndex == 0 && memory.StunnedUntil == 0,
                "Blackboard reset clears contact, noise, timers and patrol progress");
        }
        private static void ValidatePrefabs()
        {
            foreach (string name in new[] { "Rifleman", "Melee" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.Root + "/Prefabs/" + name + ".prefab");
                Check(prefab != null && prefab.GetComponent<EnemyLifetimeScope>().Archetype.IsValid && prefab.GetComponent<IEnemyWeapon>() != null &&
                    prefab.GetComponent<EnemyActor>().Eyes != null && prefab.GetComponent<EnemyActor>().Muzzle != null, name + " prefab wiring and config references");
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0) throw new InvalidOperationException("Missing script: " + name);
            }
        }
        private static IEnumerator Wait(float seconds)
        { float end = Time.time + seconds; while (Time.time < end) yield return null; }
        private static IEnumerator RuntimeTests()
        {
            // Every fixture lives only in Play Mode; authored assets are never modified by tests.
            typeof(EnemyValidation).Assembly.GetType("Breachpoint.Editor.Enemies.EnemyGameplaySetup")?
                .GetMethod("DisableArenaPlayer")?.Invoke(null, null);
            foreach (EnemyBrain existing in Object.FindObjectsByType<EnemyBrain>()) existing.gameObject.SetActive(false);
            GameLifetimeScope root = Object.FindAnyObjectByType<GameLifetimeScope>();
            EnemyWorld world = root.Container.Resolve<EnemyWorld>();
            GameObject player = new GameObject("Validation target"); player.layer = 6; player.transform.position = new Vector3(0, 0, -8);
            var collider = player.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2f; collider.radius = 0.45f;
            var health = player.AddComponent<Health>(); health.Configure(10000f, false);
            var target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            player.SetActive(false);
            EnemyBrain brain = Spawn(root, "Rifleman", new Vector3(0, 0, -14));
            var actor = brain.GetComponent<EnemyActor>();
            var routeObject = new GameObject("Validation patrol"); var route = routeObject.AddComponent<PatrolRoute>();
            var routeData = new SerializedObject(route); var routePoints = routeData.FindProperty("_points"); routePoints.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var patrolPoint = new GameObject("Patrol point " + i); patrolPoint.transform.SetParent(routeObject.transform);
                patrolPoint.transform.position = new Vector3(i == 0 ? 3 : 0, 0, -14);
                routePoints.GetArrayElementAtIndex(i).objectReferenceValue = patrolPoint.transform;
            }
            routeData.ApplyModifiedPropertiesWithoutUndo(); actor.Route = route;
            IEnumerator wait = Wait(2f); while (wait.MoveNext()) yield return null;
            Check(brain.States.Current == EnemyStateId.Patrol && brain.transform.position.x > 1f, "Patrol follows authored route on baked NavMesh");
            brain.gameObject.SetActive(false); actor.Route = null;
            brain.transform.SetPositionAndRotation(new Vector3(0, 0, -14), Quaternion.identity);
            player.transform.position = new Vector3(0, 0, -16); player.SetActive(true); brain.gameObject.SetActive(true);
            wait = Wait(0.4f); while (wait.MoveNext()) yield return null;
            Check(!brain.Memory.Visible && brain.States.Current == EnemyStateId.Idle, "Target behind enemy is outside the field of view");
            player.transform.position = new Vector3(0, 0, -8); Physics.SyncTransforms();
            wait = Wait(2f); while (wait.MoveNext()) yield return null;
            Check(brain.States.Current == EnemyStateId.Combat && brain.Memory.Visible, "FOV and line of sight detect player and enter Combat");
            Check(health.CurrentHealth < health.MaximumHealth && brain.Combat.Ammo < brain.Config.Combat.MagazineSize, "Rifle damages player and consumes ammunition");
            var weapon = brain.GetComponent<IEnemyWeapon>();
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Validation wall";
            wall.transform.position = new Vector3(0, 1.5f, -11); wall.transform.localScale = new Vector3(4, 3, 0.5f); Physics.SyncTransforms();
            Check(!weapon.CanAttack(target) && !weapon.Attack(target, 1f), "Wall blocks line of fire and damage");
            wait = Wait(0.3f); while (wait.MoveNext()) yield return null;
            Check(!brain.Memory.Visible && brain.Memory.HasContact && brain.States.Current == EnemyStateId.Chase, "Occlusion preserves memory and switches Combat to Chase");
            wall.SetActive(false);
            brain.Stun(20f); Physics.SyncTransforms();
            GameObject friendly = new GameObject("Friendly blocker"); friendly.layer = 8;
            friendly.transform.position = Vector3.Lerp(actor.Muzzle.position, target.AimPosition, 0.5f);
            friendly.AddComponent<BoxCollider>().size = Vector3.one * 1.5f;
            friendly.AddComponent<Health>(); var friendlyTarget = friendly.AddComponent<PerceptionTarget>(); friendlyTarget.Initialize(world, Faction.Hostile);
            Physics.SyncTransforms();
            Check(!weapon.CanAttack(target), "Ally between muzzle and target blocks firing"); friendly.SetActive(false);
            actor.Health.TakeDamage(new DamageInfo(10000, actor.transform.position, Vector3.forward, player));
            Check(brain.States.Current == EnemyStateId.Dead, "Damage during stun immediately enters Dead");
            Vector3 deadPosition = brain.transform.position; float playerHealth = health.CurrentHealth;
            wait = Wait(0.5f); while (wait.MoveNext()) yield return null;
            Check(brain.transform.position == deadPosition && health.CurrentHealth == playerHealth && !brain.Combat.IsReloading, "Dead enemy stops navigation, attack and reload");
            target.gameObject.SetActive(false); brain.gameObject.SetActive(false); brain.transform.position = new Vector3(0, 0, -14); brain.gameObject.SetActive(true);
            Check(!actor.Health.IsDead && actor.Health.CurrentHealth == actor.Health.MaximumHealth && brain.Memory.Target == null &&
                brain.Combat.Ammo == brain.Config.Combat.MagazineSize && brain.Memory.StunnedUntil == 0 && world.Targets.Count == 1,
                "Pooled reuse resets health, target, ammo, stun and target registration");
            world.Emit(new NoiseStimulus(new Vector3(2, 0, -12), 20f, Faction.Player, Time.time));
            wait = Wait(0.3f); while (wait.MoveNext()) yield return null;
            Check(brain.States.Current == EnemyStateId.Investigate, "Sound outside vision triggers Investigate");
            brain.Memory.HasNoise = false; brain.Memory.HasContact = true; brain.Memory.LastKnownPosition = new Vector3(0, 0, -12);
            brain.Memory.LastSeenTime = Time.time - brain.Config.Perception.MemoryDuration - 1f;
            wait = Wait(0.2f); while (wait.MoveNext()) yield return null;
            Check(brain.States.Current == EnemyStateId.Search, "Expired target memory starts Search");
            wait = Wait(brain.Config.Decision.SearchDuration + 0.3f); while (wait.MoveNext()) yield return null;
            Check(brain.States.Current == EnemyStateId.Idle && !brain.Memory.HasContact, "Search expires and returns to passive behavior");
            actor.Navigation.MoveTo(new Vector3(500, 0, 500), true, Time.time);
            Check(actor.Navigation.Failed, "Off-mesh destination fails safely");
            brain.gameObject.SetActive(false);
            player.transform.position = new Vector3(8, 0, -8); player.SetActive(true); health.ResetHealth();
            EnemyBrain melee = Spawn(root, "Melee", new Vector3(8, 0, -14));
            wait = Wait(4f); while (wait.MoveNext()) yield return null;
            Check(melee.transform.position.z > -12 && health.CurrentHealth < health.MaximumHealth,
                $"Melee archetype chases and damages player using the same brain (position={melee.transform.position}, state={melee.States.Current}, health={health.CurrentHealth}, nav={melee.GetComponent<EnemyNavigation>().Result})");
            Check(melee.Combat.Ammo == melee.Config.Combat.MagazineSize, "Melee capability does not consume rifle ammunition");
            melee.gameObject.SetActive(false);
            player.transform.position = new Vector3(0, 0, -8); health.ResetHealth();
            brain.transform.SetPositionAndRotation(new Vector3(0, 0, -14), Quaternion.identity); brain.gameObject.SetActive(true);
            bool sawReload = false, refilled = false; float deadline = Time.time + 18f;
            while (Time.time < deadline)
            {
                sawReload |= brain.Combat.IsReloading;
                if (sawReload && !brain.Combat.IsReloading && brain.Combat.Ammo > 0) { refilled = true; break; }
                yield return null;
            }
            Check(refilled, "Reload completes on gameplay timer without animation events");
            // An isolated runtime stress smoke checks lifecycle correctness; this is not a hardware performance budget.
            brain.gameObject.SetActive(false); player.SetActive(false);
            var crowd = new List<EnemyBrain>();
            for (int i = 0; i < 30; i++) crowd.Add(Spawn(root, i % 2 == 0 ? "Rifleman" : "Melee", new Vector3(-14 + (i % 10) * 3, 0, -14 + (i / 10) * 3)));
            wait = Wait(1f); while (wait.MoveNext()) yield return null;
            foreach (EnemyBrain enemy in crowd) enemy.GetComponent<Health>().TakeDamage(new DamageInfo(10000, enemy.transform.position, Vector3.up, null));
            Check(crowd.TrueForAll(enemy => enemy.States.Current == EnemyStateId.Dead), "30-agent simultaneous death smoke");
            foreach (EnemyBrain enemy in crowd) { enemy.gameObject.SetActive(false); enemy.gameObject.SetActive(true); }
            wait = Wait(0.3f); while (wait.MoveNext()) yield return null;
            Check(crowd.TrueForAll(enemy => enemy.States.Current != EnemyStateId.Dead && !enemy.GetComponent<Health>().IsDead), "30-agent pooled reset smoke");
            foreach (EnemyBrain enemy in crowd) enemy.gameObject.SetActive(false);
            var spawnerObject = new GameObject("Validation spawner"); var spawner = spawnerObject.AddComponent<EnemySpawner>();
            var point = new GameObject("Validation spawn point"); point.transform.position = new Vector3(0, 0, -14);
            var so = new SerializedObject(spawner);
            so.FindProperty("_parentScope").objectReferenceValue = root;
            var prefabs = so.FindProperty("_prefabs"); prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.Root + "/Prefabs/Rifleman.prefab").GetComponent<EnemyLifetimeScope>();
            var points = so.FindProperty("_spawnPoints"); points.arraySize = 1; points.GetArrayElementAtIndex(0).objectReferenceValue = point.transform;
            so.FindProperty("_maximumAlive").intValue = 1; so.FindProperty("_totalToSpawn").intValue = 3;
            so.FindProperty("_spawnInterval").floatValue = 0.1f; so.FindProperty("_corpseDuration").floatValue = 0.1f; so.ApplyModifiedPropertiesWithoutUndo();
            wait = Wait(0.3f); while (wait.MoveNext()) yield return null;
            Check(spawner.SpawnedCount == 1 && spawner.AliveCount == 1, "Spawner obeys active enemy limit");
            EnemyBrain pooled = spawnerObject.GetComponentInChildren<EnemyBrain>();
            for (int i = 0; i < 2; i++)
            {
                pooled = spawnerObject.GetComponentInChildren<EnemyBrain>();
                pooled.GetComponent<Health>().TakeDamage(new DamageInfo(10000, pooled.transform.position, Vector3.up, null));
                wait = Wait(0.5f); while (wait.MoveNext()) yield return null;
            }
            pooled = spawnerObject.GetComponentInChildren<EnemyBrain>();
            Check(spawner.SpawnedCount == 3 && spawnerObject.GetComponentsInChildren<EnemyBrain>(true).Length <= 2 && pooled != null && !pooled.GetComponent<Health>().IsDead,
                "Spawner reuses bounded pool across three spawns while retaining corpses");
            spawnerObject.SetActive(false);
            // Exercise the actual player prefab and existing HitscanWeapon when running the full project copy.
            var integration = typeof(EnemyValidation).Assembly.GetType("Breachpoint.Editor.Enemies.EnemyGameplaySetup")?.GetMethod("ValidatePlayerCombat");
            if (integration != null)
            {
                brain.transform.SetPositionAndRotation(new Vector3(0, 0, -14), Quaternion.identity); brain.gameObject.SetActive(true); brain.Stun(10f);
                IEnumerator playerTest = (IEnumerator)integration.Invoke(null, new object[] { root, brain });
                while (playerTest.MoveNext()) yield return null;
                Check(true, "Original Player prefab, HitscanWeapon damage and gunshot hearing integration");
                brain.gameObject.SetActive(false);
            }
            if (_failed) throw new InvalidOperationException("Runtime Console contained errors; see report.");
        }
        private static EnemyBrain Spawn(LifetimeScope parent, string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAssetBuilder.Root + "/Prefabs/" + name + ".prefab");
            GameObject instance;
            using (LifetimeScope.EnqueueParent(parent)) instance = Object.Instantiate(prefab, position, Quaternion.identity);
            return instance.GetComponent<EnemyBrain>();
        }
    }
}
