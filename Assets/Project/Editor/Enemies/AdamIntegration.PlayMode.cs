using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamIntegration
    {
        private const string PlayKey = "AdamIntegration.PlayValidation";
        private const string SetupKey = "AdamIntegration.SceneSetup";
        private static IEnumerator playRoutine;
        private static double playDeadline;
        private static bool priorRunInBackground;
        private static readonly List<string> playErrors = new List<string>();
        [Serializable] private sealed class SceneSetupData { public SceneSetup[] scenes; }
        [InitializeOnLoadMethod] private static void HookPlayValidation()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }
        public static void ValidatePlayMode()
        {
            for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; preserve it before validation.");
            ValidateAssets();
            SessionState.SetString(SetupKey,JsonUtility.ToJson(new SceneSetupData{scenes=EditorSceneManager.GetSceneManagerSetup()}));
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Evidence+"/play-mode.txt","Live Editor Play Mode validation: "+DateTime.UtcNow.ToString("O")+"\n");
            SessionState.SetInt(PlayKey+"LogOffset",File.Exists("Logs/EnemyValidation.txt")?File.ReadAllText("Logs/EnemyValidation.txt").Length:0);
            EditorSceneManager.OpenScene(EnemyAssetBuilder.ScenePath);
            SessionState.SetBool(PlayKey,true);
            EditorApplication.isPlaying=true;
        }
        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(PlayKey,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                playErrors.Clear();Application.logMessageReceived+=PlayLog;
                priorRunInBackground=Application.runInBackground;Application.runInBackground=true;
                playRoutine=PlayTests();playDeadline=EditorApplication.timeSinceStartup+120;
                EditorApplication.update+=PlayTick;
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PlayKey,false);
                var saved=JsonUtility.FromJson<SceneSetupData>(SessionState.GetString(SetupKey,""));
                EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
                File.AppendAllText(Evidence+"/play-mode.txt","Editor scene setup restored.\n");
            }
        }
        private static void PlayLog(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)playErrors.Add(message+"\n"+stack); }
        private static void PlayTick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(EditorApplication.timeSinceStartup>playDeadline)throw new TimeoutException("Play Mode validation exceeded 120 seconds.");
                if(!playRoutine.MoveNext())FinishPlay(null);
            }
            catch(Exception e){FinishPlay(e);}
        }
        private static void FinishPlay(Exception error)
        {
            EditorApplication.update-=PlayTick;Application.logMessageReceived-=PlayLog;playRoutine=null;
            Application.runInBackground=priorRunInBackground;
            string oldLog=File.Exists("Logs/EnemyValidation.txt")?File.ReadAllText("Logs/EnemyValidation.txt"):"";
            int offset=SessionState.GetInt(PlayKey+"LogOffset",0);
            if(oldLog.Length>=offset)File.AppendAllText(Evidence+"/play-mode.txt",oldLog.Substring(offset));
            foreach(var e in playErrors)File.AppendAllText(Evidence+"/play-mode.txt","FAIL Console: "+e+"\n");
            if(error!=null)File.AppendAllText(Evidence+"/play-mode.txt","FAIL "+error+"\n");
            File.AppendAllText(Evidence+"/play-mode.txt",error==null&&playErrors.Count==0?"RESULT: PASS\n":"RESULT: FAIL\n");
            EditorApplication.isPlaying=false;
        }
        private static void PlayCheck(bool value,string message)
        { if(!value)throw new InvalidOperationException(message);File.AppendAllText(Evidence+"/play-mode.txt","PASS "+message+"\n"); }
        private static IEnumerator PlayTests()
        {
            // Exercise the original shared enemy suite without its asset-builder or scene-save entry point.
            var suite=typeof(EnemyValidation);const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
            suite.GetField("_failed",flags).SetValue(null,false);
            suite.GetMethod("StateMachineTests",flags).Invoke(null,null);
            suite.GetMethod("ValidatePrefabs",flags).Invoke(null,null);
            var visual=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPath),new Vector3(30,0,0),Quaternion.identity);
            var animator=visual.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            PlayCheck(animator.isHuman&&animator.avatar.isValid&&!animator.applyRootMotion,"Original Humanoid Avatar valid; root motion disabled");
            foreach(var expected in new[]{"Speed","Dead","Reloading","Attack"})PlayCheck(animator.parameters.Any(p=>p.name==expected),"Animator contract parameter: "+expected);
            animator.SetFloat("Speed",1);float end=Time.time+1;while(Time.time<end)yield return null;
            PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("Adam_Walk"),"Speed drives the authored Humanoid Adam_Walk state");
            var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);Quaternion before=foot.localRotation;
            end=Time.time+.25f;while(Time.time<end)yield return null;
            PlayCheck(Quaternion.Angle(before,foot.localRotation)>.1f,"Humanoid foot bones animate in Play Mode");
            PlayCheck(Vector3.Distance(visual.transform.position,new Vector3(30,0,0))<.001f,"Animation does not move gameplay root");
            animator.SetBool("Dead",true);end=Time.time+.3f;while(Time.time<end)yield return null;
            PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("Dead (animation pending)"),"Dead parameter transitions without an incompatible blockout clip");
            animator.SetBool("Dead",false);animator.SetFloat("Speed",0);end=Time.time+.3f;while(Time.time<end)yield return null;
            PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("Stationary pose (authored idle pending)"),"Animator resets to stationary pose");
            Object.Destroy(visual);
            var routine=(IEnumerator)suite.GetMethod("RuntimeTests",flags).Invoke(null,null);
            bool captured=false;
            while(routine.MoveNext())
            {
                yield return routine.Current;
                if(captured)continue;
                var moving=Object.FindObjectsByType<Breachpoint.Gameplay.AI.EnemyBrain>().FirstOrDefault(b=>b.isActiveAndEnabled&&b.GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.magnitude>.2f);
                if(moving==null)continue;
                var animated=moving.GetComponentInChildren<Animator>();
                if(animated==null||!animated.isHuman||!animated.GetCurrentAnimatorStateInfo(0).IsName("Adam_Walk"))continue;
                PlayCheck(animated.GetFloat("Speed")>.1f,"Existing EnemyAnimationBridge drives Humanoid walk on a moving Rifleman");
                var camera=new GameObject("Adam runtime verification camera").AddComponent<Camera>();
                var hd=camera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
                hd.antialiasing=UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var photoVolume=new GameObject("Temporary still capture settings").AddComponent<UnityEngine.Rendering.Volume>();
                photoVolume.isGlobal=true;photoVolume.priority=100000;
                var photoProfile=ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                photoProfile.Add<UnityEngine.Rendering.HighDefinition.MotionBlur>(true).intensity.Override(0);
                photoVolume.sharedProfile=photoProfile;
                camera.nearClipPlane=.03f;camera.fieldOfView=32;
                camera.transform.position=moving.transform.position+new Vector3(2.7f,1.65f,3.4f);
                camera.transform.LookAt(moving.transform.position+new Vector3(0,.96f,0));
                CaptureCamera(camera,"Docs/Art/AdamIntegration/Renders/12_live_rifleman_walk.png",1400,1600);
                Object.Destroy(camera.gameObject);Object.Destroy(photoVolume.gameObject);Object.Destroy(photoProfile);captured=true;
            }
            PlayCheck(captured,"Captured the integrated Rifleman walking in the existing enemy arena during Play Mode");
            PlayCheck(playErrors.Count==0,"No new errors, exceptions or assertions during live Play Mode validation");
        }
    }
}
