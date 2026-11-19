using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Player.Input;
using Breachpoint.Gameplay.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VContainer;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class BattlefieldBaseline
    {
        private const string ObservationKey = "Breachpoint.BattlefieldBaseline";
        private const float ObservationSeconds = 60f;
        private static PlayerInputReader _player;
        private static Health _health;
        private static PlayerWeaponController _weapon;
        private static EnemyBrain[] _enemies;
        private static float _began;
        private static float _nextSample;
        private static float _nextHeartbeat;
        private static double _deadline;
        private static bool _emitted;
        private static bool _attached;
        private static int _enemyShots;
        private static int _playerShots;
        private static int _reloads;
        private static int _damageEvents;
        private static float _firstDamage;
        private static Vector3 _previousPosition;
        private static float _distance;
        private static StreamWriter _samples;
        private static string _failure;

        [Serializable] private sealed class SceneSetupBackup { public SceneSetup[] scenes; }

        [InitializeOnLoadMethod]
        private static void SubscribeObservation()
        {
            EditorApplication.playModeStateChanged += ObservationModeChanged;
            EditorApplication.update += ObservationTick;
        }

        [MenuItem("Breachpoint/Level Design/Baseline/Observe Entry (60 seconds)")]
        private static void ObserveEntry() => StartObservation(0, false);

        [MenuItem("Breachpoint/Level Design/Baseline/Observe West Lane (60 seconds)")]
        private static void ObserveWest() => StartObservation(1, false);

        [MenuItem("Breachpoint/Level Design/Baseline/Observe Overlook (60 seconds)")]
        private static void ObserveOverlook() => StartObservation(2, false);

        [MenuItem("Breachpoint/Level Design/Baseline/Record Normal Playthrough")]
        private static void RecordPlaythrough() => StartObservation(0, true);

        [MenuItem("Breachpoint/Level Design/Open Current Arena")]
        public static void OpenArena()
        {
            RequireIdleEditor();
            EditorSceneManager.OpenScene(ArenaPath);
        }

        [MenuItem("Breachpoint/Level Design/Baseline/Stop Owned Recording")]
        public static void StopObservation()
        {
            if (!SessionState.GetBool(ObservationKey, false)) return;
            EditorApplication.isPlaying = false;
        }

        public static void ValidateObservationGuards()
        {
            RequireIdleEditor();
            string before = JsonUtility.ToJson(new SceneSetupBackup { scenes = EditorSceneManager.GetSceneManagerSetup() });
            foreach (int invalid in new[] { -1, 3 })
            {
                bool rejected = false;
                try { StartObservation(invalid, false); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Invalid viewpoint was accepted.");
            }
            StopObservation();
            string after = JsonUtility.ToJson(new SceneSetupBackup { scenes = EditorSceneManager.GetSceneManagerSetup() });
            if (before != after || SessionState.GetBool(ObservationKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Baseline guards changed editor ownership or scene setup.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/guard-checks.txt", "PASS: negative/out-of-range viewpoints are rejected and unowned Stop is a no-op, without scene/session changes.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
        }

        public static void StartObservation(int viewpoint, bool manual)
        {
            if (viewpoint < 0 || viewpoint > 2) throw new ArgumentOutOfRangeException(nameof(viewpoint));
            RequireIdleEditor();
            if (SessionState.GetBool("EnemyTools.Validation", false) || SessionState.GetBool(ObservationKey, false))
                throw new InvalidOperationException("Stop the existing owned review first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => string.IsNullOrEmpty(s.path)))
                throw new InvalidOperationException("Open a saved scene before baseline observation; an untitled scene cannot be safely restored.");
            SessionState.SetString(ObservationKey + ".Scenes", JsonUtility.ToJson(new SceneSetupBackup { scenes = setup }));
            SessionState.SetInt(ObservationKey + ".Viewpoint", viewpoint);
            SessionState.SetBool(ObservationKey + ".Manual", manual);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/observation-status.txt", "STARTING " + Label(viewpoint) + "\n");
            EditorSceneManager.OpenScene(ArenaPath);
            SessionState.SetBool(ObservationKey, true);
            EditorApplication.isPlaying = true;
        }

        private static string Label(int viewpoint) => viewpoint == 0 ? "entry" : viewpoint == 1 ? "west-lane" : "overlook";
        private static string ObservationPath => Output + "/" +
            (SessionState.GetBool(ObservationKey + ".Manual", false) ? "manual" : "observation-" + Label(SessionState.GetInt(ObservationKey + ".Viewpoint", 0)));

        private static void ObservationModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ObservationKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _attached = false;
                _failure = null;
                _deadline = EditorApplication.timeSinceStartup + 180;
                SessionState.SetBool(ObservationKey + ".Background", Application.runInBackground);
                SessionState.SetFloat(ObservationKey + ".TimeScale", Time.timeScale);
                SessionState.SetInt(ObservationKey + ".CursorLock", (int)Cursor.lockState);
                SessionState.SetBool(ObservationKey + ".CursorVisible", Cursor.visible);
                Application.runInBackground = true;
                Time.timeScale = 1;
                // Isolate automatic probes from real mouse/keyboard input before the first frame.
                if (!SessionState.GetBool(ObservationKey + ".Manual", false))
                {
                    var input = Object.FindAnyObjectByType<PlayerInputReader>();
                    if (input != null) input.enabled = false;
                }
                Application.logMessageReceived += ObservationLog;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                try { FinishObservation(); }
                finally
                {
                    Application.logMessageReceived -= ObservationLog;
                    Application.runInBackground = SessionState.GetBool(ObservationKey + ".Background", false);
                    Time.timeScale = SessionState.GetFloat(ObservationKey + ".TimeScale", 1);
                    Cursor.lockState = (CursorLockMode)SessionState.GetInt(ObservationKey + ".CursorLock", 0);
                    Cursor.visible = SessionState.GetBool(ObservationKey + ".CursorVisible", true);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(ObservationKey, false);
                var saved = JsonUtility.FromJson<SceneSetupBackup>(SessionState.GetString(ObservationKey + ".Scenes", ""));
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                File.AppendAllText(Output + "/observation-status.txt", "Editor scene setup restored.\n" + EnemyValidationRunner.ConsoleCounts() + "\n");
            }
        }

        private static void ObservationTick()
        {
            if (!SessionState.GetBool(ObservationKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (!_attached) { if (Time.timeSinceLevelLoad > .5f) AttachObservation(); return; }
                if (!SessionState.GetBool(ObservationKey + ".Manual", false) && !_emitted && Time.time - _began >= 2)
                {
                    Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<EnemyWorld>().Emit(
                        new NoiseStimulus(_player.transform.position, 35f, Faction.Player, Time.time));
                    _emitted = true;
                }
                if (Time.time >= _nextSample) SampleObservation();
                if (Time.time >= _nextHeartbeat)
                {
                    _nextHeartbeat = Time.time + 10;
                    File.WriteAllText(Output + "/observation-status.txt", "RUNNING " + ObservationPath + " | seconds=" +
                        Number(Time.time - _began) + " | health=" + Number(_health.CurrentHealth) + " | enemy shots=" + _enemyShots + "\n");
                }
                bool manual = SessionState.GetBool(ObservationKey + ".Manual", false);
                if (_health.IsDead || !manual && Time.time - _began >= ObservationSeconds || !manual && EditorApplication.timeSinceStartup > _deadline)
                    EditorApplication.isPlaying = false;
            }
            catch (Exception exception)
            {
                _failure = exception.ToString();
                File.WriteAllText(Output + "/observation-status.txt", "FAILED " + _failure + "\n");
                EditorApplication.isPlaying = false;
            }
        }

        private static void AttachObservation()
        {
            _player = Object.FindAnyObjectByType<PlayerInputReader>();
            if (_player == null) throw new InvalidOperationException("Arena player is missing.");
            _health = _player.GetComponent<Health>();
            _weapon = _player.GetComponentInChildren<PlayerWeaponController>();
            _enemies = Object.FindObjectsByType<EnemyBrain>();
            if (_health == null || _weapon == null || _enemies.Length == 0 || _enemies.Any(e => e.Memory == null))
                throw new InvalidOperationException("Arena composition did not initialize.");
            _enemyShots = _playerShots = _damageEvents = 0;
            _reloads = 0;
            _firstDamage = -1;
            _distance = 0;
            _emitted = false;
            bool manual = SessionState.GetBool(ObservationKey + ".Manual", false);
            int viewpoint = SessionState.GetInt(ObservationKey + ".Viewpoint", 0);
            if (!manual)
            {
                _player.enabled = false;
                Vector3 destination = viewpoint == 0 ? _player.transform.position :
                    viewpoint == 1 ? new Vector3(-23, 0, -17) : new Vector3(-23, 1.5f, 20);
                if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Observation point is not on the existing NavMesh: " + destination);
                var controller = _player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                float rootHeight = controller != null ? controller.height * .5f - controller.center.y + controller.skinWidth + .01f : 1.09f;
                _player.transform.position = hit.position + Vector3.up * rootHeight;
                if (controller != null) controller.enabled = true;
                Physics.SyncTransforms();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            _began = Time.time;
            _nextSample = _nextHeartbeat = Time.time;
            _previousPosition = _player.transform.position;
            _health.DamageReceived += RecordDamage;
            _weapon.ShotFired += RecordPlayerShot;
            _weapon.ReloadStarted += RecordReload;
            foreach (var enemy in _enemies) enemy.Combat.Fired += RecordEnemyShot;
            _samples = new StreamWriter(ObservationPath + ".csv", false);
            _samples.WriteLine("seconds,playerHealth,playerX,playerY,playerZ,enemy,enemyX,enemyY,enemyZ,squad,encounter,state,visible,contact,noise,intent,cover,navigation,magazine,reloading");
            File.WriteAllText(ObservationPath + ".txt", "Started UTC " + DateTime.UtcNow.ToString("O") + "\n" +
                "Mode=" + (manual ? "normal manual playthrough" : "stationary exposure probe; one 35m noise stimulus at +2s; no player firing") + "\n" +
                "Start=" + _player.transform.position + " | Health=" + _health.CurrentHealth + "/" + _health.MaximumHealth +
                " | Magazine=" + _weapon.Magazine + "/" + _weapon.MagazineSize + "\n");
            _attached = true;
        }

        private static string Number(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
        private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        private static void SampleObservation()
        {
            _nextSample = Time.time + .5f;
            _distance += Vector3.Distance(_previousPosition, _player.transform.position);
            _previousPosition = _player.transform.position;
            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                Vector3 p = _player.transform.position;
                Vector3 e = enemy.transform.position;
                var actor = enemy.GetComponent<EnemyActor>();
                _samples.WriteLine(string.Join(",", Number(Time.time - _began), Number(_health.CurrentHealth), Number(p.x), Number(p.y), Number(p.z),
                    Csv(enemy.name), Number(e.x), Number(e.y), Number(e.z), Csv(actor.SquadId), Csv(actor.EncounterId), Csv(enemy.States.Current.ToString()),
                    enemy.Memory.Visible, enemy.Memory.HasContact, enemy.Memory.HasNoise, Csv(enemy.Tactics.Current.Intent.ToString()),
                    Csv(enemy.Tactics.CurrentCover != null ? enemy.Tactics.CurrentCover.name : ""), Csv(actor.Navigation.Result.ToString()),
                    _weapon.Magazine, _weapon.IsReloading));
            }
            _samples.Flush();
        }

        private static void RecordDamage(DamageInfo damage)
        {
            _damageEvents++;
            if (_firstDamage < 0) _firstDamage = Time.time - _began;
            File.AppendAllText(ObservationPath + ".txt", "DAMAGE t=" + Number(Time.time - _began) + " amount=" + Number(damage.Amount) +
                " source=" + (damage.Source != null ? damage.Source.name : "unknown") + "\n");
        }

        private static void RecordPlayerShot(WeaponShotResult shot) => _playerShots++;
        private static void RecordReload(float duration)
        {
            _reloads++;
            File.AppendAllText(ObservationPath + ".txt", "RELOAD t=" + Number(Time.time - _began) + " duration=" + Number(duration) + "\n");
        }
        private static void RecordEnemyShot() => _enemyShots++;
        private static void ObservationLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _failure = (_failure ?? "") + type + ": " + message + "\n";
        }

        private static void FinishObservation()
        {
            try
            {
                if (_attached)
                {
                    SampleObservation();
                    File.AppendAllText(ObservationPath + ".txt", "END seconds=" + Number(Time.time - _began) + " | health=" + Number(_health.CurrentHealth) +
                        " | dead=" + _health.IsDead + " | firstDamage=" + Number(_firstDamage) + " | damageEvents=" + _damageEvents +
                        " | enemyShots=" + _enemyShots + " | playerShots=" + _playerShots + " | reloads=" + _reloads + " | travel=" + Number(_distance) + "\n");
                }
            }
            finally
            {
                if (_health != null) _health.DamageReceived -= RecordDamage;
                if (_weapon != null)
                {
                    _weapon.ShotFired -= RecordPlayerShot;
                    _weapon.ReloadStarted -= RecordReload;
                }
                if (_enemies != null)
                    foreach (var enemy in _enemies) if (enemy != null && enemy.Combat != null) enemy.Combat.Fired -= RecordEnemyShot;
                _samples?.Dispose();
                _samples = null;
                _attached = false;
            }
            File.WriteAllText(Output + "/observation-status.txt", (_failure == null ? "OBSERVATION COMPLETE" : "FAILED " + _failure) +
                "\nThis is evidence collection, not a combat-feel acceptance verdict.\n");
        }
    }
}
