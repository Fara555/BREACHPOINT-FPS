using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HierarchyToolkit
{
    // Unity object references survive domain reload. Never serialize instance IDs to disk.
    public sealed class SessionDecorations : ScriptableObject
    {
        public List<SessionEntry> entries = new List<SessionEntry>();
        static SessionDecorations current;
        [NonSerialized] Dictionary<GameObject,SessionEntry> lookup;
        [NonSerialized] int indexedCount = -1;
        public static SessionDecorations Instance
        {
            get
            {
                if(current)return current;
                var existing=Resources.FindObjectsOfTypeAll<SessionDecorations>();
                current=existing.Length>0?existing[0]:CreateInstance<SessionDecorations>();
                current.hideFlags=HideFlags.HideAndDontSave;return current;
            }
        }
        public void Invalidate() { lookup=null; indexedCount=-1; }
        public SessionEntry Find(GameObject go)
        {
            if(lookup==null || indexedCount!=entries.Count){lookup=new Dictionary<GameObject,SessionEntry>();foreach(var e in entries)if(e.target)lookup[e.target]=e;indexedCount=entries.Count;}
            return lookup.TryGetValue(go,out var entry)?entry:null;
        }
    }

}
