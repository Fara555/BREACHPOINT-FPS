using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
#if UNITY_6000_4_OR_NEWER
using RowId = UnityEngine.EntityId;
#else
using RowId = System.Int32;
#endif

namespace HierarchyToolkit
{
    [InitializeOnLoad]
    public static class HierarchyExtension
    {
        public sealed class Row
        {
            public GameObject go;
            public Decoration decoration;
            public Component[] components;
            public GUIContent[] componentIcons, componentTooltips;
            public Texture icon;
            public int depth;
            public float nameWidth;
        }
        static readonly Dictionary<RowId, Row> Cache = new Dictionary<RowId, Row>();
        static readonly HashSet<RowId> Pending = new HashSet<RowId>();
        static readonly Dictionary<string, Texture> Icons = new Dictionary<string, Texture>();
        static readonly ProfilerMarker DrawMarker = new ProfilerMarker("HierarchyToolkit.Row");
        static bool scheduled, barsScheduled;
        static GameObject hovered;
        static EditorWindow hoverWindow;
        static readonly GUIContent PaletteButton = new GUIContent("●", "Colors, icons and bookmarks (Alt-click row also opens palette)");
        static readonly GUIContent Active = new GUIContent("●","Active");
        static readonly GUIContent InactiveParent = new GUIContent("●","Enabled locally; inactive parent");
        static readonly GUIContent Inactive = new GUIContent("○","Inactive locally");
        static readonly GUIContent Folder = new GUIContent(ToolkitIcons.Get("toolkit:folder"));
        static readonly GUIContent Missing = new GUIContent("!", "Missing script — remove or repair through Inspector");
        static GUIStyle Small;
        static GameObject[] duplicateSources;

        static HierarchyExtension()
        {
            #if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += DrawEntity;
#else
            EditorApplication.hierarchyWindowItemOnGUI += Draw;
#endif
            EditorApplication.hierarchyChanged += HierarchyChanged;
            EditorApplication.projectChanged += Invalidate;
            Selection.selectionChanged += Repaint;
            ObjectChangeEvents.changesPublished += ObjectChanged;
            Undo.undoRedoPerformed += UndoChanged;
            EditorSceneManager.sceneSaved += SceneSaved;
            EditorSceneManager.sceneOpened += SceneOpened;
            PrefabStage.prefabSaved += PrefabSaved;
            EditorApplication.playModeStateChanged += PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            ToolkitEvents.Changed += Invalidate;
            HierarchyCompatibility.Hook(GlobalInput, true);
            ScheduleBars();
        }
        static void Dispose()
        {
            HierarchyCompatibility.RestoreRowIcons();
            #if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= DrawEntity;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= Draw;
#endif
            EditorApplication.hierarchyChanged -= HierarchyChanged;
            EditorApplication.projectChanged -= Invalidate; Selection.selectionChanged -= Repaint;
            ObjectChangeEvents.changesPublished -= ObjectChanged; Undo.undoRedoPerformed -= UndoChanged;
            EditorSceneManager.sceneSaved -= SceneSaved; EditorSceneManager.sceneOpened -= SceneOpened;
            PrefabStage.prefabSaved -= PrefabSaved;
            EditorApplication.playModeStateChanged -= PlayChanged; ToolkitEvents.Changed -= Invalidate;
            HierarchyCompatibility.Hook(GlobalInput, false); EditorApplication.delayCall -= Prepare; EditorApplication.delayCall -= RefreshBars;
            foreach(var w in HierarchyCompatibility.Windows()) HierarchyCompatibility.RemoveBar(w);
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
        }
        static void ObjectChanged(ref ObjectChangeEventStream stream)
        {
            bool repaint=false;
            for(int i=0;i<stream.length;i++)
            {
                if(stream.GetEventType(i)!=ObjectChangeKind.ChangeGameObjectOrComponentProperties)continue;
                stream.GetChangeGameObjectOrComponentPropertiesEvent(i,out var change);
#if UNITY_6000_4_OR_NEWER
                var obj=EditorUtility.EntityIdToObject(change.entityId);
#else
                var obj=EditorUtility.InstanceIDToObject(change.instanceId);
#endif
                var go=obj as GameObject;if(!go && obj is Component component)go=component.gameObject;if(!go)continue;
#if UNITY_6000_4_OR_NEWER
                var id=go.GetEntityId();
#else
                var id=go.GetInstanceID();
#endif
                Cache.Remove(id);repaint=true;
            }
            if(repaint)Repaint();
        }
        static void Repaint() => EditorApplication.RepaintHierarchyWindow();
        static void UndoChanged() { DecorationStore.instance.AfterUndo(); Invalidate(); }
        static void HierarchyChanged() { DecorationStore.instance.Reconcile(); Invalidate(); }
        static void SceneSaved(Scene s) { DecorationStore.instance.Reconcile(); Invalidate(); }
        static void SceneOpened(Scene s, OpenSceneMode mode) => Invalidate();
        static void PrefabSaved(GameObject root) { StableIdentity.ClearCache();DecorationStore.instance.Reconcile();Invalidate(); }
        static void PlayChanged(PlayModeStateChange state) { Cache.Clear(); Pending.Clear(); hovered = null; Invalidate(); }
        public static void Invalidate() { HierarchyCompatibility.RestoreRowIcons(); StableIdentity.ClearCache(); Cache.Clear(); Icons.Clear(); ScheduleBars(); Repaint(); }
        static void ScheduleBars() { if (barsScheduled) return; barsScheduled = true; EditorApplication.delayCall += RefreshBars; }
        static void RefreshBars() { barsScheduled = false; foreach (var w in HierarchyCompatibility.Windows()) { if (ToolkitEvents.Enabled) HierarchyCompatibility.InstallBar(w); else HierarchyCompatibility.RemoveBar(w); } }
        static void Queue(RowId id)
        {
            Pending.Add(id); if (scheduled) return; scheduled = true; EditorApplication.delayCall += Prepare;
        }
        static void Prepare()
        {
            scheduled = false;
            foreach (RowId id in Pending)
            {
                #if UNITY_6000_4_OR_NEWER
                var go = EditorUtility.EntityIdToObject(id) as GameObject;
#else
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
#endif
                if (!go) continue;
                var row = new Row { go = go, decoration = DecorationStore.instance.For(go), components = go.GetComponents<Component>() };
                DecorationStore.instance.Track(go, row.decoration);
                for (var p = go.transform.parent; p; p=p.parent) row.depth++;
                row.nameWidth = EditorStyles.label.CalcSize(new GUIContent(go.name)).x;
                row.decoration = RuleEngine.Evaluate(go, row.decoration);
                row.componentIcons = new GUIContent[row.components.Length]; row.componentTooltips = new GUIContent[row.components.Length];
                for(int i=0;i<row.components.Length;i++) row.componentIcons[i] = row.components[i] ? new GUIContent(EditorGUIUtility.ObjectContent(row.components[i], row.components[i].GetType()).image, row.components[i].GetType().Name) : Missing;
                for(int i=0;i<row.components.Length;i++)row.componentTooltips[i]=new GUIContent(row.components[i] ? string.Empty : "!",row.componentIcons[i].tooltip);
                var iconKey = ToolkitIcons.ResolveKey(row.decoration?.icon);
                if(iconKey == "toolkit:divider")iconKey=null;
                if (!string.IsNullOrEmpty(iconKey))
                {
                    if (!Icons.TryGetValue(iconKey, out var tex))
                    {
                        tex = iconKey.StartsWith("toolkit:",StringComparison.Ordinal) ? ToolkitIcons.Get(iconKey) : iconKey.StartsWith("asset:", StringComparison.Ordinal) ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(iconKey.Substring(6))) : EditorGUIUtility.IconContent(iconKey).image;
                        Icons[iconKey] = tex;
                    }
                    row.icon = tex;
                }
                Cache[id] = row;
            }
            Pending.Clear(); Repaint();
        }
        public static void ToggleActive(GameObject go)
        {
            if (!go) return; Undo.RecordObject(go, "Toggle GameObject activity"); go.SetActive(!go.activeSelf);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            EditorUtility.SetDirty(go); Repaint();
        }
        #if UNITY_6000_4_OR_NEWER
        static void DrawEntity(EntityId id, Rect rect) => Draw(id, rect);
#endif
        static void Draw(RowId id, Rect rect)
        {
            if (!ToolkitEvents.Enabled) return;
            using (DrawMarker.Auto())
            {
                if (!Cache.TryGetValue(id, out var row) || !row.go) { Queue(id); ScheduleBars(); return; }
                var e = Event.current; var prefs = Preferences.instance; var d = row.decoration;
                if (rect.Contains(e.mousePosition) && HierarchyCompatibility.IsHierarchy(EditorWindow.mouseOverWindow)) { hovered = row.go; hoverWindow = EditorWindow.mouseOverWindow; }
                if (Small == null) Small = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(0,0,0,0), fontSize = 10 };
                if (e.type == EventType.Repaint)
                {
                    if (prefs.zebra && !prefs.minimal && ((int)(rect.y / rect.height) & 1) == 0) EditorGUI.DrawRect(new Rect(0, rect.y, rect.xMax, rect.height), new Color(0,0,0,EditorGUIUtility.isProSkin ? .06f : .025f));
                    if (prefs.colors && d != null && d.hasColor)
                    {
                        var c = d.color; c.a = prefs.tint;
                        // Unity excludes the reserved right-hand controls from the callback rect.
                        // Paint to the viewport edge; the native tree clip protects the scrollbar.
                        var window = HierarchyCompatibility.DrawingWindow;
                        float width = window ? Mathf.Max(rect.xMax, window.position.width) : rect.xMax;
                        EditorGUI.DrawRect(new Rect(0,rect.y,width,rect.height),c);
                        EditorGUI.DrawRect(new Rect(0,rect.y,3,rect.height),d.color);
                    }
                    if (prefs.lines && !prefs.minimal) for (int depth = 0; depth < row.depth; depth++) EditorGUI.DrawRect(new Rect(rect.x - 14 * (depth + 1) + 7, rect.y, 1, rect.height), new Color(.5f,.5f,.5f,.2f));
                }
                if(e.type==EventType.Repaint && !prefs.minimal)
                {
                    Texture replacement=prefs.icons ? row.icon : null;
                    if(!replacement && prefs.folders && d!=null && d.kind==DecorationKind.Folder)replacement=Folder.image;
                    if(replacement)HierarchyCompatibility.ReplaceRowIcon(HierarchyCompatibility.DrawingWindow,row.go,replacement);
                }
                float right = rect.xMax - 2;
                if (prefs.activation)
                {
                    var r = new Rect(right - 16,rect.y,16,rect.height); right -= 18;
                    var old = GUI.color; GUI.color = row.go.activeSelf ? (row.go.activeInHierarchy ? new Color(.4f,.8f,.65f) : new Color(.9f,.65f,.3f)) : new Color(.6f,.6f,.6f);
                    if (GUI.Button(r, row.go.activeSelf ? (row.go.activeInHierarchy ? Active : InactiveParent) : Inactive, Small)) ToggleActive(row.go); GUI.color = old;

                }
                if (!prefs.minimal && rect.width > 100 && (prefs.colors || prefs.icons || prefs.navigation || prefs.folders))
                {
                    var r = new Rect(right-16,rect.y,16,rect.height); right -= 18;
                    if (GUI.Button(r,PaletteButton,Small)) PalettePopup.Open(r,Targets(row.go));
                }
                if (prefs.componentMap && !prefs.minimal)
                {
                    float nameWidth = row.nameWidth;
                    int count = Mathf.Min(prefs.maxComponents, row.components.Length - 1);
                    for(int i=1;i<=count && right-18 > rect.x+nameWidth+38;i++)
                    {
                        var r = new Rect(right-16,rect.y,16,16); right -= 18;
                        if(e.type==EventType.Repaint && row.componentIcons[i].image) GUI.DrawTexture(new Rect(r.x,r.y+(rect.height-16)*.5f,16,16),row.componentIcons[i].image,ScaleMode.ScaleToFit);
                        GUI.Label(r,row.componentTooltips[i],GUIStyle.none);
                        if (prefs.componentEditor && e.type == EventType.MouseDown && e.button==0 && e.alt && r.Contains(e.mousePosition))
                        {
                            if (row.components[i]) ComponentPopup.Open(r,row.components[i],Targets(row.go));
                            else EditorGUIUtility.PingObject(row.go); e.Use();
                        }
                    }
                }
                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1) && rect.Contains(e.mousePosition) && e.alt) { PalettePopup.Open(rect,Targets(row.go)); e.Use(); }
            }
        }
        public static GameObject[] Targets(GameObject go)
        {
            var selection = Selection.gameObjects;
            foreach(var item in selection) if(item == go) return selection;
            return new[] {go};
        }
        static void GlobalInput()
        {
            var e = Event.current; if (e == null || !ToolkitEvents.Enabled) return;
            var window = EditorWindow.focusedWindow;
            if (!HierarchyCompatibility.IsHierarchy(window) || EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0 || HierarchyCompatibility.IsRenaming(window)) return;
            if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && e.commandName == "Duplicate")
            {
                duplicateSources = Selection.gameObjects;
                EditorApplication.delayCall += CopyDuplicated; return;
            }
            if (e.type != EventType.KeyDown || !Preferences.instance.shortcuts || EditorWindow.mouseOverWindow != window) return;
            foreach(var binding in Preferences.instance.bindings)
            {
                if (!binding.Matches(e) || binding.key == KeyCode.None) continue;
                if (binding.action == "Collapse all") { HierarchyCompatibility.Collapse(window); e.Use(); return; }
                hovered = HierarchyCompatibility.Hovered(window);
                if (!hovered) return;
                switch(binding.action)
                {
                    case "Default parent": if (!HierarchyCompatibility.CanDefaultParent) return; HierarchyCompatibility.SetDefault(window, hovered); break;
                    case "Toggle active": if (!Preferences.instance.activation) return; ToggleActive(hovered); break;
                    case "Focus": Selection.activeGameObject=hovered; if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected(); break;
                    case "Delete": Undo.DestroyObjectImmediate(hovered); break;
                    case "Expand recursive": HierarchyCompatibility.Toggle(window,hovered,true); break;
                    case "Expand only": HierarchyCompatibility.Isolate(window,hovered); break;
                    default: return;
                }
                e.Use(); return;
            }
        }
        static void CopyDuplicated()
        {
            var targets=Selection.gameObjects;if(duplicateSources==null || targets.Length!=duplicateSources.Length)return;
            int group=Undo.GetCurrentGroup();
            for(int i=0;i<targets.Length;i++)if(targets[i]!=duplicateSources[i])Toolkit.CopyAppearance(duplicateSources[i],targets[i]);
            Undo.CollapseUndoOperations(group);duplicateSources=null;
        }
    }
}
