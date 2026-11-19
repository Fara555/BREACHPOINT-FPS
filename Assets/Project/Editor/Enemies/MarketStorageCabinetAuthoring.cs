using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class MarketStorageCabinetAuthoring
    {
        private const string Folder=MarketCabinetMaterials.Folder;
        private const string Source="SourceArt/Props/MarketStorageCabinet";
        private const string Output="Logs/MarketStorageCabinet";
        private const string RootName="21 Props - Detailed storage cabinets";
        private const string LegacyRootName="21 Props - Detailed south storage cabinet";
        [Serializable] private sealed class MeshBinding { public string filter; public string asset; public string mesh; }
        [Serializable] private sealed class BindingList { public List<MeshBinding> items=new List<MeshBinding>(); }

        public static void Create()
        {
            RequireIdle();var scene=BattlefieldBlockout.RequireBlockout();Directory.CreateDirectory(Output);
            if(!File.Exists(Source+"/GameplayBefore.sha256.txt"))
            {
                File.WriteAllText(Source+"/GameplayBefore.sha256.txt",BattlefieldCornerArt.GameplayFingerprint(scene));File.Copy(scene.path,Output+"/scene-before.unity",false);View("before-cabinet",new Vector3(-14.8f,1.75f,36.6f),new Vector3(-16.9f,1.25f,36.6f));
            }
            MarketCabinetMaterials.Bake();string modelPath=Folder+"/Models/MarketStorageCabinet.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;importer.addCollider=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            foreach(var material in AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct())
            {
                var target=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/"+material.name+".mat");if(target==null)throw new InvalidOperationException("Missing cabinet finish: "+material.name);importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material),target);
            }
            importer.SaveAndReimport();var prefabRoot=new GameObject("PF_MarketStorageCabinet");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),prefabRoot.transform);foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);PrefabUtility.SaveAsPrefabAsset(prefabRoot,Folder+"/PF_MarketStorageCabinet.prefab");
            }
            finally{Object.DestroyImmediate(prefabRoot);}
            if(FindRoot()==null)
            {
                var root=new GameObject(RootName);root.transform.SetParent(scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>()).Single(t=>t.name=="Blockout B - Service Quarter"),false);Undo.RegisterCreatedObjectUndo(root,"Replace south cabinet with standalone model");var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PF_MarketStorageCabinet.prefab"),root.transform);instance.transform.position=new Vector3(-17.15f,0,36.6f);
            }
            var cabinets=FindRoot();Undo.RecordObject(cabinets,"Unify market storage cabinets");cabinets.name=RootName;
            if(cabinets.transform.childCount==1)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PF_MarketStorageCabinet.prefab"),cabinets.transform);instance.name="North storage cabinet";instance.transform.position=new Vector3(-17.15f,0,44.4f);Undo.RegisterCreatedObjectUndo(instance,"Replace remaining old cabinet");
            }
            ApplyReplacement();Validate();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save cabinet replacement.");Capture();
        }

        private static bool IsOldCabinet(Vector3 p)=>p.x>-17.611f&&p.x<-16.52f&&p.y>-.001f&&p.y<2.401f&&(Mathf.Abs(p.z-36.6f)<1.161f||Mathf.Abs(p.z-44.4f)<1.161f);
        internal static void ApplyReplacement()
        {
            if(FindRoot()==null)return;Directory.CreateDirectory(Output);BattlefieldCornerMaterials.EnsureFolder(Folder+"/Integration");
            string bindingPath=Source+"/OriginalMeshBindings.json";var bindings=File.Exists(bindingPath)?JsonUtility.FromJson<BindingList>(File.ReadAllText(bindingPath)):new BindingList();int removed=0;
            foreach(var target in RoomInstance().GetComponentsInChildren<MeshFilter>())
            {
                var binding=bindings.items.SingleOrDefault(b=>b.filter==target.name);var original=target.sharedMesh;
                if(binding!=null)original=AssetDatabase.LoadAllAssetsAtPath(binding.asset).OfType<Mesh>().Single(m=>m.name==binding.mesh);
                else if(AssetDatabase.GetAssetPath(original).StartsWith(Folder+"/Integration/",StringComparison.Ordinal))throw new InvalidOperationException("Cabinet original mesh binding is missing.");
                var positions=original.vertices;var filtered=Object.Instantiate(original);int localRemoved=0;
                for(int sub=0;sub<original.subMeshCount;sub++)
                {
                    int[] indices=original.GetTriangles(sub);var kept=new List<int>();
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        bool Inside(int i)=>IsOldCabinet(target.transform.TransformPoint(positions[i]));
                        if(Inside(indices[t])&&Inside(indices[t+1])&&Inside(indices[t+2])){localRemoved++;continue;}kept.Add(indices[t]);kept.Add(indices[t+1]);kept.Add(indices[t+2]);
                    }
                    filtered.SetTriangles(kept,sub,false);
                }
                if(localRemoved==0){Object.DestroyImmediate(filtered);if(binding!=null){target.sharedMesh=original;PrefabUtility.RecordPrefabInstancePropertyModifications(target);}continue;}
                removed+=localRemoved;if(removed>12000){Object.DestroyImmediate(filtered);throw new InvalidOperationException("Cabinet removal exceeded its bounded scope.");}
                if(binding==null)bindings.items.Add(new MeshBinding{filter=target.name,asset=AssetDatabase.GetAssetPath(original),mesh=original.name});
                filtered.name="CabinetFiltered_"+target.name;string path=Folder+"/Integration/"+filtered.name+".asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(stored==null){AssetDatabase.CreateAsset(filtered,path);stored=filtered;}
                else
                {
                    EditorUtility.CopySerialized(filtered,stored);
                    // Explicitly invalidate native index buffers when extending an existing filtered mesh.
                    for(int sub=0;sub<filtered.subMeshCount;sub++)stored.SetTriangles(filtered.GetTriangles(sub),sub,false);
                    stored.UploadMeshData(false);Object.DestroyImmediate(filtered);EditorUtility.SetDirty(stored);AssetDatabase.SaveAssetIfDirty(stored);
                }
                Undo.RecordObject(target,"Remove old south cabinet surfaces");target.sharedMesh=stored;PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            if(removed<500)throw new InvalidOperationException("Expected the complete old south cabinet to be removed.");
            File.WriteAllText(bindingPath,JsonUtility.ToJson(bindings,true));File.WriteAllText(Output+"/legacy-removal.txt","Both old cabinet triangle indices omitted="+removed+". All previous source vertex data, materials and other triangles retained; canonical art files unchanged.\n");
        }

        public static void Validate()
        {
            RequireIdle();var scene=BattlefieldBlockout.RequireBlockout();var root=FindRoot();
            if(root==null||root.transform.childCount!=2)throw new InvalidOperationException("Two detailed cabinets are required.");
            for(int index=0;index<2;index++)
            {
                var child=root.transform.GetChild(index);
                if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)!=Folder+"/PF_MarketStorageCabinet.prefab"||Vector3.Distance(child.position,new Vector3(-17.15f,0,index==0?36.6f:44.4f))>.001f)throw new InvalidOperationException("Cabinet prefab link or placement is invalid.");
            }
            if(BattlefieldCornerArt.GameplayFingerprint(scene)!=File.ReadAllText(Source+"/GameplayBefore.sha256.txt"))throw new InvalidOperationException("Cabinet replacement changed gameplay/collision/navigation.");
            if(root.GetComponentsInChildren<Collider>().Length!=0||root.GetComponentsInChildren<MonoBehaviour>().Length!=0||root.GetComponentsInChildren<Light>().Length!=0)throw new InvalidOperationException("Cabinet must remain passive art.");
            int triangles=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                triangles+=filter.sharedMesh.triangles.Length/3;var bounds=filter.GetComponent<MeshRenderer>().bounds;
                float centerZ=filter.transform.IsChildOf(root.transform.GetChild(0))?36.6f:44.4f;
                if(bounds.min.x<-17.63f||bounds.max.x>-16.49f||bounds.min.y<-.001f||bounds.max.y>2.411f||bounds.min.z<centerZ-1.18f||bounds.max.z>centerZ+1.18f)throw new InvalidOperationException("Cabinet escaped its existing furniture envelope.");
                foreach(var material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                {
                    if(material==null||material.shader.name!="HDRP/Lit"||!AssetDatabase.GetAssetPath(material).StartsWith(Folder+"/Materials/",StringComparison.Ordinal))throw new InvalidOperationException("Cabinet owned HDRP finish missing.");
                    var properties=material.name=="CabinetLabel"?new[]{"_BaseColorMap"}:new[]{"_BaseColorMap","_MaskMap","_NormalMap"};
                    if(material.name!="CabinetLabel"&&(!material.IsKeywordEnabled("_MASKMAP")||!material.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE")))throw new InvalidOperationException("Cabinet PBR maps inactive.");
                    foreach(string property in properties)
                    {
                        string path=AssetDatabase.GetAssetPath(material.GetTexture(property));if(!path.StartsWith(Folder+"/Textures/",StringComparison.Ordinal))throw new InvalidOperationException("Cabinet texture is not owned.");var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer.sRGBTexture!=(property=="_BaseColorMap")||!importer.mipmapEnabled||importer.filterMode!=FilterMode.Trilinear)throw new InvalidOperationException("Cabinet map import invalid.");
                    }
                }
            }
            if(triangles>60000)throw new InvalidOperationException("Two-cabinet mesh budget exceeded.");
            foreach(var filter in RoomInstance().GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>().enabled))
            {
                var p=filter.sharedMesh.vertices;var indices=filter.sharedMesh.triangles;
                for(int t=0;t<indices.Length;t+=3)if(IsOldCabinet(filter.transform.TransformPoint(p[indices[t]]))&&IsOldCabinet(filter.transform.TransformPoint(p[indices[t+1]]))&&IsOldCabinet(filter.transform.TransformPoint(p[indices[t+2]])))throw new InvalidOperationException("Old visible cabinet fragment remains.");
            }
            MarketFacadeAuthoring.Validate();File.WriteAllText(Output+"/validation.txt","PASS two linked cabinets/shared dark HDRP finishes/owned PBR maps/real bounds/both original cabinets removed; unchanged current gameplay-collision-nav and all frozen accepted art bytes.\nTriangles="+triangles+"\n"+EnemyValidationRunner.ConsoleCounts());
        }
        public static void Capture()
        {
            View("cabinet-front",new Vector3(-14.8f,1.75f,36.6f),new Vector3(-16.9f,1.25f,36.6f));View("cabinet-angle",new Vector3(-14.9f,1.8f,34.9f),new Vector3(-17,1.3f,36.6f));View("cabinet-hardware",new Vector3(-16.0f,1.5f,36.15f),new Vector3(-16.6f,1.3f,36.6f));View("cabinet-upper",new Vector3(-15.7f,2.25f,37.6f),new Vector3(-16.65f,1.98f,37.18f));
            View("both-cabinets",new Vector3(-11.95f,1.8f,40.5f),new Vector3(-17,1.30f,40.5f));
            View("north-cabinet",new Vector3(-14.8f,1.75f,44.4f),new Vector3(-16.9f,1.25f,44.4f));
        }
        private static void View(string name,Vector3 p,Vector3 at)=>BattlefieldBaseline.Capture(BattlefieldBlockout.RequireBlockout(),name,p,Quaternion.LookRotation(at-p),false,Output,temporalAntialiasing:true);
        private static Transform RoomInstance()=>BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>()).Single(t=>t.name=="14 Art - Walk-through market room").GetComponentsInChildren<Transform>().Single(t=>t.name=="MarketRoom");
        private static GameObject FindRoot()=>BattlefieldBlockout.RequireBlockout().GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t=>t.name==RootName||t.name==LegacyRootName)?.gameObject;
        private static void RequireIdle(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Wait for an idle Unity Editor.");}
    }
}
