using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamIntegration
    {
        public const string ValidationScene=Root+"/Validation/Adam_HDRP_Validation.unity";
        [Serializable] private sealed class VisualReport { public string pipeline, scene; public string[] images, consoleErrors; public int width=1400,height=1600; }
        [Serializable] private sealed class PerformanceData { public int skinnedRenderers, meshRenderers, disabledRenderers, triangles, materialSlots, materials, transformCount, referencedSkinBones, humanoidBones, textures; public string[] twistBones; public long textureRuntimeBytes; public int approximateBasePassDraws; }

        [MenuItem("Breachpoint/Enemies/Adam/Render HDRP validation")]
        public static void RenderValidation()
        {
            var oldSetup=EditorSceneManager.GetSceneManagerSetup();
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve unsaved scene edits before rendering.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var errors=new List<string>();var images=new List<string>();bool oldAsync=ShaderUtil.allowAsyncCompilation;
            Application.LogCallback log=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);};
            Application.logMessageReceived+=log;
            try
            {
                SceneManager.SetActiveScene(scene);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPath),scene);
                // Freeze the source bind pose for like-for-like material inspection; controller stays on the prefab.
                visual.GetComponentInChildren<Animator>().enabled=false;
                var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);plane.name="Neutral stage";plane.transform.localScale=new Vector3(20,1,20);plane.transform.position=new Vector3(0,-.005f,0);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                string floorPath=Root+"/Validation/M_ValidationFloor.mat";
                var floor=AssetDatabase.LoadAssetAtPath<Material>(floorPath);
                if(floor==null){floor=new Material(Shader.Find("HDRP/Lit"));floor.SetColor("_BaseColor",new Color(.18f,.18f,.18f));floor.SetFloat("_Smoothness",.2f);HDShaderUtils.ResetMaterialKeywords(floor);AssetDatabase.CreateAsset(floor,floorPath);}
                plane.GetComponent<Renderer>().sharedMaterial=floor;
                var profile=MakeValidationProfile();
                var volume=new GameObject("Validation environment").AddComponent<Volume>();volume.isGlobal=true;volume.priority=10000;volume.sharedProfile=profile;
                var key=MakeLight("Key",new Vector3(40,145,0),11000,Color.white);
                var fill=MakeLight("Fill",new Vector3(20,-145,0),2800,new Color(.83f,.90f,1));fill.shadows=LightShadows.None;
                var rim=MakeLight("Rim",new Vector3(15,0,0),4500,Color.white);rim.shadows=LightShadows.None;
                var cam=new GameObject("Validation Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.nearClipPlane=.03f;cam.farClipPlane=100;cam.fieldOfView=32;
                var hd=cam.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.08f,.09f,.10f);hd.antialiasing=HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.transform.position=new Vector3(2.7f,1.65f,3.4f);cam.transform.LookAt(new Vector3(0,.96f,0));
                cam.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                EditorSceneManager.SaveScene(scene,ValidationScene);
                ShaderUtil.allowAsyncCompilation=false;
                Directory.CreateDirectory("Docs/Art/AdamIntegration/Renders");
                Action<string,Vector3,Vector3,float> capture=(name,pos,target,fov)=>{
                    cam.transform.position=pos;cam.transform.LookAt(target);cam.fieldOfView=fov;
                    CaptureCamera(cam,"Docs/Art/AdamIntegration/Renders/"+name+".png",1400,1600);images.Add(name+".png");
                    File.WriteAllText(Evidence+"/render-progress.txt",string.Join("\n",images));
                };
                var center=new Vector3(0,.96f,0);
                capture("01_front",new Vector3(0,1.1f,4),center,32);
                capture("02_side",new Vector3(4,1.1f,0),center,32);
                capture("03_back",new Vector3(0,1.1f,-4),center,32);
                capture("04_threequarter",new Vector3(2.7f,1.65f,3.4f),center,32);
                capture("05_head",new Vector3(.42f,1.76f,1.05f),new Vector3(0,1.65f,0),30);
                capture("06_torso",new Vector3(.7f,1.4f,1.6f),new Vector3(0,1.19f,0),30);
                capture("07_full_body",new Vector3(1.9f,1.3f,4.4f),center,30);
                key.transform.rotation=Quaternion.Euler(5,-80,0);key.intensity=18000;fill.intensity=900;rim.intensity=1800;
                capture("08_bright_side",new Vector3(2.7f,1.65f,3.4f),center,32);
                key.intensity=650;fill.intensity=120;rim.intensity=1500;
                capture("09_dark",new Vector3(2.7f,1.65f,3.4f),center,32);
                key.color=new Color(1,.035f,.015f);key.intensity=9000;fill.color=new Color(.03f,.13f,1);fill.intensity=11000;rim.intensity=800;
                capture("10_red_blue",new Vector3(2.7f,1.65f,3.4f),center,32);
                // Match the existing enemy arena's documented 12,000 lux / EV9 lighting baseline.
                key.color=new Color(1,.94f,.84f);key.transform.rotation=Quaternion.Euler(50,150,0);key.intensity=12000;fill.color=new Color(.6f,.75f,1);fill.intensity=1800;rim.intensity=1500;
                var exposure=profile.components.OfType<Exposure>().First();float prior=exposure.fixedExposure.value;exposure.fixedExposure.value=9;
                capture("11_game_like",new Vector3(2.7f,1.65f,3.4f),center,32);exposure.fixedExposure.value=prior;
                WritePerformance(visual);
                File.WriteAllText(Evidence+"/visual-validation.json",JsonUtility.ToJson(new VisualReport{pipeline=GraphicsSettings.currentRenderPipeline.GetType().FullName,scene=ValidationScene,images=images.ToArray(),consoleErrors=errors.ToArray()},true));
                if(errors.Count>0)throw new InvalidOperationException("HDRP render produced errors; inspect visual-validation.json");
            }
            finally{ShaderUtil.allowAsyncCompilation=oldAsync;Application.logMessageReceived-=log;EditorSceneManager.RestoreSceneManagerSetup(oldSetup);}
        }
        private static Light MakeLight(string name,Vector3 angles,float lux,Color color)
        {
            var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;light.gameObject.AddComponent<HDAdditionalLightData>();light.lightUnit=LightUnit.Lux;light.intensity=lux;light.color=color;light.transform.rotation=Quaternion.Euler(angles);light.shadows=LightShadows.Soft;return light;
        }
        private static VolumeProfile MakeValidationProfile()
        {
            string path=Root+"/Validation/VP_Adam_Studio.asset";
            var p=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);if(p!=null)return p;
            p=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(p,path);
            var env=p.Add<VisualEnvironment>(true);env.skyType.Override((int)SkyType.Gradient);env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            var sky=p.Add<GradientSky>(true);sky.top.Override(new Color(.55f,.60f,.66f));sky.middle.Override(new Color(.22f,.23f,.25f));sky.bottom.Override(new Color(.05f,.05f,.05f));sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);sky.multiplier.Override(400);sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            var e=p.Add<Exposure>(true);e.mode.Override(ExposureMode.Fixed);e.fixedExposure.Override(10);
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            p.Add<Fog>(true).enabled.Override(false);
            foreach(var c in p.components)AssetDatabase.AddObjectToAsset(c,p);
            EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);return p;
        }
        private static void CaptureCamera(Camera camera,string path,int width,int height)
        {
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();var old=RenderTexture.active;
            Texture2D png=null;
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=rt};
                for(int i=0;i<6;i++)RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;png=new Texture2D(width,height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,width,height),0,0);png.Apply();File.WriteAllBytes(path,png.EncodeToPNG());
            }
            finally{RenderTexture.active=old;if(png!=null)Object.DestroyImmediate(png);rt.Release();Object.DestroyImmediate(rt);}
        }
        private static void WritePerformance(GameObject visual)
        {
            var all=visual.GetComponentsInChildren<Renderer>(true);var active=all.Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var skinned=active.OfType<SkinnedMeshRenderer>().ToArray();var materials=active.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t=>t!=null).Distinct().ToArray();
            int triangles=0;foreach(var r in active){var mesh=r is SkinnedMeshRenderer smr?smr.sharedMesh:r.GetComponent<MeshFilter>()?.sharedMesh;if(mesh!=null)triangles+=Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)(mesh.GetIndexCount(s)/3));}
            var d=new PerformanceData{skinnedRenderers=skinned.Length,meshRenderers=active.Length-skinned.Length,disabledRenderers=all.Length-active.Length,triangles=triangles,materialSlots=active.Sum(r=>r.sharedMaterials.Length),materials=materials.Length,transformCount=visual.GetComponentsInChildren<Transform>(true).Length,referencedSkinBones=skinned.SelectMany(r=>r.bones).Distinct().Count(),humanoidBones=((ModelImporter)AssetImporter.GetAtPath(Source)).humanDescription.human.Length,textures=textures.Length,textureRuntimeBytes=textures.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)),twistBones=visual.GetComponentsInChildren<Transform>(true).Where(t=>t.name.ToLowerInvariant().Contains("twist")).Select(t=>t.name).ToArray(),approximateBasePassDraws=active.Sum(r=>r.sharedMaterials.Length)};
            File.WriteAllText(Evidence+"/performance.json",JsonUtility.ToJson(d,true));
        }
    }
}
