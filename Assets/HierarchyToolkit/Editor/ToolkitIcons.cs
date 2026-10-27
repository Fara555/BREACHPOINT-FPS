using UnityEditor;
using UnityEngine;
namespace HierarchyToolkit
{
    // Native Unity Editor textures, without generated or copied artwork.
    public static class ToolkitIcons
    {
        public sealed class Entry
        {
            public readonly string key; public readonly Texture2D texture; public readonly GUIContent tooltip;
            public Entry(string key,string label){this.key=key;texture=EditorGUIUtility.IconContent(key).image as Texture2D;tooltip=new GUIContent(string.Empty,label);}
        }
        static Entry E(string name,string label)=>new Entry(name,label);
        public static readonly Entry[][] Groups={
            new[]{E("Folder Icon","Folder"),E("RectTransform Icon","UI"),E("Animator Icon","Animator"),E("cs Script Icon","Script"),E("NavMeshAgent Icon","Navigation"),E("AudioListener Icon","Audio listener"),E("Terrain Icon","Terrain"),E("Prefab Icon","Prefab")},
            new[]{E("Camera Icon","Camera"),E("Rigidbody Icon","Rigidbody"),E("SphereCollider Icon","Sphere collider"),E("CapsuleCollider Icon","Capsule collider")},
            new[]{E("Light Icon","Light"),E("ParticleSystem Icon","Particles"),E("LightProbeGroup Icon","Light probes"),E("ReflectionProbe Icon","Reflection probe")},
            new[]{E("BoxCollider Icon","Box collider"),E("MeshCollider Icon","Mesh collider"),E("WheelCollider Icon","Wheel collider"),E("CharacterController Icon","Character controller"),E("Grid Icon","Grid")},
            new[]{E("AudioSource Icon","Audio source"),E("AudioClip Icon","Audio clip"),E("AudioListener Icon","Audio listener"),E("AudioLowPassFilter Icon","Audio filter"),E("AudioReverbZone Icon","Reverb zone")},
            new[]{E("GameObject Icon","GameObject"),E("MeshFilter Icon","Mesh"),E("TextAsset Icon","Text"),E("Favorite","Favorite"),E("Settings Icon","Settings")}
        };
        public static string ResolveKey(string key)
        {
            switch(key){case "toolkit:folder":return "Folder Icon";case "toolkit:star":return "Favorite";case "toolkit:diamond":return "Prefab Icon";case "toolkit:flag":return "GameObject Icon";case "toolkit:divider":return null;default:return key;}
        }
        public static Texture2D Get(string key){key=ResolveKey(key);return string.IsNullOrEmpty(key)?null:EditorGUIUtility.IconContent(key).image as Texture2D;}
    }
}
