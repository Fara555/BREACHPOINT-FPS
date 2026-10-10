using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Breachpoint.Composition;
using VContainer;

namespace Breachpoint.Editor.Enemies
{
    [InitializeOnLoad]
    public static class TacticalArenaPlaytest
    {
        private const string Key = "Breachpoint.ArenaPlaytest";
        private static readonly StringBuilder Report = new StringBuilder();
        private static readonly HashSet<string> Behaviors = new HashSet<string>();
        private static readonly HashSet<string> Combatants = new HashSet<string>();
        private static readonly HashSet<string> CoverUsers = new HashSet<string>();
        private static readonly HashSet<string> Participants = new HashSet<string>();
        private static readonly HashSet<string> ActiveSquads = new HashSet<string>();
        private static readonly List<string> Errors = new List<string>();
        private static EnemyBrain[] _enemies;
        private static Transform _player;
        private static float _started;
        private static float _nextSample;
        private static int _sector;
        private static int _shots;
        private static bool _background;
        private static EnemyWorld _world;
        private static readonly Vector3[] Positions = { new Vector3(0,1,-28), new Vector3(-22,1,-8), new Vector3(22,1,7), new Vector3(-8,1,25) };

        static TacticalArenaPlaytest() => EditorApplication.playModeStateChanged += ModeChanged;

        [MenuItem("Breachpoint/Level Design/Playtest Enemy Arena")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Project/Scenes/EnemyArena/EnemyArena.unity")
                throw new InvalidOperationException("Save and open EnemyArena in Edit Mode before playtesting.");
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Arena save failed.");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Report.Clear(); Behaviors.Clear(); Combatants.Clear(); CoverUsers.Clear(); Participants.Clear(); ActiveSquads.Clear(); Errors.Clear();
                _enemies = UnityEngine.Object.FindObjectsByType<EnemyBrain>();
                _player = SceneManager.GetActiveScene().GetRootGameObjects().Single(root => root.name == "Player").transform;
                _world = UnityEngine.Object.FindObjectsByType<GameLifetimeScope>().Single().Container.Resolve<EnemyWorld>();
                // This health override exists only in the test's Play Mode instance.
                _player.GetComponent<Health>().Configure(100000, false);
                _background = Application.runInBackground;
                Application.runInBackground = true;
                _started = Time.time; _nextSample = _started + 1; _sector = 0; _shots = 0;
                Application.logMessageReceived += Log;
                EditorApplication.update += Tick;
                Report.AppendLine("60-second arena test, real AI and player target in four sectors, simulated weapon noise at sector entry; test-only player health override.");
                foreach (EnemyBrain enemy in _enemies) enemy.Combat.Fired += Shot;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= Log;
                foreach (EnemyBrain enemy in _enemies ?? Array.Empty<EnemyBrain>()) if (enemy != null) enemy.Combat.Fired -= Shot;
                Application.runInBackground = _background;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                File.AppendAllText(EnemyTools.Evidence + "/arena-playtest.txt", "Returned to Edit Mode; scene runtime changes discarded.\n" + EnemyValidationRunner.ConsoleCounts());
            }
        }

        private static void Shot() => _shots++;
        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Warning) Errors.Add(type + ": " + message);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.time < _nextSample) return;
            _nextSample = Time.time + 1;
            float elapsed = Time.time - _started;
            int sector = Mathf.Min(3, (int)(elapsed / 15));
            if (sector != _sector)
            {
                CharacterController controller = _player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                _player.position = Positions[sector];
                Rigidbody body = _player.GetComponent<Rigidbody>();
                if (body != null) { body.position = Positions[sector]; body.linearVelocity = Vector3.zero; }
                if (controller != null) controller.enabled = true;
                Physics.SyncTransforms();
                _world.Emit(new NoiseStimulus(_player.position, 35, Faction.Player, Time.time));
                _sector = sector;
            }
            foreach (EnemyBrain enemy in _enemies)
            {
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (!agent.isOnNavMesh) Errors.Add(enemy.name + " left NavMesh.");
                Behaviors.Add(enemy.States.Current + "/" + enemy.Tactics.Current.Intent);
                if (enemy.States.Current == EnemyStateId.Combat) Combatants.Add(enemy.name);
                if (enemy.States.Current == EnemyStateId.Combat || enemy.States.Current == EnemyStateId.Chase)
                {
                    Participants.Add(enemy.name);
                    ActiveSquads.Add(enemy.GetComponent<EnemyActor>().SquadId);
                }
                if (enemy.Tactics.CurrentCover != null) CoverUsers.Add(enemy.name);
            }
            Report.AppendLine($"t={elapsed:F1}s sector={sector} player={_player.position} combat={Combatants.Count} coverUsers={CoverUsers.Count} shots={_shots}");
            if (elapsed < 60) return;
            Report.AppendLine("Observed: " + string.Join(", ", Behaviors.OrderBy(item => item)));
            Report.AppendLine("Warnings/errors: " + Errors.Count);
            foreach (string error in Errors.Distinct()) Report.AppendLine(error);
            Report.AppendLine($"Engaged actors={Participants.Count}; engaged squads={ActiveSquads.Count}; firing-state actors={Combatants.Count}; cover users={CoverUsers.Count}; shots={_shots}");
            // Chase also executes tactical movement/cover. Shooter leases deliberately keep other members moving.
            bool passed = _enemies.Length == 12 && Errors.Count == 0 && Participants.Count >= 6 && ActiveSquads.Count == 3 && Combatants.Count >= 3 && CoverUsers.Count >= 2 && _shots > 0;
            Report.AppendLine("RESULT: " + (passed ? "PASS" : "FAIL"));
            File.WriteAllText(EnemyTools.Evidence + "/arena-playtest.txt", Report.ToString());
            EditorApplication.isPlaying = false;
        }
    }
}
