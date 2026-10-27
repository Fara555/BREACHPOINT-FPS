using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HierarchyToolkit
{
    public static class Toolkit
    {
        public static void SetColor(GameObject target,Color color)=>DecorationStore.instance.Edit(new[]{target},r=>{r.color=color;r.hasColor=true;});
        public static void SetColor(GameObject target,int paletteIndex)
        {
            var colors=ProjectSettings.instance.palette.colors;if(paletteIndex<0||paletteIndex>=colors.Count)throw new ArgumentOutOfRangeException(nameof(paletteIndex));SetColor(target,colors[paletteIndex]);
        }
        public static void SetIcon(GameObject target,string icon)=>DecorationStore.instance.Edit(new[]{target},r=>r.icon=icon);
        public static void SetBookmark(GameObject target,bool bookmarked)=>DecorationStore.instance.Edit(new[]{target},r=>r.bookmark=bookmarked);
        public static void Reset(GameObject target)=>DecorationStore.instance.Edit(new[]{target},r=>{r.hasColor=false;r.icon=null;r.kind=DecorationKind.Normal;});
        public static void CopyAppearance(GameObject source,GameObject duplicate,bool recursive=true)
        {
            if(!source || !duplicate || source==duplicate)return;
            var targets=new List<GameObject>();var snapshots=new List<Decoration>();var pending=new Stack<(Transform source,Transform target)>();pending.Push((source.transform,duplicate.transform));
            while(pending.Count>0)
            {
                var pair=pending.Pop();var saved=DecorationStore.instance.For(pair.source.gameObject);
                if(saved!=null){targets.Add(pair.target.gameObject);snapshots.Add(saved.Clone(null));}
                if(recursive)for(int i=Math.Min(pair.source.childCount,pair.target.childCount)-1;i>=0;i--)pending.Push((pair.source.GetChild(i),pair.target.GetChild(i)));
            }
            int next=0;DecorationStore.instance.Edit(targets.ToArray(),r=>{var original=snapshots[next++];r.color=original.color;r.hasColor=original.hasColor;r.icon=original.icon;r.kind=original.kind;},"Duplicate hierarchy appearance");
        }
        public static void RefreshRules()=>ToolkitEvents.Notify();
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HierarchyRuleAttribute : Attribute { public int priority; public HierarchyRuleAttribute(int priority=0){this.priority=priority;} }
    public sealed class RuleContext
    {
        public GameObject gameObject;
        public Color? color;
        public string icon;
        public DecorationKind? kind;
    }
    public static class RuleEngine
    {
        sealed class Rule {public string name;public int priority;public Action<RuleContext> action;public bool failed;}
        static readonly List<Rule> Rules=new List<Rule>();
        static RuleEngine()
        {
            foreach(var method in TypeCache.GetMethodsWithAttribute<HierarchyRuleAttribute>())
            {
                if(!method.IsStatic || method.ReturnType!=typeof(void) || method.GetParameters().Length!=1 || method.GetParameters()[0].ParameterType!=typeof(RuleContext)) {Debug.LogWarning("HierarchyToolkit: rule signature must be static void Method(RuleContext): "+method);continue;}
                try{Rules.Add(new Rule{name=method.DeclaringType.FullName+"."+method.Name,priority=method.GetCustomAttribute<HierarchyRuleAttribute>().priority,action=(Action<RuleContext>)Delegate.CreateDelegate(typeof(Action<RuleContext>),method)});}
                catch(Exception e){Debug.LogWarning("HierarchyToolkit: invalid rule "+method.Name+": "+e.Message);}
            }
            Rules.Sort((a,b)=>a.priority!=b.priority?a.priority.CompareTo(b.priority):string.CompareOrdinal(a.name,b.name));
        }
        public static Decoration Evaluate(GameObject go,Decoration saved)
        {
            if(!Preferences.instance.rules || Rules.Count==0)return saved;
            var context=new RuleContext{gameObject=go};
            foreach(var rule in Rules)
            {
                if(rule.failed)continue;
                try{rule.action(context);}catch(Exception e){rule.failed=true;Debug.LogWarning("HierarchyToolkit: rule "+rule.name+" disabled until reload: "+e.Message);}
            }
            var result=saved?.Clone(saved.id)??new Decoration();
            if(!result.hasColor&&context.color.HasValue){result.color=context.color.Value;result.hasColor=true;}
            if(string.IsNullOrEmpty(result.icon))result.icon=context.icon;
            if(result.kind==DecorationKind.Normal&&context.kind.HasValue)result.kind=context.kind.Value;
            return result;
        }
    }
}
