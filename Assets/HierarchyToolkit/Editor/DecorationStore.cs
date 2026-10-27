using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HierarchyToolkit
{
    [FilePath("ProjectSettings/HierarchyToolkitData.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class DecorationStore : ScriptableSingleton<DecorationStore>
    {
        public List<Decoration> records = new List<Decoration>();
        public List<string> sceneBookmarks = new List<string>();
        [NonSerialized] Dictionary<string, Decoration> index;
        public void Invalidate() { index = null; }
        public Decoration Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (index == null) { index = new Dictionary<string, Decoration>(); foreach (var r in records) if (r != null && !string.IsNullOrEmpty(r.id)) index[r.id] = r; }
            return index.TryGetValue(id, out var value) ? value : null;
        }
        public Decoration For(GameObject go)
        {
            if(!go)return null;
            var entry=SessionDecorations.Instance.Find(go);
            if(entry!=null)return entry.stableId==null?entry.temporary:Get(entry.stableId);
            return Get(StableIdentity.For(go));
        }
        public void Edit(GameObject[] objects, Action<Decoration> change, string undo = "Hierarchy decoration")
        {
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{this,SessionDecorations.Instance}, undo);
            foreach (var go in objects)
            {
                if (!go) continue;
                string id = StableIdentity.For(go);
                var record = For(go)?.Clone(id) ?? new Decoration { id = id };
                record.label = go.name; record.sceneGuid = StableIdentity.SceneGuid(go); change(record);
                var entry=SessionDecorations.Instance.Find(go);
                if(entry==null){entry=new SessionEntry{target=go};SessionDecorations.Instance.entries.Add(entry);}
                entry.stableId=id;entry.temporary=id==null?record:null;
                if(id!=null)Upsert(record);
            }
            Commit();
        }
        void Upsert(Decoration r) { for (int i = 0; i < records.Count; i++) if (records[i].id == r.id) { records[i] = r; Invalidate(); return; } records.Add(r); Invalidate(); }
        public void Reconcile()
        {
            bool changed=false;
            foreach(var entry in SessionDecorations.Instance.entries)
            {
                if(!entry.target)continue;
                string id=StableIdentity.For(entry.target);
                if(id==null || id==entry.stableId)continue;
                var source=entry.stableId==null?entry.temporary:Get(entry.stableId);
                if(source==null)continue;
                if(!changed)Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{this,SessionDecorations.Instance},"Migrate hierarchy data");
                changed=true;
                if(entry.stableId!=null)records.RemoveAll(r=>r.id==entry.stableId);
                var record=source.Clone(id);record.sceneGuid=StableIdentity.SceneGuid(entry.target);record.label=entry.target.name;
                entry.stableId=id;entry.temporary=null;Upsert(record);
            }
            if(changed)Commit();
        }
        public void Track(GameObject go, Decoration record)
        {
            if(!go || record==null || SessionDecorations.Instance.Find(go)!=null)return;
            SessionDecorations.Instance.entries.Add(new SessionEntry{target=go,stableId=record.id,temporary=record.id==null?record:null});
        }
        public void AfterUndo() { SessionDecorations.Instance.Invalidate(); Invalidate(); Save(true); ToolkitEvents.Notify(); }
        public void ToggleScene(string guid) { Undo.RegisterCompleteObjectUndo(this, "Scene bookmark"); if (!sceneBookmarks.Remove(guid)) sceneBookmarks.Add(guid); Commit(); }
        public void Remove(string id) { Undo.RegisterCompleteObjectUndo(this, "Remove bookmark"); var r = Get(id); if (r != null) r.bookmark = false; Commit(); }
        public void Commit() { Invalidate(); Save(true); ToolkitEvents.Notify(); }
    }

}
