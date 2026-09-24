using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    // Task-specific editor commands. No runtime component or gameplay ownership.
    [InitializeOnLoad]
    public static partial class AdamIntegration
    {
        public const string Source = "Assets/UnityTechnologies/Adam Character Pack/Adam/Adam.FBX";
        public const string Root = "Assets/Project/Art/Character/Enemies/Adam";
        public const string Evidence = "Docs/Art/AdamIntegration/Evidence";
        private const string Request = "Library/AdamIntegration.request";
        private static double nextPoll;
        internal static bool SavingIntegration;
        static AdamIntegration() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            Directory.CreateDirectory(Evidence);
            try
            {
                SavingIntegration = true;
                if (command == "audit") Audit();
                else if (command == "refresh") AssetDatabase.Refresh();
                else if (command == "build") BuildPresentation();
                else if (command == "render") RenderValidation();
                else if (command == "integrate") IntegrateRifleman();
                else if (command == "validate") ValidateAssets();
                else if (command == "play") ValidatePlayMode();
                else throw new InvalidOperationException("Unknown Adam command: " + command);
                File.WriteAllText(Evidence + "/command-status.json", JsonUtility.ToJson(new Status { command=command, success=true, time=DateTime.UtcNow.ToString("O") }, true));
            }
            catch (Exception e)
            {
                File.WriteAllText(Evidence + "/command-status.json", JsonUtility.ToJson(new Status { command=command, error=e.ToString(), time=DateTime.UtcNow.ToString("O") }, true));
                Debug.LogException(e);
            }
            finally { SavingIntegration = false; }
        }
        [Serializable] private sealed class Status { public string command, time, error; public bool success; }
        [Serializable] public sealed class AuditData
        {
            public string unity, pipeline, project, animationType;
            public bool avatarValid, avatarHuman, optimized;
            public float globalScale;
            public string[] humanoidMapping, transforms, clips;
            public RendererData[] renderers;
            public MaterialData[] materials;
            public SceneRendererData[] sceneRenderers;
        }
        [Serializable] public sealed class SceneRendererData { public string path, kind; public string[] materialPaths; public bool active, enabled; }
        [Serializable] public sealed class RendererData { public string name, mesh; public int triangles, bones; public string[] materials; public Vector3 center, size; }
        [Serializable] public sealed class MaterialData { public string name, path, shader; public TextureData[] textures; public FloatData[] floats; public ColorData[] colors; }
        [Serializable] public sealed class TextureData { public string property, path; public int width, height; public bool srgb, mipmaps; public string type, compression; public int maxSize; }
        [Serializable] public sealed class FloatData { public string name; public float value; }
        [Serializable] public sealed class ColorData { public string name; public Color value; }
        [MenuItem("Breachpoint/Enemies/Adam/Audit source (read only)")]
        public static void Audit()
        {
            Directory.CreateDirectory(Evidence);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) throw new InvalidOperationException("Adam source could not be loaded");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Source);
            var renderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var animator = source.GetComponentInChildren<Animator>(true);
            var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
            var report = new AuditData {
                unity=Application.unityVersion, project=Application.dataPath,
                pipeline=GraphicsSettings.currentRenderPipeline == null ? "None" : GraphicsSettings.currentRenderPipeline.GetType().FullName,
                animationType=importer.animationType.ToString(), optimized=importer.optimizeGameObjects, globalScale=importer.globalScale,
                avatarValid=animator != null && animator.avatar != null && animator.avatar.isValid,
                avatarHuman=animator != null && animator.avatar != null && animator.avatar.isHuman,
                humanoidMapping=importer.humanDescription.human.Select(h=>h.humanName+" = "+h.boneName).ToArray(),
                transforms=source.GetComponentsInChildren<Transform>(true).Select(t=>AnimationUtility.CalculateTransformPath(t,source.transform)).ToArray(),
                clips=AssetDatabase.FindAssets("t:AnimationClip", new[]{"Assets/UnityTechnologies/Adam Character Pack/Adam"}).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray(),
                renderers=renderers.Select(r=>new RendererData { name=r.name, mesh=r.sharedMesh.name, triangles=(int)(r.sharedMesh.GetIndexCount(0)/3), bones=r.bones.Length, materials=r.sharedMaterials.Select(m=>m==null?"MISSING":m.name).ToArray(),center=r.localBounds.center,size=r.localBounds.size }).ToArray(),
                materials=materials.Select(ReadMaterial).ToArray()
            };
            // Mesh submeshes can hold different surface classes.
            for(int i=0;i<renderers.Length;i++) report.renderers[i].triangles=Enumerable.Range(0,renderers[i].sharedMesh.subMeshCount).Sum(s=>(int)(renderers[i].sharedMesh.GetIndexCount(s)/3));
            Scene preview = EditorSceneManager.OpenPreviewScene("Assets/UnityTechnologies/Adam Character Pack/Scenes/Adam_Scene.unity");
            try
            {
                var original = preview.GetRootGameObjects().First(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==Source);
                var sourceRenderers=original.GetComponentsInChildren<Renderer>(true);
                report.sceneRenderers=sourceRenderers.Select(r=>new SceneRendererData{path=AnimationUtility.CalculateTransformPath(r.transform,original.transform),kind=r.GetType().Name,active=r.gameObject.activeSelf,enabled=r.enabled,materialPaths=r.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray()}).ToArray();
                report.materials=sourceRenderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Select(ReadMaterial).ToArray();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(Evidence+"/unity-source-audit.json",JsonUtility.ToJson(report,true));
            File.WriteAllText(Evidence+"/rifleman-before.txt",Describe(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Enemies/Prefabs/Rifleman.prefab")));
        }
        private static MaterialData ReadMaterial(Material m)
        {
            var names=new[]{"_Smoothness","_Glossiness","_GlossMapScale","_Metallic","_BumpScale","_OcclusionStrength","_WorkflowMode","_Mode","_Surface","_Blend","_SmoothnessTextureChannel","_Cull"};
            return new MaterialData {name=m.name,path=AssetDatabase.GetAssetPath(m),shader=m.shader.name,
                floats=names.Where(m.HasProperty).Select(p=>new FloatData{name=p,value=m.GetFloat(p)}).ToArray(),
                colors=new[]{"_BaseColor","_Color","_SpecColor","_EmissionColor"}.Where(m.HasProperty).Select(p=>new ColorData{name=p,value=m.GetColor(p)}).ToArray(),
                textures=m.GetTexturePropertyNames().Where(p=>m.GetTexture(p)!=null).Select(p=>{
                    var t=m.GetTexture(p);var path=AssetDatabase.GetAssetPath(t);var imp=AssetImporter.GetAtPath(path) as TextureImporter;
                    return new TextureData{property=p,path=path,width=t.width,height=t.height,srgb=imp!=null&&imp.sRGBTexture,mipmaps=imp!=null&&imp.mipmapEnabled,type=imp==null?"":imp.textureType.ToString(),compression=imp==null?"":imp.textureCompression.ToString(),maxSize=imp==null?0:imp.maxTextureSize}; }).ToArray() };
        }
        private static string Describe(GameObject root)
        {
            var b=new StringBuilder();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                b.AppendLine(AnimationUtility.CalculateTransformPath(t,root.transform)+" | "+string.Join(", ",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name)));
            return b.ToString();
        }
    }
    // Unity's controller creation can flush unrelated dirty materials. Source pack is read-only during these commands.
    internal sealed class AdamSourceSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths) => AdamIntegration.SavingIntegration
            ? paths.Where(p => !p.StartsWith("Assets/UnityTechnologies/Adam Character Pack/", StringComparison.Ordinal)).ToArray()
            : paths;
    }
}
