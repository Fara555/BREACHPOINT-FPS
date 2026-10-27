using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyToolkit
{
    public static class HierarchyCompatibility
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type WindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        static readonly PropertyInfo HierarchyProperty = WindowType?.GetProperty("sceneHierarchy", Flags);
        static readonly Type HierarchyType = HierarchyProperty?.PropertyType;
        static readonly MethodInfo Expand = Find("ExpandTreeViewItem",2);
        static readonly MethodInfo Recursive = Find("SetExpandedRecursive",2);
        static readonly MethodInfo ExpandedIds = Find("GetExpandedIDs",0);
        static readonly MethodInfo Rename = Find("IsRenaming",0);
        static readonly MethodInfo DefaultParent = Find("SetDefaultParentObject",2);
        static readonly FieldInfo GlobalHandler = typeof(EditorApplication).GetField("globalEventHandler", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo FileHint=typeof(Unsupported).GetMethod("GetFileIDHint",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.Object)},null);
        public static ulong PrefabFileHint(GameObject go)
        {
            if(FileHint==null)return 0;try{return (ulong)FileHint.Invoke(null,new object[]{go});}catch{return 0;}
        }
        static readonly PropertyInfo Hover = WindowType?.GetProperty("hoveredObject",Flags);
        static readonly HashSet<string> Failed = new HashSet<string>();
        static readonly PropertyInfo Tree = HierarchyType?.GetProperty("treeView",Flags);
        static readonly PropertyInfo Gui = Tree?.PropertyType.GetProperty("gui",Flags);
        static readonly Type GuiType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameObjectTreeViewGUI");
        static readonly FieldInfo IconWidth = GuiType?.GetField("k_IconWidth",Flags);
        static readonly FieldInfo BottomMargin = GuiType?.GetField("k_BottomRowMargin",Flags);
        sealed class Layout { public object gui;public float iconWidth,bottomMargin; }
        static readonly Dictionary<EditorWindow,Layout> Layouts=new Dictionary<EditorWindow,Layout>();
        static readonly Type ViewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
        static readonly PropertyInfo CurrentView=ViewType?.GetProperty("current",Flags);
        static readonly Type HostType=typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
        static readonly PropertyInfo ActualView=HostType?.GetProperty("actualView",Flags);
        static readonly MethodInfo ResetItemIcon=GuiType?.GetMethod("SetItemIcon",Flags);
        static readonly MethodInfo ResetSelectedIcon=GuiType?.GetMethod("SetItemSelectedIcon",Flags);
        sealed class IconBinding
        {
            public object data,gui; public MethodInfo row,item; public PropertyInfo icon,objectReference,selected;
            public readonly Dictionary<object,GameObject> changed=new Dictionary<object,GameObject>();
        }
        static readonly Dictionary<EditorWindow,IconBinding> IconBindings=new Dictionary<EditorWindow,IconBinding>();
        public static EditorWindow DrawingWindow => ActualView?.GetValue(CurrentView?.GetValue(null)) as EditorWindow;
        public static bool ReplaceRowIcon(EditorWindow window,GameObject go,Texture texture)
        {
            if(!IsHierarchy(window)||!go||!texture)return false;
            return Invoke("row icon",()=>{
                if(!IconBindings.TryGetValue(window,out var binding))
                {
                    var tree=Tree.GetValue(HierarchyProperty.GetValue(window));var data=tree.GetType().GetProperty("data",Flags).GetValue(tree);
                    var itemMethod=data.GetType().GetMethod("GetItem",Flags,null,new[]{typeof(int)},null);var itemType=itemMethod.ReturnType;
                    binding=new IconBinding{data=data,gui=Gui.GetValue(tree),row=data.GetType().GetMethod("GetRow",Flags,null,new[]{ObjectId(go).GetType()},null),item=itemMethod,icon=itemType.GetProperty("icon",Flags)};
                    IconBindings[window]=binding;
                }
                int index=(int)binding.row.Invoke(binding.data,new[]{ObjectId(go)});if(index<0)return;
                var item=binding.item.Invoke(binding.data,new object[]{index});if(item==null)return;
                if(binding.objectReference==null){binding.objectReference=item.GetType().GetProperty("objectPPTR",Flags);binding.selected=item.GetType().GetProperty("selectedIcon",Flags);}
                if(binding.objectReference.GetValue(item) as GameObject != go)return;
                if(ReferenceEquals(binding.icon.GetValue(item),texture) && ReferenceEquals(binding.selected?.GetValue(item),texture))return;
                binding.changed[item]=go;binding.icon.SetValue(item,texture);binding.selected?.SetValue(item,texture);window.Repaint();
            });
        }
        public static void RestoreRowIcons()
        {
            foreach(var pair in IconBindings)foreach(var item in pair.Value.changed)
                if(item.Value && pair.Value.objectReference.GetValue(item.Key) as GameObject == item.Value)
                {ResetItemIcon?.Invoke(pair.Value.gui,new[]{item.Key});ResetSelectedIcon?.Invoke(pair.Value.gui,new[]{item.Key});}
            IconBindings.Clear();
        }
        public static bool CanMinimal => IconWidth!=null;
        static void ConfigureLayout(EditorWindow window,bool installed)
        {
            Invoke("layout",()=>{
                if(!Layouts.TryGetValue(window,out var state)){
                    var gui=Gui?.GetValue(Tree?.GetValue(HierarchyProperty.GetValue(window)));if(gui==null)return;
                    state=new Layout{gui=gui,iconWidth=IconWidth!=null?(float)IconWidth.GetValue(gui):16,bottomMargin=BottomMargin!=null?(float)BottomMargin.GetValue(gui):0};Layouts[window]=state;
                }
                if(IconWidth!=null)IconWidth.SetValue(state.gui,installed&&Preferences.instance.minimal?0:state.iconWidth);
                if(BottomMargin!=null)BottomMargin.SetValue(state.gui,installed?state.bottomMargin+23:state.bottomMargin);
                if(!installed)Layouts.Remove(window);
            });
        }
        static MethodInfo Find(string name,int count)
        {
            if(HierarchyType==null)return null;
            foreach(var m in HierarchyType.GetMethods(Flags))if(m.Name==name&&m.GetParameters().Length==count)return m;
            return null;
        }
        static object ObjectId(GameObject go)
        {
#if UNITY_6000_4_OR_NEWER
            return go.GetEntityId();
#else
            return go.GetInstanceID();
#endif
        }
        public static bool IsHierarchy(EditorWindow w) => w && WindowType != null && WindowType.IsInstanceOfType(w);
        public static bool CanExpand => Expand != null && Recursive != null && ExpandedIds != null;
        public static bool CanDefaultParent => DefaultParent != null;
        public static GameObject Hovered(EditorWindow w) => IsHierarchy(w) && Hover!=null ? Hover.GetValue(w) as GameObject : null;
        public static bool IsRenaming(EditorWindow w)
        {
            try { return Rename==null || (bool)Rename.Invoke(HierarchyProperty.GetValue(w),null); }
            catch { return true; }
        }
        public static void Hook(EditorApplication.CallbackFunction callback, bool add)
        {
            if (GlobalHandler == null) return;
            var old = GlobalHandler.GetValue(null) as EditorApplication.CallbackFunction;
            GlobalHandler.SetValue(null, add ? Delegate.Combine(old, callback) : Delegate.Remove(old, callback));
        }
        static bool Invoke(string name, Action action)
        {
            if (Failed.Contains(name)) return false;
            try { action(); return true; }
            catch (Exception e) { Failed.Add(name); Debug.LogWarning("HierarchyToolkit: " + name + " unavailable in Unity " + Application.unityVersion + ". " + e.GetBaseException().Message); return false; }
        }
        public static void Toggle(EditorWindow window, GameObject go, bool recursive)
        {
            if (!CanExpand || !IsHierarchy(window)) return;
            Invoke("expand", () => {
                var h = HierarchyProperty.GetValue(window);var id=ObjectId(go);
                var expanded = ExpandedIds.Invoke(h, null) as Array;
                bool was=false;if(expanded!=null)foreach(var item in expanded)if(item.Equals(id)){was=true;break;}
                (recursive?Recursive:Expand).Invoke(h,new[]{id,(object)!was});window.Repaint();
            });
        }
        public static void Isolate(EditorWindow window,GameObject go)
        {
            if(!CanExpand || !go || !IsHierarchy(window))return;
            Collapse(window);
            Invoke("isolate",()=>{var h=HierarchyProperty.GetValue(window);for(var p=go.transform;p;p=p.parent)Expand.Invoke(h,new[]{ObjectId(p.gameObject),(object)true});window.Repaint();});
        }
        public static void Collapse(EditorWindow window)
        {
            if (!CanExpand || !IsHierarchy(window)) return;
            Invoke("collapse", () => {
                var h=HierarchyProperty.GetValue(window);var ids=ExpandedIds.Invoke(h,null) as Array;
                if(ids!=null)foreach(var id in ids)Recursive.Invoke(h,new[]{id,(object)false});window.Repaint();
            });
        }
        public static void Reveal(GameObject go, EditorWindow window = null)
        {
            if (!go) return;
            Selection.activeGameObject = go;
            if (!IsHierarchy(window) && WindowType!=null) window = EditorWindow.GetWindow(WindowType);
            if (Expand != null && IsHierarchy(window)) Invoke("reveal", () => { var h = HierarchyProperty.GetValue(window); for (var p = go.transform.parent; p; p = p.parent) Expand.Invoke(h, new[]{ObjectId(p.gameObject),(object)true}); });
            EditorGUIUtility.PingObject(go);
        }
        public static void SetDefault(EditorWindow window, GameObject go)
        {
            if (DefaultParent == null || !IsHierarchy(window)) return;
            if(Invoke("default parent", () => DefaultParent.Invoke(HierarchyProperty.GetValue(window), new object[] { true, go }))) window.ShowNotification(new GUIContent("Default parent toggled: " + go.name + " (bold = enabled; new objects go here)"));
        }
        public static void InstallBar(EditorWindow window)
        {
            if (!IsHierarchy(window)) return;
            ConfigureLayout(window,true);
            if(window.rootVisualElement.Q("hierarchy-toolkit-bar") != null) return;
            // Overlay is bounded to its own band. The native IMGUI hierarchy still owns all tree interactions.
            var bar = new IMGUIContainer(() => Toolbar.Draw(window)) { name = "hierarchy-toolkit-bar" };
            bar.style.position=Position.Absolute;bar.style.left=0;bar.style.right=0;bar.style.bottom=0;bar.style.height=23;
            window.rootVisualElement.Add(bar);
        }
        public static void RemoveBar(EditorWindow window) { window.rootVisualElement.Q("hierarchy-toolkit-bar")?.RemoveFromHierarchy();ConfigureLayout(window,false); }
        public static EditorWindow[] Windows()
        {
            if (WindowType == null) return Array.Empty<EditorWindow>();
            var items = Resources.FindObjectsOfTypeAll(WindowType); var result = new EditorWindow[items.Length];
            for (int i=0;i<items.Length;i++) result[i] = (EditorWindow)items[i]; return result;
        }
    }
}
