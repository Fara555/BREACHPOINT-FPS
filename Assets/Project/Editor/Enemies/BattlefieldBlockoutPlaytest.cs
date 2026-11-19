using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Player.Composition;
using Breachpoint.Gameplay.Player.Input;
using Breachpoint.Gameplay.Player.Look;
using Breachpoint.Gameplay.Player.Movement;
using Breachpoint.Gameplay.Player.Movement.Jump;
using Breachpoint.Gameplay.Player.Movement.Slide;
using Breachpoint.Gameplay.Player.Movement.Stance;
using Breachpoint.Gameplay.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    // Test-only input drives the real player controller, weapon and physics in the saved prototype.
    internal static class BattlefieldBlockoutPlaytest
    {
        private const string Key = "Breachpoint.BlockoutPlaytest";
        private const float MaximumSeconds = 100;
        private sealed class TestInput : IPlayerInput
        {
            public Vector2 Move { get; set; }
            public Vector2 Look { get; set; }
            public bool IsFireHeld { get; set; }
            public bool IsAimHeld { get; set; }
            public bool WasReloadPressed { get; set; }
            public bool IsSprintHeld => false;
            public bool IsCrouchHeld => false;
            public bool IsJumpHeld => false;
            public bool WasJumpPressed => false;
            public bool WasJumpReleased => false;
        }

        private static TestInput _input;
        private static PlayerInputReader _reader;
        private static PlayerWeaponController _weapon;
        private static Health _health;
        private static PerceptionTarget _playerTarget;
        private static Transform _orientation;
        private static Camera _camera;
        private static float _sensitivity;
        private static EnemyActor[] _actors;
        private static BattlefieldBlockout.ReviewRoute _route;
        private static Vector3[] _corners;
        private static int _waypoint;
        private static int _corner;
        private static int _frame;
        private static int _shots;
        private static int _enemyShots;
        private static int _reloads;
        private static float _began;
        private static float _sampleAt;
        private static double _deadline;
        private static bool _started;
        private static bool _reloadRequested;
        private static bool _captured;
        private static string _result;
        private static int _runtimeIssues;
        private static readonly RaycastHit[] AimHits = new RaycastHit[64];

        private static string Report => BattlefieldBlockout.ReportFolder + "/playtest-" + SessionState.GetInt(Key + ".Route", 0);

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Tick;
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Playtest/East Route")]
        private static void East() => Start(0);
        [MenuItem("Breachpoint/Level Design/Blockout B/Playtest/West Route")]
        private static void West() => Start(1);
        [MenuItem("Breachpoint/Level Design/Blockout B/Playtest/Gallery Route")]
        private static void Gallery() => Start(2);

        internal static void Start(int route)
        {
            if (route < 0 || route >= BattlefieldBlockout.ReviewRoutes().Length) throw new ArgumentOutOfRangeException(nameof(route));
            BattlefieldBaseline.RequireIdleEditor();
            BattlefieldBlockout.RequireBlockout();
            if (SessionState.GetBool("EnemyTools.Validation", false) || SessionState.GetBool("Breachpoint.BattlefieldBaseline", false))
                throw new InvalidOperationException("Stop the other owned review before the blockout playtest.");
            Directory.CreateDirectory(BattlefieldBlockout.ReportFolder);
            SessionState.SetInt(Key + ".Route", route);
            SessionState.SetBool(Key + ".Background", Application.runInBackground);
            SessionState.SetFloat(Key + ".TimeScale", Time.timeScale);
            File.WriteAllText(Report + ".txt", "STARTING " + DateTime.UtcNow.ToString("O") + "\n");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Breachpoint/Level Design/Blockout B/Playtest/Stop Owned Test")]
        internal static void Stop()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.isPlaying = false;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _started = false;
                _result = null;
                _runtimeIssues = 0;
                _deadline = EditorApplication.timeSinceStartup + 180;
                Application.runInBackground = true;
                Time.timeScale = 1;
                _reader = Object.FindAnyObjectByType<PlayerInputReader>();
                if (_reader != null) _reader.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Application.logMessageReceived += Log;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Application.logMessageReceived -= Log;
                if (_weapon != null) { _weapon.ShotFired -= Shot; _weapon.ReloadStarted -= Reload; }
                if (_actors != null)
                    foreach (var actor in _actors) if (actor != null && actor.GetComponent<EnemyBrain>().Combat != null)
                        actor.GetComponent<EnemyBrain>().Combat.Fired -= EnemyShot;
                if (_started)
                {
                    int killed = _actors.Count(a => a == null || a.Health.IsDead);
                    File.AppendAllText(Report + ".txt", "END seconds=" + N(Time.time - _began) + " health=" + N(_health.CurrentHealth) +
                        " shots=" + _shots + " reloads=" + _reloads + " enemiesKilled=" + killed + "/" + _actors.Length + " enemyShots=" + _enemyShots +
                        " waypoint=" + _waypoint + "/" + _route.Points.Length + " position=" + _reader.transform.position + "\n");
                }
                File.AppendAllText(Report + ".txt", "RESULT: " + (_result ?? "STOPPED by user") + " | runtimeIssues=" + _runtimeIssues + "\n" +
                    EnemyValidationRunner.ConsoleCounts() + "\n" + EnemyValidationRunner.ConsoleDetails());
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
                Time.timeScale = SessionState.GetFloat(Key + ".TimeScale", 1);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                _started = false;
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
                File.AppendAllText(Report + ".txt", "Play Mode ended; original blockout restored by Unity.\n");
            }
        }

        private static void Initialize()
        {
            var scope = Object.FindAnyObjectByType<PlayerLifetimeScope>();
            var movement = _reader.GetComponent<PlayerMovement>();
            var look = _reader.GetComponent<PlayerLook>();
            _weapon = _reader.GetComponentInChildren<PlayerWeaponController>();
            _health = _reader.GetComponent<Health>();
            _playerTarget = _reader.GetComponent<PerceptionTarget>();
            _actors = Object.FindObjectsByType<EnemyActor>();
            if (scope == null || movement == null || look == null || _weapon == null || _actors.Length != BattlefieldBlockout.EnemyCount || _health.CurrentHealth != 100)
                throw new InvalidOperationException("The saved blockout did not initialize its normal player and expected enemies.");
            var movementData = new SerializedObject(movement);
            _orientation = (Transform)movementData.FindProperty("_orientation").objectReferenceValue;
            var lookData = new SerializedObject(look);
            _sensitivity = ((PlayerLookConfig)lookData.FindProperty("_config").objectReferenceValue).MouseSensitivity;
            var weaponData = new SerializedObject(_reader.GetComponentInChildren<HitscanWeapon>());
            _camera = (Camera)weaponData.FindProperty("_aimCamera").objectReferenceValue;
            _input = new TestInput();
            movement.Construct(_input, scope.Container.Resolve<PlayerMovementConfig>(), scope.Container.Resolve<PlayerJumpController>(),
                scope.Container.Resolve<PlayerSlideController>(), scope.Container.Resolve<PlayerStanceController>());
            look.Construct(_input);
            _weapon.Construct(_input, _weapon.CurrentConfig);
            _weapon.ShotFired += Shot;
            _weapon.ReloadStarted += Reload;
            foreach (var actor in _actors) actor.GetComponent<EnemyBrain>().Combat.Fired += EnemyShot;
            _route = BattlefieldBlockout.ReviewRoutes()[SessionState.GetInt(Key + ".Route", 0)];
            _waypoint = 1;
            _shots = _enemyShots = _reloads = 0;
            _reloadRequested = _captured = false;
            _began = Time.time;
            _sampleAt = 0;
            _frame = -1;
            SetPath();
            _started = true;
            File.AppendAllText(Report + ".txt", "ROUTE " + _route.Name + " | normalHealth=" + _health.CurrentHealth + " magazine=" + _weapon.Magazine +
                " | real Rigidbody movement/look/weapon; scripted input, not a human balance test.\n");
            File.WriteAllText(Report + ".csv", "seconds,health,x,y,z,waypoint,shots,reloads,enemiesAlive,contacts,magazine\n");
        }

        private static void SetPath()
        {
            Vector3 feet = _playerTarget.NavigationPosition;
            if (!NavMesh.SamplePosition(feet, out NavMeshHit a, .3f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(_route.Points[_waypoint], out NavMeshHit b, .2f, NavMesh.AllAreas))
                throw new InvalidOperationException("Player route endpoint is off NavMesh.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Player route became unreachable.");
            _corners = path.corners;
            _corner = _corners.Length > 1 ? 1 : 0;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (!_started) { if (Time.timeSinceLevelLoad > .5f) Initialize(); return; }
                if (_frame == Time.frameCount) return;
                _frame = Time.frameCount;
                float elapsed = Time.time - _began;
                if (_health.IsDead) { Finish("FAIL player died before exit"); return; }
                if (elapsed > MaximumSeconds || EditorApplication.timeSinceStartup > _deadline) { Finish("FAIL traversal timed out"); return; }
                Vector3 feet = _playerTarget.NavigationPosition;
                Vector3 delta = _corners[_corner] - feet;
                if (Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(_corners[_corner].x, _corners[_corner].z)) < .3f && Mathf.Abs(delta.y) < .35f)
                {
                    if (++_corner >= _corners.Length)
                    {
                        if (++_waypoint >= _route.Points.Length)
                        {
                            bool combatVerified = _shots > 0 && _reloads > 0 && _actors.Any(a => a.Health.IsDead);
                            Finish(_runtimeIssues == 0 && combatVerified ? "PASS route, firing, reload and enemy death" : "FAIL route ended without clean combat evidence");
                            return;
                        }
                        SetPath();
                    }
                    delta = _corners[_corner] - feet;
                }
                delta.y = 0;
                Vector3 local = _orientation.InverseTransformDirection(delta.normalized);
                _input.Move = new Vector2(local.x, local.z) * Mathf.Clamp(delta.magnitude / .6f, .3f, 1);
                Vector3 aim = _camera.transform.position + delta.normalized * 10;
                EnemyActor target = VisibleTarget(out Vector3 observedAim);
                if (target != null) aim = observedAim;
                Vector3 toward = aim - _camera.transform.position;
                Quaternion desired = Quaternion.LookRotation(toward);
                float turn = 200 * Mathf.Min(Time.deltaTime, .05f);
                _input.Look = new Vector2(Mathf.Clamp(Mathf.DeltaAngle(_camera.transform.eulerAngles.y, desired.eulerAngles.y), -turn, turn),
                    -Mathf.Clamp(Mathf.DeltaAngle(_camera.transform.eulerAngles.x, desired.eulerAngles.x), -turn, turn)) / Mathf.Max(.001f, _sensitivity);
                _input.IsAimHeld = target != null;
                _input.IsFireHeld = target != null && Vector3.Angle(_camera.transform.forward, toward) < 3 && elapsed > 1;
                _input.WasReloadPressed = !_reloadRequested && _shots >= 4 && _weapon.Magazine < _weapon.MagazineSize && !_weapon.IsReloadInProgress;
                if (_input.WasReloadPressed) _reloadRequested = true;
                if (!_captured && elapsed > 8)
                {
                    _captured = true;
                    BattlefieldBaseline.Capture(_reader.gameObject.scene, "playtest-" + SessionState.GetInt(Key + ".Route", 0),
                        _camera.transform.position, _camera.transform.rotation, false, BattlefieldBlockout.ReportFolder);
                }
                if (Time.time >= _sampleAt)
                {
                    _sampleAt = Time.time + .5f;
                    Vector3 p = _reader.transform.position;
                    File.AppendAllText(Report + ".csv", string.Join(",", N(elapsed), N(_health.CurrentHealth), N(p.x), N(p.y), N(p.z), _waypoint,
                        _shots, _reloads, _actors.Count(a => !a.Health.IsDead), _actors.Count(a => a.GetComponent<EnemyBrain>().Memory.HasContact), _weapon.Magazine) + "\n");
                }
            }
            catch (Exception exception) { File.AppendAllText(Report + ".txt", exception + "\n"); Finish("FAIL " + exception.Message); }
        }

        private static EnemyActor VisibleTarget(out Vector3 aim)
        {
            aim = default;
            EnemyActor selected = null;
            float nearest = float.PositiveInfinity;
            foreach (var actor in _actors)
            {
                if (actor.Health.IsDead) continue;
                Vector3 point = actor.Target.AimSample(2);
                Vector3 delta = point - _camera.transform.position;
                if (delta.sqrMagnitude >= nearest) continue;
                int count = Physics.RaycastNonAlloc(_camera.transform.position, delta.normalized, AimHits, delta.magnitude + .1f, ~0, QueryTriggerInteraction.Ignore);
                if (count == AimHits.Length) continue;
                float distance = float.PositiveInfinity;
                Collider first = null;
                for (int i = 0; i < count; i++)
                    if (!AimHits[i].transform.IsChildOf(_reader.transform) && AimHits[i].distance < distance)
                    { distance = AimHits[i].distance; first = AimHits[i].collider; }
                if (first == null || first.GetComponentInParent<EnemyActor>() != actor) continue;
                nearest = delta.sqrMagnitude;
                selected = actor;
                aim = point;
            }
            return selected;
        }

        private static string N(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
        private static void Shot(WeaponShotResult shot) => _shots++;
        private static void EnemyShot() => _enemyShots++;
        private static void Reload(float duration) => _reloads++;
        private static void Finish(string result)
        {
            _result = result;
            if (_input != null) { _input.Move = _input.Look = Vector2.zero; _input.IsFireHeld = _input.IsAimHeld = _input.WasReloadPressed = false; }
            EditorApplication.isPlaying = false;
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert && type != LogType.Warning) return;
            File.AppendAllText(Report + ".txt", type + ": " + message + "\n" + stack + "\n");
            if (!message.StartsWith("WeaponAmmoEmission could not find ordered reload segments", StringComparison.Ordinal)) _runtimeIssues++;
        }
    }
}
