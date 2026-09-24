using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Breachpoint.Gameplay.AI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamIntegration
    {
        public const string PresentationPath=Root+"/Prefabs/PF_Enemy_Adam.prefab";
        public const string RiflemanPath="Assets/Project/Enemies/Prefabs/Rifleman.prefab";
        [Serializable] public sealed class TextureBindings { public MaskBinding[] materials; }
        [Serializable] public sealed class MaskBinding { public string sourceName, sourcePath, maskPath, specularPath, occlusionPath; public bool originalAoWasSrgb; }
        [Serializable] public sealed class BuildData { public string prefab, controller; public MaterialData[] materials; public string[] sourceMaterialPaths; public Vector3 boundsCenter, boundsSize, visualScale; public string[] clips; public bool avatarValid, avatarHuman; }

        [MenuItem("Breachpoint/Enemies/Adam/Build HDRP presentation")]
        public static void BuildPresentation()
        {
            foreach(string d in new[]{"Materials","Prefabs","Config","Validation"}) Directory.CreateDirectory(Root+"/"+d);
            AssetDatabase.Refresh();
            var bindings=JsonUtility.FromJson<TextureBindings>(File.ReadAllText(Root+"/Config/AdamTextureBindings.json"));
            foreach(string path in bindings.materials.Select(b=>b.maskPath).Distinct())
            {
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Default;imp.sRGBTexture=false;imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.alphaIsTransparency=false;
                imp.mipmapEnabled=true;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.compressionQuality=100;imp.isReadable=false;
                var platform=imp.GetPlatformTextureSettings("Standalone");platform.name="Standalone";platform.overridden=true;platform.maxTextureSize=4096;platform.format=TextureImporterFormat.BC7;platform.compressionQuality=100;imp.SetPlatformTextureSettings(platform);
                imp.SaveAndReimport();
            }
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var sourceAvatar=source.GetComponent<Animator>().avatar;
            if(!sourceAvatar.isValid||!sourceAvatar.isHuman)throw new InvalidOperationException("Source Humanoid Avatar invalid");
            var referenceScene=EditorSceneManager.OpenPreviewScene("Assets/UnityTechnologies/Adam Character Pack/Scenes/Adam_Scene.unity");
            var workScene=EditorSceneManager.NewPreviewScene();
            try
            {
                var reference=referenceScene.GetRootGameObjects().First(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==Source);
                var authored=reference.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>AnimationUtility.CalculateTransformPath(r.transform,reference.transform));
                var converted=new Dictionary<Material,Material>();
                foreach(var m in authored.Values.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct()) converted[m]=ConvertMaterial(m,bindings);
                var wrapper=new GameObject("PF_Enemy_Adam");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wrapper,workScene);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,workScene);visual.name="AdamVisual";visual.transform.SetParent(wrapper.transform,false);
                visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
                foreach(var r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    string path=AnimationUtility.CalculateTransformPath(r.transform,visual.transform);
                    var src=authored[path];r.sharedMaterials=src.sharedMaterials.Select(m=>converted[m]).ToArray();
                    r.enabled=src.enabled;r.gameObject.SetActive(src.gameObject.activeSelf);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);
                }
                // Original hierarchy, meshes, bones, weights and Avatar remain direct FBX references.
                var animator=visual.GetComponent<Animator>();animator.applyRootMotion=false;
                animator.runtimeAnimatorController=BuildController();
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                if(visual.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Unexpected imported visual colliders; review required");
                var bounds=VisibleBounds(wrapper);
                if(bounds.size.y<1.7f||bounds.size.y>2f)throw new InvalidOperationException("Unexpected Adam scale: "+bounds);
                PrefabUtility.SaveAsPrefabAsset(wrapper,PresentationPath);
                var data=new BuildData{prefab=PresentationPath,controller=AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),materials=converted.Values.Select(ReadMaterial).ToArray(),sourceMaterialPaths=converted.Keys.Select(AssetDatabase.GetAssetPath).Distinct().ToArray(),boundsCenter=bounds.center,boundsSize=bounds.size,visualScale=visual.transform.localScale,avatarValid=animator.avatar.isValid,avatarHuman=animator.avatar.isHuman,clips=animator.runtimeAnimatorController.animationClips.Select(c=>c.name+" | Humanoid="+c.isHumanMotion+" | "+AssetDatabase.GetAssetPath(c)).ToArray()};
                File.WriteAllText(Evidence+"/presentation-build.json",JsonUtility.ToJson(data,true));
            }
            finally{EditorSceneManager.ClosePreviewScene(workScene);EditorSceneManager.ClosePreviewScene(referenceScene);}
        }

        private static Material ConvertMaterial(Material src,TextureBindings bindings)
        {
            string path=Root+"/Materials/M_"+src.name.Replace(' ','_')+"_HDRP.mat";
            var material=new Material(Shader.Find("HDRP/Lit")){name="M_"+src.name.Replace(' ','_')+"_HDRP"};
            if(src.shader.name=="HDRP/Lit") material.CopyPropertiesFromMaterial(src); // Disabled rig helpers only.
            else
            {
                var sg=src.GetTexture("_SpecGlossMap");
                material.SetFloat("_MaterialID",4);material.SetFloat("_EnergyConservingSpecularColor",1);
                material.SetColor("_BaseColor",src.GetColor("_BaseColor"));
                material.SetTexture("_BaseColorMap",src.GetTexture("_BaseMap"));
                material.SetTextureScale("_BaseColorMap",src.GetTextureScale("_BaseMap"));material.SetTextureOffset("_BaseColorMap",src.GetTextureOffset("_BaseMap"));
                material.SetTexture("_NormalMap",src.GetTexture("_BumpMap"));material.SetFloat("_NormalScale",src.GetFloat("_BumpScale"));
                material.SetTexture("_SpecularColorMap",sg);
                // Legacy SG texture replaces _SpecColor; HDRP multiplies it, therefore white is required.
                material.SetColor("_SpecularColor",sg!=null?Color.white:src.GetColor("_SpecColor"));
                float smooth=src.GetFloat("_Smoothness");material.SetFloat("_Smoothness",smooth);
                if(sg!=null)
                {
                    var b=bindings.materials.Single(x=>x.sourceName==src.name&&x.sourcePath==AssetDatabase.GetAssetPath(src));
                    material.SetTexture("_MaskMap",AssetDatabase.LoadAssetAtPath<Texture2D>(b.maskPath));
                    material.SetFloat("_SmoothnessRemapMin",0);material.SetFloat("_SmoothnessRemapMax",smooth);
                    material.SetFloat("_AORemapMin",1-src.GetFloat("_OcclusionStrength"));material.SetFloat("_AORemapMax",1);
                }
                material.SetTexture("_EmissiveColorMap",src.GetTexture("_EmissionMap"));
                material.SetColor("_EmissiveColor",src.IsKeywordEnabled("_EMISSION")?src.GetColor("_EmissionColor"):Color.black);
                material.SetFloat("_AlbedoAffectEmissive",0);material.SetFloat("_UseEmissiveIntensity",0);
                if(src.GetFloat("_Surface")>0)
                {
                    material.SetFloat("_SurfaceType",1);material.SetFloat("_BlendMode",0);
                    material.SetFloat("_EnableBlendModePreserveSpecularLighting",1);
                    material.SetFloat("_RefractionModel",0);material.SetFloat("_TransparentZWrite",0);
                    material.SetFloat("_TransparentDepthPrepassEnable",0);material.SetFloat("_TransparentDepthPostpassEnable",0);
                    material.SetFloat("_ReceivesSSRTransparent",0);material.renderQueue=3000;
                }
            }
            HDShaderUtils.ResetMaterialKeywords(material);
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing==null){AssetDatabase.CreateAsset(material,path);existing=material;}
            else {EditorUtility.CopySerialized(material,existing);Object.DestroyImmediate(material);EditorUtility.SetDirty(existing);}
            AssetDatabase.SaveAssetIfDirty(existing);return existing;
        }
        private static AnimatorController BuildController()
        {
            string path=Root+"/Config/AC_Enemy_Adam.controller";
            var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(existing!=null)return existing;
            var walk=AssetDatabase.LoadAllAssetsAtPath("Assets/UnityTechnologies/Adam Character Pack/Adam/Adam_Walk.FBX").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            if(!walk.isHumanMotion)throw new InvalidOperationException("Adam_Walk is not Humanoid");
            var c=AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Speed",AnimatorControllerParameterType.Float);c.AddParameter("Dead",AnimatorControllerParameterType.Bool);
            c.AddParameter("Reloading",AnimatorControllerParameterType.Bool);c.AddParameter("Attack",AnimatorControllerParameterType.Trigger);
            var sm=c.layers[0].stateMachine;var idle=sm.AddState("Stationary pose (authored idle pending)");idle.motion=walk;idle.speed=0;
            var locomotion=sm.AddState("Adam_Walk");locomotion.motion=walk;locomotion.speed=1;
            var dead=sm.AddState("Dead (animation pending)");dead.writeDefaultValues=false;
            sm.defaultState=idle;
            var go=idle.AddTransition(locomotion);go.hasExitTime=false;go.duration=.15f;go.AddCondition(AnimatorConditionMode.Greater,.1f,"Speed");go.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
            var stop=locomotion.AddTransition(idle);stop.hasExitTime=false;stop.duration=.15f;stop.AddCondition(AnimatorConditionMode.Less,.1f,"Speed");
            var death=sm.AddAnyStateTransition(dead);death.hasExitTime=false;death.duration=.1f;death.canTransitionToSelf=false;death.AddCondition(AnimatorConditionMode.If,0,"Dead");
            var reset=dead.AddTransition(idle);reset.hasExitTime=false;reset.duration=0;reset.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssetIfDirty(c);return c;
        }
        private static Bounds VisibleBounds(GameObject root)
        {
            var rs=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        [MenuItem("Breachpoint/Enemies/Adam/Integrate verified presentation into Rifleman")]
        public static void IntegrateRifleman()
        {
            if(!File.Exists(Evidence+"/visual-validation.json"))throw new InvalidOperationException("Render and inspect HDRP presentation before gameplay integration");
            var verified=JsonUtility.FromJson<VisualReport>(File.ReadAllText(Evidence+"/visual-validation.json"));
            if(verified.consoleErrors.Length!=0||verified.images.Length<11||verified.images.Any(p=>!File.Exists("Docs/Art/AdamIntegration/Renders/"+p)))
                throw new InvalidOperationException("HDRP visual validation must finish without errors before integration");
            var root=PrefabUtility.LoadPrefabContents(RiflemanPath);
            try
            {
                var model=root.transform.Find("Model");if(model==null)throw new InvalidOperationException("Rifleman Model missing");
                // Keep Model's identity for scene references. Only remove its old presentation children and Animator.
                foreach(Transform child in model.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                var oldAnimator=model.GetComponent<Animator>();if(oldAnimator!=null)Object.DestroyImmediate(oldAnimator);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPath),root.scene);
                visual.transform.SetParent(model,false);
                var animator=visual.GetComponentInChildren<Animator>(true);
                var bridge=new SerializedObject(root.GetComponent<EnemyAnimationBridge>());bridge.FindProperty("_animator").objectReferenceValue=animator;bridge.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,RiflemanPath);
                File.WriteAllText(Evidence+"/rifleman-after.txt",Describe(root));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            ValidateAssets();
        }
        public static void ValidateAssets()
        {
            var presentation=AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPath);
            if(presentation==null)throw new InvalidOperationException("Presentation missing");
            foreach(var c in presentation.GetComponentsInChildren<Component>(true))
                if(c==null||c is MonoBehaviour||c is Collider)throw new InvalidOperationException("Unexpected gameplay/missing component in presentation");
            foreach(var r in presentation.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)
                if(m==null||m.shader.name!="HDRP/Lit"||!AssetDatabase.GetAssetPath(m).StartsWith(Root+"/Materials/"))throw new InvalidOperationException("Invalid material: "+r.name);
            var a=presentation.GetComponentInChildren<Animator>(true);if(!a.avatar.isValid||!a.avatar.isHuman)throw new InvalidOperationException("Avatar failed");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath);
            foreach(var c in prefab.GetComponentsInChildren<Component>(true))
            {
                if(c==null)throw new InvalidOperationException("Missing script");
                var so=new SerializedObject(c);var it=so.GetIterator();while(it.Next(true))
                    if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&!it.objectReferenceEntityIdValue.Equals(EntityId.None))throw new InvalidOperationException("Missing object reference: "+c.name+" / "+it.propertyPath);
            }
            File.WriteAllText(Evidence+"/asset-validation.txt","PASS: project-side HDRP materials; source Avatar valid Human; no presentation gameplay/colliders; no missing prefab references.\n");
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(presentation,preview);
                var points=new System.Collections.Generic.List<Vector3>();
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>().Where(r=>r.enabled))
                {
                    Mesh baked=null;Mesh mesh=null;var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;
                    if(renderer is SkinnedMeshRenderer skin){baked=new Mesh();skin.BakeMesh(baked);mesh=baked;}
                    if(mesh!=null)points.AddRange(mesh.vertices.Select(renderer.transform.TransformPoint));
                    if(baked!=null)Object.DestroyImmediate(baked);
                }
                var exact=new Bounds(points[0],Vector3.zero);foreach(var p in points)exact.Encapsulate(p);
                File.WriteAllText(Evidence+"/geometry-bounds.txt","Bind-pose visible vertices, metres\nmin="+exact.min.ToString("F6")+"\nmax="+exact.max.ToString("F6")+"\nsize="+exact.size.ToString("F6")+"\nroot scale="+instance.transform.localScale+"\nforward="+instance.transform.forward+"\n");
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
