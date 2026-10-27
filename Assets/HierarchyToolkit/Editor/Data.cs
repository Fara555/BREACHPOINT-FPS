using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HierarchyToolkit
{
    public enum DecorationKind { Normal = 0, Folder = 1 }
    [Serializable] public sealed class Decoration
    {
        public string id, label, sceneGuid;
        public Color color;
        public bool hasColor, bookmark;
        public string icon;
        public DecorationKind kind;
        public Decoration Clone(string key) => new Decoration { id = key, label = label, sceneGuid = sceneGuid, color = color, hasColor = hasColor, bookmark = bookmark, icon = icon, kind = kind };
    }
    [Serializable] public sealed class IconEntry { public string name, guid; }
    [Serializable] public sealed class Palette
    {
        public int version = 1;
        public List<Color> colors = new List<Color> { new Color(.25f,.68f,.65f), new Color(.35f,.57f,.9f), new Color(.68f,.45f,.85f), new Color(.93f,.58f,.28f), new Color(.9f,.38f,.45f), new Color(.65f,.72f,.3f) };
        public List<IconEntry> icons = new List<IconEntry>();
        public static Palette Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) throw new FormatException("Palette must be a JSON document smaller than 1 MB.");
            Palette p;
            try { p = JsonUtility.FromJson<Palette>(json); } catch (Exception e) { throw new FormatException("Invalid palette JSON.", e); }
            if (p == null || p.version != 1 || p.colors == null || p.icons == null || p.colors.Count > 256 || p.icons.Count > 256) throw new FormatException("Unsupported or invalid palette format (expected version 1).");
            foreach (var c in p.colors) if (!Finite(c.r) || !Finite(c.g) || !Finite(c.b) || !Finite(c.a) || c.r < 0 || c.r > 1 || c.g < 0 || c.g > 1 || c.b < 0 || c.b > 1 || c.a < 0 || c.a > 1) throw new FormatException("Colors must be finite values between zero and one.");
            foreach (var i in p.icons) if (i == null || string.IsNullOrWhiteSpace(i.name) || i.name.Length > 128 || string.IsNullOrEmpty(i.guid) || i.guid.Length != 32 || !IsHex(i.guid)) throw new FormatException("Each icon requires a name and 32-character asset GUID.");
            return p;
        }
        static bool IsHex(string s) { foreach (char c in s) if (!Uri.IsHexDigit(c)) return false; return true; }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public string ToJson() => JsonUtility.ToJson(this, true);
    }
    [Serializable] public sealed class KeyBinding
    {
        public string action;
        public KeyCode key;
        public bool shift, primary, alt;
        public KeyBinding(string action, KeyCode key, bool shift = false, bool primary = false) { this.action = action; this.key = key; this.shift = shift; this.primary = primary; }
        public bool Matches(Event e) => e.keyCode == key && e.shift == shift && (e.control || e.command) == primary && e.alt == alt;
    }
    public static class ToolkitEvents
    {
        public static event Action Changed;
        public static void Notify() { Changed?.Invoke(); EditorApplication.RepaintHierarchyWindow(); }
        public static bool Enabled => ProjectSettings.instance.enabled && Preferences.instance.enabled;
    }
    [Serializable] public sealed class SessionEntry
    {
        public GameObject target;
        public string stableId;
        public Decoration temporary;
    }
    public static class StableIdentity
    {
        static readonly Dictionary<string,Dictionary<(ulong,ulong),GameObject>> PrefabKeys=new Dictionary<string,Dictionary<(ulong,ulong),GameObject>>();
        public static void ClearCache()=>PrefabKeys.Clear();
        public static string SceneGuid(GameObject go)
        {
            var stage = PrefabStageUtility.GetPrefabStage(go);
            var path = stage != null ? stage.assetPath : go.scene.path;
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }
        public static string For(GameObject go)
        {
            if (!go) return null;
            var stage = PrefabStageUtility.GetPrefabStage(go);
            UnityEngine.Object target = go;
            if(stage!=null)
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);if(!root)return null;
                var source=PrefabUtility.GetCorrespondingObjectFromSource(go);
                if(source && AssetDatabase.GetAssetPath(source)==stage.assetPath)target=source;
                else
                {
                    var raw=GlobalObjectId.GetGlobalObjectIdSlow(go);
                    if(!PrefabKeys.TryGetValue(stage.assetPath,out var map))
                    {
                        map=new Dictionary<(ulong,ulong),GameObject>();
                        foreach(var t in root.GetComponentsInChildren<Transform>(true)){var assetId=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject);map[(assetId.targetObjectId,assetId.targetPrefabId)]=t.gameObject;}
                        PrefabKeys[stage.assetPath]=map;
                    }
                                        ulong localId=raw.targetObjectId;
                    if(localId==0)localId=HierarchyCompatibility.PrefabFileHint(go);
                    if(localId==0)return null;
                    if(!map.TryGetValue((localId,raw.targetPrefabId),out var assetObject))
                    {
                        foreach(var pair in map)if(pair.Key.Item1==localId){if(assetObject)return null;assetObject=pair.Value;}
                        if(!assetObject)return null;
                    }
                    target=assetObject;
                }
            }
            else if (!EditorUtility.IsPersistent(go) && string.IsNullOrEmpty(go.scene.path)) return null;
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return gid.targetObjectId == 0 || gid.assetGUID.ToString() == "00000000000000000000000000000000" ? null : gid.ToString();
        }
        public static GameObject Resolve(string id)
        {
            if (!GlobalObjectId.TryParse(id, out var gid)) return null;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && gid.assetGUID.ToString() == AssetDatabase.AssetPathToGUID(stage.assetPath))
            {
                foreach (var t in stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true)) if (For(t.gameObject) == id) return t.gameObject;
            }
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
        }
    }
}
