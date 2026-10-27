using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HierarchyToolkit
{
    public static class Toolbar
    {
        static readonly GUIContent Scenes = new GUIContent("Scenes", "Find or bookmark scenes");
        static readonly GUIContent Books = new GUIContent("Bookmarks", "Reveal objects • drag objects here to bookmark");
        static readonly GUIContent Collapse = new GUIContent("↑", "Collapse all branches in this Hierarchy");
        static readonly GUIContent Settings = new GUIContent("⚙", "HierarchyToolkit settings");
        static readonly List<Decoration> Bookmarks = new List<Decoration>();
        static readonly List<GUIContent> Labels = new List<GUIContent>();
        static bool dirty=true;
        static string dragId;
        static Vector2 dragStart;
        static Toolbar(){ToolkitEvents.Changed+=()=>dirty=true;EditorApplication.hierarchyChanged+=()=>dirty=true;}
        static void Rebuild()
        {
            dirty=false;Bookmarks.Clear();Labels.Clear();
            foreach(var r in DecorationStore.instance.records)if(r.bookmark){Bookmarks.Add(r);Labels.Add(new GUIContent(r.label??"Object","Click to reveal • drag away to unbookmark"));}
        }
        public static void Draw(EditorWindow window)
        {
            if (!ToolkitEvents.Enabled) return;
            if(dirty)Rebuild();
            var p = Preferences.instance; var e=Event.current;
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (p.sceneSelector && GUILayout.Button(Scenes,EditorStyles.toolbarButton,GUILayout.Width(55))) PopupWindow.Show(GUILayoutUtility.GetLastRect(),new ScenePopup());
            if (p.navigation)
            {
                if (GUILayout.Button(Books,EditorStyles.toolbarButton,GUILayout.Width(83))) PopupWindow.Show(GUILayoutUtility.GetLastRect(),new BookmarkPopup(window));
                using(new EditorGUI.DisabledScope(!HierarchyCompatibility.CanExpand)) if (GUILayout.Button(Collapse,EditorStyles.toolbarButton,GUILayout.Width(24))) HierarchyCompatibility.Collapse(window);
                int visible=Mathf.Max(0,(int)((window.position.width-205)/70));
                for(int i=0;i<Bookmarks.Count && i<visible;i++)
                {
                    var bookmark=Bookmarks[i];
                    if(GUILayout.Button(Labels[i],EditorStyles.toolbarButton,GUILayout.MaxWidth(90)))BookmarkNavigation.Navigate(bookmark,window);
                    var r=GUILayoutUtility.GetLastRect();
                    if(e.type==EventType.MouseDown && r.Contains(e.mousePosition)){dragId=bookmark.id;dragStart=e.mousePosition;}
                    if(e.type==EventType.MouseDrag && dragId==bookmark.id && (e.mousePosition-dragStart).sqrMagnitude>16)
                    {
                        var go=StableIdentity.Resolve(dragId);if(go){DragAndDrop.PrepareStartDrag();DragAndDrop.objectReferences=new UnityEngine.Object[]{go};DragAndDrop.SetGenericData("HierarchyToolkit.Bookmark",dragId);DragAndDrop.StartDrag(go.name);e.Use();}
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Settings,EditorStyles.toolbarButton,GUILayout.Width(24))) ToolkitSettingsWindow.Open();
            GUILayout.EndHorizontal();
            var area=new Rect(0,0,window.position.width,23);
            if(p.navigation && area.Contains(e.mousePosition) && (e.type==EventType.DragUpdated || e.type==EventType.DragPerform))
            {
                bool valid=false;foreach(var obj in DragAndDrop.objectReferences)if(obj is GameObject){valid=true;break;}
                if(valid){DragAndDrop.visualMode=DragAndDropVisualMode.Link;if(e.type==EventType.DragPerform){DragAndDrop.AcceptDrag();var objects=new List<GameObject>();foreach(var obj in DragAndDrop.objectReferences)if(obj is GameObject go)objects.Add(go);DecorationStore.instance.Edit(objects.ToArray(),r=>r.bookmark=true);DragAndDrop.SetGenericData("HierarchyToolkit.Bookmark",null);dragId=null;}e.Use();}
            }
            if(e.type==EventType.DragExited && !area.Contains(e.mousePosition) && DragAndDrop.GetGenericData("HierarchyToolkit.Bookmark") is string id){DecorationStore.instance.Remove(id);DragAndDrop.SetGenericData("HierarchyToolkit.Bookmark",null);dragId=null;}
            if(e.type==EventType.MouseUp)dragId=null;
        }
    }
    public static class BookmarkNavigation
    {
        public static bool Navigate(Decoration record,EditorWindow hierarchy)
        {
            var go=StableIdentity.Resolve(record.id);
            if(!go)
            {
                var path=AssetDatabase.GUIDToAssetPath(record.sceneGuid??"");
                if(path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)){if(!SceneNavigation.Open(path,true))return false;go=StableIdentity.Resolve(record.id);}
                else if(path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)){PrefabStageUtility.OpenPrefab(path);go=StableIdentity.Resolve(record.id);}
            }
            if(go){HierarchyCompatibility.Reveal(go,hierarchy);return true;}
            if(hierarchy)hierarchy.ShowNotification(new GUIContent("Object was deleted or its asset is missing."));return false;
        }
    }
    public sealed class PalettePopup : PopupWindowContent
    {
        GameObject[] targets;
        Color custom = new Color(.3f,.7f,.65f);
        Vector2 scroll;
        public static void Open(Rect rect,GameObject[] targets) => PopupWindow.Show(rect,new PalettePopup {targets=targets});
        public override Vector2 GetWindowSize() => new Vector2(300,360);
        void Apply(Action<Decoration> action)
        {
            if(!Event.current.alt){DecorationStore.instance.Edit(targets,action);return;}
            var expanded=new HashSet<GameObject>();foreach(var go in targets)if(go)foreach(var t in go.GetComponentsInChildren<Transform>(true))expanded.Add(t.gameObject);
            var all=new GameObject[expanded.Count];expanded.CopyTo(all);DecorationStore.instance.Edit(all,action,"Recursive hierarchy decoration");
        }
        public override void OnGUI(Rect rect)
        {
            var p = Preferences.instance;
            GUILayout.Label("Object appearance",EditorStyles.boldLabel);
            GUILayout.Label(targets.Length + " object(s) • changes support Undo",EditorStyles.miniLabel);
            scroll = GUILayout.BeginScrollView(scroll);
            if (p.colors)
            {
                GUILayout.Space(6); GUILayout.Label("Color (hold Alt to apply recursively)",EditorStyles.boldLabel);
                GUILayout.BeginHorizontal(); int column=0;
                foreach(var c in ProjectSettings.instance.palette.colors)
                {
                    var old=GUI.backgroundColor; GUI.backgroundColor=c;
                    if(GUILayout.Button(" ",GUILayout.Width(28),GUILayout.Height(24))) {var chosen=c;Apply(r=>{r.hasColor=true;r.color=chosen;});} GUI.backgroundColor=old;
                    if(++column % 8==0) {GUILayout.EndHorizontal();GUILayout.BeginHorizontal();}
                }
                GUILayout.EndHorizontal(); custom=EditorGUILayout.ColorField("Custom",custom);
                GUILayout.BeginHorizontal(); if(GUILayout.Button("Apply color")) Apply(r=>{r.hasColor=true;r.color=custom;});
                if(GUILayout.Button("Clear color")) Apply(r=>r.hasColor=false); GUILayout.EndHorizontal();
            }
            if(p.icons)
            {
                GUILayout.Space(6);GUILayout.Label("Icon",EditorStyles.boldLabel);
                bool firstRow=true;var currentIcon=targets.Length>0?ToolkitIcons.ResolveKey(DecorationStore.instance.For(targets[0])?.icon):null;
                foreach(var group in ToolkitIcons.Groups)
                {
                    GUILayout.BeginHorizontal();
                    if(firstRow){if(GUILayout.Button(new GUIContent("×","Clear icon"),GUIStyle.none,GUILayout.Width(24),GUILayout.Height(24)))Apply(r=>{r.icon=null;r.kind=DecorationKind.Normal;});firstRow=false;}
                    foreach(var entry in group)
                    {
                        if(!entry.texture)continue;
                        var cell=GUILayoutUtility.GetRect(24,24,GUILayout.Width(24),GUILayout.Height(24));
                        bool selected=currentIcon==entry.key;
                        if(selected && Event.current.type==EventType.Repaint)EditorGUI.DrawRect(cell,new Color(.25f,.47f,.66f));
                        if(GUI.Button(cell,entry.tooltip,GUIStyle.none)){var chosen=entry.key;Apply(r=>r.icon=chosen);}
                        if(Event.current.type==EventType.Repaint)GUI.DrawTexture(new Rect(cell.x+4,cell.y+4,16,16),entry.texture,ScaleMode.ScaleToFit);
                    }
                    GUILayout.EndHorizontal();
                }
                if(GUILayout.Button(new GUIContent("+","Add custom icons in settings"),GUILayout.Width(24),GUILayout.Height(24)))ToolkitSettingsWindow.Open();
                foreach(var entry in ProjectSettings.instance.palette.icons)
                {
                    var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(entry.guid));
                    using(new EditorGUI.DisabledScope(!tex)) if(GUILayout.Button(new GUIContent(entry.name,tex))) {var chosen="asset:"+entry.guid;Apply(r=>r.icon=chosen);}
                }
                if(GUILayout.Button("Clear icon")) Apply(r=>r.icon=null);
            }
            if(p.folders)
            {
                GUILayout.Space(6);GUILayout.BeginHorizontal();
                if(p.folders && GUILayout.Button("Folder")) Apply(r=>r.kind=DecorationKind.Folder);
                if(GUILayout.Button("Normal")) Apply(r=>r.kind=DecorationKind.Normal);GUILayout.EndHorizontal();
            }
            if(p.navigation)
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button("Bookmark")) Apply(r=>r.bookmark=true);
                if(GUILayout.Button("Unbookmark")) Apply(r=>r.bookmark=false);GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);if(GUILayout.Button("Reset appearance")) Apply(r=>{r.hasColor=false;r.icon=null;r.kind=DecorationKind.Normal;});
            GUILayout.EndScrollView();
        }
    }
    public sealed class ComponentPopup : PopupWindowContent
    {
        UnityEditor.Editor editor;
        UnityEngine.Object[] targets;
        Vector2 scroll;
        public static void Open(Rect rect,Component component,GameObject[] selection)
        {
            var type=component.GetType();var objects=new List<UnityEngine.Object>();
            // Match the ordinal when a GameObject contains repeated component types.
            var siblings=component.gameObject.GetComponents(type);int ordinal=Array.IndexOf(siblings,component);
            foreach(var go in selection) {if(!go)continue;var matches=go.GetComponents(type);if(ordinal<matches.Length)objects.Add(matches[ordinal]);}
            if(objects.Count==0)objects.Add(component);
            PopupWindow.Show(rect,new ComponentPopup {targets=objects.ToArray()});
        }
        public override Vector2 GetWindowSize()=>new Vector2(380,420);
        public override void OnOpen(){ editor=UnityEditor.Editor.CreateEditor(targets); }
        public override void OnClose(){if(editor)UnityEngine.Object.DestroyImmediate(editor);}
        public override void OnGUI(Rect rect)
        {
            if(!editor || !editor.target){GUILayout.Label("Component no longer exists.");return;}
            GUILayout.Label(editor.target.GetType().Name,EditorStyles.boldLabel);GUILayout.Label(targets.Length+" component(s)",EditorStyles.miniLabel);
            scroll=GUILayout.BeginScrollView(scroll);
            try
            {
                // Native/custom Editor owns SerializedObject, Undo and prefab property handling.
                editor.serializedObject.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();editor.OnInspectorGUI();
                if(EditorGUI.EndChangeCheck())
                {
                    editor.serializedObject.ApplyModifiedProperties();
                    foreach(var target in targets) if(target && PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    ToolkitEvents.Notify();
                }
            }
            catch(ExitGUIException){throw;}
            catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Warning);if(GUILayout.Button("Open native Inspector")){Selection.objects=targets;editorWindow.Close();}}
            GUILayout.EndScrollView();
        }
    }
    public sealed class ScenePopup : PopupWindowContent
    {
        struct Entry {public string guid,path,name;}
        readonly List<Entry> scenes=new List<Entry>();
        string query="";Vector2 scroll;
        public override Vector2 GetWindowSize()=>new Vector2(420,420);
        public override void OnOpen()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets"})){var path=AssetDatabase.GUIDToAssetPath(guid);scenes.Add(new Entry {guid=guid,path=path,name=Path.GetFileNameWithoutExtension(path)});}
            scenes.Sort((a,b)=>string.Compare(a.path,b.path,StringComparison.OrdinalIgnoreCase));
        }
        public override void OnGUI(Rect rect)
        {
            GUILayout.Label("Scenes",EditorStyles.boldLabel);query=EditorGUILayout.TextField("Search",query);
            EditorGUILayout.HelpBox("Open replaces loaded scenes after Unity's save prompt. Add loads alongside them. Active changes the target for new objects.",MessageType.None);
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(var entry in scenes)
            {
                if(entry.path.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                bool bookmarked=DecorationStore.instance.sceneBookmarks.Contains(entry.guid);
                GUILayout.BeginHorizontal();if(GUILayout.Button(bookmarked?"★":"☆",GUILayout.Width(24)))DecorationStore.instance.ToggleScene(entry.guid);
                GUILayout.Label(new GUIContent(entry.name,entry.path),GUILayout.MinWidth(90));
                if(GUILayout.Button("Open",GUILayout.Width(42))){SceneNavigation.Open(entry.path,false);editorWindow.Close();}
                var loaded=SceneManager.GetSceneByPath(entry.path);
                if(!loaded.isLoaded){if(GUILayout.Button("Add",GUILayout.Width(36))){SceneNavigation.Open(entry.path,true);editorWindow.Close();}}
                else if(GUILayout.Button("Active",GUILayout.Width(45)))SceneManager.SetActiveScene(loaded);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);GUILayout.Label("Saved scene bookmarks",EditorStyles.boldLabel);
            foreach(var guid in new List<string>(DecorationStore.instance.sceneBookmarks))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);if(!SceneNavigation.IsProjectScene(path))continue;GUILayout.BeginHorizontal();
                if(GUILayout.Button(string.IsNullOrEmpty(path)?"Missing scene":path,EditorStyles.linkLabel) && !string.IsNullOrEmpty(path)){SceneNavigation.Open(path,false);editorWindow.Close();}
                if(GUILayout.Button("×",GUILayout.Width(24)))DecorationStore.instance.ToggleScene(guid);GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
    }
    public static class SceneNavigation
    {
        public static bool IsProjectScene(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/",StringComparison.Ordinal) && path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase);
        public static bool Open(string path,bool additive)
        {
            if(!IsProjectScene(path)||EditorApplication.isPlayingOrWillChangePlaymode)return false;
            if(!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return false;
            if(additive && SceneManager.GetSceneByPath(path).isLoaded)return true;
            EditorSceneManager.OpenScene(path,additive?OpenSceneMode.Additive:OpenSceneMode.Single);return true;
        }
    }
    public sealed class BookmarkPopup : PopupWindowContent
    {
        readonly EditorWindow hierarchy;Vector2 scroll;string query="";
        public BookmarkPopup(EditorWindow hierarchy){this.hierarchy=hierarchy;}
        public override Vector2 GetWindowSize()=>new Vector2(380,350);
        public override void OnGUI(Rect rect)
        {
            GUILayout.Label("Object bookmarks",EditorStyles.boldLabel);query=EditorGUILayout.TextField("Search",query);
            if(GUILayout.Button("Bookmark selection"))DecorationStore.instance.Edit(Selection.gameObjects,r=>r.bookmark=true);
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(var record in DecorationStore.instance.records)
            {
                if(!record.bookmark || (record.label??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                GUILayout.BeginHorizontal();
                if(GUILayout.Button(record.label??"Object",EditorStyles.linkLabel))Navigate(record);
                if(GUILayout.Button("×",GUILayout.Width(24))){DecorationStore.instance.Remove(record.id);GUIUtility.ExitGUI();}
                GUILayout.EndHorizontal();
            }
            foreach(var entry in SessionDecorations.Instance.entries)
            {
                if(entry.stableId!=null || !entry.target || entry.temporary==null || !entry.temporary.bookmark)continue;
                GUILayout.BeginHorizontal();
                if(GUILayout.Button(entry.target.name+" (unsaved)",EditorStyles.linkLabel)){HierarchyCompatibility.Reveal(entry.target,hierarchy);editorWindow.Close();}
                if(GUILayout.Button("×",GUILayout.Width(24))){DecorationStore.instance.Edit(new[]{entry.target},r=>r.bookmark=false);GUIUtility.ExitGUI();}
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();EditorGUILayout.HelpBox("Bookmarks retain their IDs when scenes close. Missing objects stay listed until removed. Unsaved objects are promoted after saving.",MessageType.None);
        }
        void Navigate(Decoration r) { if(BookmarkNavigation.Navigate(r,hierarchy))editorWindow.Close(); }
    }
    public sealed class ToolkitSettingsWindow : EditorWindow
    {
        Vector2 scroll;int tab;Texture2D customIcon;string iconName="Custom icon";
        [MenuItem("Tools/HierarchyToolkit/Settings")]
        public static void Open(){var w=GetWindow<ToolkitSettingsWindow>();w.titleContent=new GUIContent("HierarchyToolkit");w.minSize=new Vector2(360,350);}
        [MenuItem("Tools/HierarchyToolkit/Enabled")]
        static void Toggle(){Preferences.instance.enabled=!Preferences.instance.enabled;Preferences.instance.Commit();}
        [MenuItem("Tools/HierarchyToolkit/Enabled",true)]
        static bool ValidateToggle(){Menu.SetChecked("Tools/HierarchyToolkit/Enabled",ToolkitEvents.Enabled);return true;}
        [MenuItem("GameObject/HierarchyToolkit/Appearance",false,30)]
        static void Appearance(){if(Selection.gameObjects.Length>0)PalettePopup.Open(new Rect(120,100,1,1),Selection.gameObjects);}

        void OnGUI()
        {
            GUILayout.Label("HierarchyToolkit",EditorStyles.largeLabel);GUILayout.Label("Make the hierarchy yours",EditorStyles.miniLabel);
            var p=Preferences.instance;var project=ProjectSettings.instance;
            EditorGUI.BeginChangeCheck();p.enabled=EditorGUILayout.Toggle("Enabled for me",p.enabled);if(EditorGUI.EndChangeCheck())p.Commit();
            EditorGUI.BeginChangeCheck();project.enabled=EditorGUILayout.Toggle("Enabled for project",project.enabled);if(EditorGUI.EndChangeCheck())project.Commit();
            tab=GUILayout.Toolbar(tab,new[]{"Appearance","Workflow","Palette","Keys"});scroll=GUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            if(tab==0)
            {
                p.colors=EditorGUILayout.Toggle("Object colors",p.colors);p.icons=EditorGUILayout.Toggle("Object icons",p.icons);
                p.folders=EditorGUILayout.Toggle("Visual folders",p.folders);
                p.lines=EditorGUILayout.Toggle("Hierarchy lines",p.lines);p.zebra=EditorGUILayout.Toggle("Alternating rows",p.zebra);
                p.minimal=EditorGUILayout.Toggle("Minimal mode",p.minimal);p.tint=EditorGUILayout.Slider("Color intensity",p.tint,.03f,.5f);
            }
            else if(tab==1)
            {
                p.sceneSelector=EditorGUILayout.Toggle("Scene selector",p.sceneSelector);p.navigation=EditorGUILayout.Toggle("Object navigation",p.navigation);
                p.componentMap=EditorGUILayout.Toggle("Component minimap",p.componentMap);p.componentEditor=EditorGUILayout.Toggle("Alt-click editor",p.componentEditor);
                p.maxComponents=EditorGUILayout.IntSlider("Visible components",p.maxComponents,1,12);p.activation=EditorGUILayout.Toggle("Activity switch",p.activation);
                p.shortcuts=EditorGUILayout.Toggle("Hover keyboard actions",p.shortcuts);
                p.rules=EditorGUILayout.Toggle("Automatic rules",p.rules);
                EditorGUILayout.HelpBox("Alt-click a row opens its appearance palette. Native right-click, renaming, selection and dragging remain available.",MessageType.Info);
                EditorGUILayout.LabelField("Expansion API",HierarchyCompatibility.CanExpand?"Available":"Unavailable; native foldouts still work");
                EditorGUILayout.LabelField("Default parent API",HierarchyCompatibility.CanDefaultParent?"Available":"Unavailable; use Unity context menu");
            }
            else if(tab==3)
            {
                foreach(var key in p.bindings)
                {
                    GUILayout.Label(key.action,EditorStyles.boldLabel);key.key=(KeyCode)EditorGUILayout.EnumPopup("Key (None disables)",key.key);
                    GUILayout.BeginHorizontal();key.shift=GUILayout.Toggle(key.shift,"Shift");key.primary=GUILayout.Toggle(key.primary,"Ctrl / Cmd");key.alt=GUILayout.Toggle(key.alt,"Alt / Option");GUILayout.EndHorizontal();
                }
                for(int i=0;i<p.bindings.Count;i++)for(int j=i+1;j<p.bindings.Count;j++){var a=p.bindings[i];var b=p.bindings[j];if(a.key!=KeyCode.None&&a.key==b.key&&a.shift==b.shift&&a.primary==b.primary&&a.alt==b.alt)EditorGUILayout.HelpBox("Conflict: "+a.action+" / "+b.action+". First action takes precedence.",MessageType.Warning);}
            }
            if(EditorGUI.EndChangeCheck())p.Commit();
            if(tab==2)DrawPalette(project);
            GUILayout.Space(12);if(GUILayout.Button("Reset my preferences"))p.Reset();
            if(GUILayout.Button("Reset project palette")){project.palette=new Palette();project.Commit();}
            GUILayout.EndScrollView();
        }
        void DrawPalette(ProjectSettings project)
        {
            EditorGUI.BeginChangeCheck();
            var palette=project.palette;
            for(int i=0;i<palette.colors.Count;i++) {GUILayout.BeginHorizontal();palette.colors[i]=EditorGUILayout.ColorField(palette.colors[i]);if(GUILayout.Button("×",GUILayout.Width(24))){palette.colors.RemoveAt(i);GUI.changed=true; i--;}GUILayout.EndHorizontal();}
            if(palette.colors.Count<256 && GUILayout.Button("Add color")){palette.colors.Add(Color.white);GUI.changed=true;}
            for(int i=0;i<palette.icons.Count;i++)
            {
                var entry=palette.icons[i];GUILayout.BeginHorizontal();entry.name=EditorGUILayout.TextField(entry.name);
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(entry.guid));GUILayout.Label(tex?tex.name:"Missing resource",GUILayout.Width(110));
                if(GUILayout.Button("×",GUILayout.Width(24))){palette.icons.RemoveAt(i);GUI.changed=true;i--;}GUILayout.EndHorizontal();
            }
            if(EditorGUI.EndChangeCheck())project.Commit();
            GUILayout.Space(8);customIcon=(Texture2D)EditorGUILayout.ObjectField("Custom icon",customIcon,typeof(Texture2D),false);iconName=EditorGUILayout.TextField("Name",iconName);
            using(new EditorGUI.DisabledScope(!customIcon || string.IsNullOrWhiteSpace(iconName)))if(GUILayout.Button("Add custom icon")){palette.icons.Add(new IconEntry{name=iconName,guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(customIcon))});project.Commit();}
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Export JSON")){var path=EditorUtility.SaveFilePanel("Export palette","","HierarchyToolkit-palette","json");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,palette.ToJson());}
            if(GUILayout.Button("Import JSON"))
            {
                var path=EditorUtility.OpenFilePanel("Import palette","","json");
                if(!string.IsNullOrEmpty(path))try {var imported=Palette.Parse(File.ReadAllText(path));project.palette=imported;project.Commit();}catch(Exception e){EditorUtility.DisplayDialog("Palette not imported",e.Message,"OK");}
            }
            GUILayout.EndHorizontal();EditorGUILayout.HelpBox("JSON carries icon GUID references. Transfer the texture assets with their .meta files too; unresolved resources appear as Missing resource.",MessageType.Info);
        }
    }
}
