using System;
using System.IO;
using System.Linq;
using Breachpoint.Gameplay.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breachpoint.Editor.Enemies
{
    // Isolated lifecycle probe: only the new art prefab runs; no player/enemy/weapon scenario is started.
    [InitializeOnLoad]
    internal static class MarketFanProbe
    {
        private const string Key = "Breachpoint.MarketFanProbe";
        private const string Report = BattlefieldMarketLife.Output + "/fan-playmode.txt";
        private static AmbientFan _fan;
        private static Transform _rotor;
        private static Quaternion _rotation;
        private static Vector3 _position;
        private static double _at;
        private static double _deadline;
        private static int _phase;
        private static int _errors;
        private static bool _settling;

        static MarketFanProbe()
        {
            EditorApplication.playModeStateChanged += Mode;
            EditorApplication.update += Tick;
        }

        internal static void Start()
        {
            BattlefieldMarketLife.Validate();
            var main = BattlefieldBlockout.RequireBlockout();
            if (main.isDirty && !EditorSceneManager.SaveScene(main)) throw new InvalidOperationException("Save the market before the isolated fan probe.");
            var probe = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldMarketLife.Folder + "/PF_MarketDailyUse.prefab");
            PrefabUtility.InstantiatePrefab(prefab, probe);
            string scenePath = BattlefieldMarketLife.Folder + "/FanRuntimeProbe.unity";
            if (!EditorSceneManager.SaveScene(probe, scenePath)) throw new InvalidOperationException("Fan probe fixture could not be saved.");
            EditorSceneManager.CloseScene(probe, true);SceneManager.SetActiveScene(main);
            SessionState.SetString(Key + ".StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + ".Background", Application.runInBackground);
            SessionState.SetFloat(Key + ".TimeScale", Time.timeScale);
            SessionState.SetBool(Key, true);
            File.WriteAllText(Report, "START isolated market fan lifecycle probe\n");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            EditorApplication.isPlaying = true;
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.runInBackground = true;Time.timeScale = 1;
                _phase = -1;_errors = 0;_deadline = EditorApplication.timeSinceStartup + 30;
                Application.logMessageReceived += Log;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Application.logMessageReceived -= Log;
                Time.timeScale = SessionState.GetFloat(Key + ".TimeScale", 1);
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                string old = SessionState.GetString(Key + ".StartScene", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(old) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
                Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
                SessionState.SetBool(Key, false);
                File.AppendAllText(Report, "Returned to original Editor scenes/start-scene setting.\n" + EnemyValidationRunner.ConsoleCounts());
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (now > _deadline && _phase >= 0) throw new InvalidOperationException("Fan probe timed out.");
                if (_phase < 0)
                {
                    var fans = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<AmbientFan>()).ToArray();
                    if (fans.Length != 1) throw new InvalidOperationException("Isolated fixture must contain exactly one fan.");
                    _fan=fans[0];_rotor=(Transform)new SerializedObject(_fan).FindProperty("_rotor").objectReferenceValue;
                    if (_rotor == null) throw new InvalidOperationException("Runtime rotor reference is missing.");
                    _position=_rotor.position;Sample(now);_phase=0;return;
                }
                if (now < _at) return;
                if (_settling) {_rotation=_rotor.localRotation;_at=now+.45;_settling=false;return;}
                if (Vector3.Distance(_position,_rotor.position)>.0001f) throw new InvalidOperationException("Fan rotor drifted out of its guard.");
                float angle=Quaternion.Angle(_rotation,_rotor.localRotation);
                if (_phase==0 || _phase==3)
                {
                    if (angle<10) throw new InvalidOperationException("Enabled fan did not rotate during actual runtime updates.");
                    File.AppendAllText(Report,"PASS runtime rotation "+angle.ToString("F1")+" degrees; pivot stayed fixed.\n");
                }
                else if (angle>.01f) throw new InvalidOperationException("Paused/disabled fan kept rotating.");
                else File.AppendAllText(Report,_phase==1?"PASS Time.timeScale=0 stops rotation.\n":"PASS disabled component stops rotation.\n");
                if (_phase==0) Time.timeScale=0;
                else if (_phase==1) {Time.timeScale=1;_fan.enabled=false;}
                else if (_phase==2) _fan.enabled=true;
                else
                {
                    if (_errors!=0) throw new InvalidOperationException("Runtime errors occurred in fan fixture.");
                    File.AppendAllText(Report,"RESULT: PASS | runtimeErrors=0\n");EditorApplication.isPlaying=false;return;
                }
                _phase++;Sample(now);
            }
            catch(Exception exception)
            {
                File.AppendAllText(Report,"RESULT: FAIL "+exception+"\n");EditorApplication.isPlaying=false;
            }
        }

        private static void Sample(double now) {_rotation=_rotor.localRotation;_at=now+.1;_settling=true;}
        private static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_errors++;}
    }
}
