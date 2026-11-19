using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class SealedSupplyPackAuthoring
    {
        private const string Folder = "Assets/Project/Art/Props/SealedSupplyPack";
        private const string RootName = "16 Props - Sealed supply parcels";
        private const string Output = "Logs/SealedSupplyPack";
        private const string MarketModel = "Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Models/MarketRoom.fbx";
        private const int Width = 1024;
        private const int Height = 2048;

        [MenuItem("Breachpoint/Art/Props/Replace Market Supply Parcels")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Editor before replacing supply parcels.");
            var scene = BattlefieldBlockout.RequireBlockout();Directory.CreateDirectory(Output);
            string before = BattlefieldCornerArt.GameplayFingerprint(scene);
            if (!File.Exists(Output + "/gameplay-before.txt"))
            {
                File.WriteAllText(Output + "/gameplay-before.txt", before);
                File.Copy(scene.path, Output + "/scene-before.unity", true);
            }
            BakeMaterial();ConfigureModel();
            string prefabPath = Folder + "/PF_SealedSupplyPack.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var prefabRoot = new GameObject("Sealed supply pack");
                try
                {
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/SealedSupplyPack.fbx"), prefabRoot.transform);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
                finally { Object.DestroyImmediate(prefabRoot); }
            }
            var root = FindRoot();
            if (root == null)
            {
                root = new GameObject(RootName);
                var parent = scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>()).Single(t=>t.name=="Blockout B - Service Quarter");
                root.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(root,"Replace supply parcels with standalone props");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                for (int i=0;i<2;i++)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
                    instance.name="Sealed field ration "+(i+1);
                    instance.transform.SetPositionAndRotation(new Vector3(-12,1.210f,i==0?36.33f:36.80f),Quaternion.Euler(0,i==0?-4:3,0));
                }
            }
            ApplyLegacyRemoval();
            MarketServiceDoorAuthoring.ApplyRackRemoval();
            Validate();
            if (before != BattlefieldCornerArt.GameplayFingerprint(scene)) throw new InvalidOperationException("Parcel replacement changed gameplay.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Parcel replacement scene could not be saved.");
            Capture();
        }

        // Rebuild only index buffers from the canonical source whenever room art is reimported.
        // This avoids freezing unrelated portions of its combined material meshes.
        internal static void ApplyLegacyRemoval()
        {
            if (FindRoot()==null) return;
            BattlefieldCornerMaterials.EnsureFolder(Folder+"/Integration");
            var scene=BattlefieldBlockout.RequireBlockout();
            var modelRoot=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>()).Single(t=>t.name=="14 Art - Walk-through market room");
            var instance=modelRoot.GetComponentsInChildren<Transform>().Single(t=>t.name=="MarketRoom");
            var canonical=AssetDatabase.LoadAssetAtPath<GameObject>(MarketModel);
            int removed=0;
            foreach (var source in canonical.GetComponentsInChildren<MeshFilter>())
            {
                if (!source.name.EndsWith("InteriorNeutral",StringComparison.Ordinal) && !source.name.EndsWith("MarketWarm",StringComparison.Ordinal)) continue;
                var target=instance.GetComponentsInChildren<MeshFilter>().Single(f=>f.name==source.name);
                var original=source.sharedMesh;var positions=original.vertices;
                bool InLegacy(Vector3 local)
                {
                    Vector3 p=target.transform.TransformPoint(local);
                    return p.x>-12.246f && p.x<-11.754f && p.y>1.204f && p.y<1.351f &&
                        (Mathf.Abs(p.z-36.33f)<.166f || Mathf.Abs(p.z-36.80f)<.166f);
                }
                var filtered=Object.Instantiate(original);int localRemoved=0;
                for (int sub=0;sub<original.subMeshCount;sub++)
                {
                    int[] triangles=original.GetTriangles(sub);var kept=new List<int>(triangles.Length);
                    for (int t=0;t<triangles.Length;t+=3)
                    {
                        if (InLegacy(positions[triangles[t]])&&InLegacy(positions[triangles[t+1]])&&InLegacy(positions[triangles[t+2]])) {localRemoved++;continue;}
                        kept.Add(triangles[t]);kept.Add(triangles[t+1]);kept.Add(triangles[t+2]);
                    }
                    filtered.SetTriangles(kept,sub,false);
                }
                Undo.RecordObject(target,"Remove archived placeholder parcel faces");
                if (localRemoved==0) {target.sharedMesh=original;Object.DestroyImmediate(filtered);}
                else
                {
                    if (localRemoved>1000) throw new InvalidOperationException("Legacy parcel removal exceeded its bounded region.");
                    string path=Folder+"/Integration/Filtered_"+source.name+".asset";
                    var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    filtered.name="Filtered_"+source.name;
                    if (stored==null) {AssetDatabase.CreateAsset(filtered,path);stored=filtered;}
                    else {EditorUtility.CopySerialized(filtered,stored);Object.DestroyImmediate(filtered);EditorUtility.SetDirty(stored);AssetDatabase.SaveAssetIfDirty(stored);}
                    target.sharedMesh=stored;removed+=localRemoved;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            File.WriteAllText(Output+"/legacy-removal.txt","Only triangles wholly inside the two archived parcel envelopes were removed: "+removed+"\nCanonical room assets unchanged; all other vertex data/materials retained.\n");
        }

        public static void Validate()
        {
            var scene=BattlefieldBlockout.RequireBlockout();var root=FindRoot();
            if (root==null || root.transform.childCount!=2) throw new InvalidOperationException("Two standalone parcel instances are required.");
            var finish=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/SupplyRationFoil.mat");
            if (finish==null || finish.shader.name!="HDRP/Lit" || !finish.IsKeywordEnabled("_MASKMAP") || !finish.IsKeywordEnabled("_NORMALMAP_TANGENT_SPACE"))
                throw new InvalidOperationException("Standalone foil HDRP maps are inactive.");
            foreach (string property in new[]{"_BaseColorMap","_MaskMap","_NormalMap"})
            {
                string path=AssetDatabase.GetAssetPath(finish.GetTexture(property));
                if (!path.StartsWith(Folder+"/Textures/",StringComparison.Ordinal)) throw new InvalidOperationException("Prop texture must be self-contained: "+property);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if (!importer.mipmapEnabled || importer.filterMode!=FilterMode.Trilinear || importer.sRGBTexture!=(property=="_BaseColorMap"))
                    throw new InvalidOperationException("Prop texture filtering/color-space failed: "+property);
            }
            if (!MarketServiceDoorAuthoring.MatchesGameplay(scene,File.ReadAllText(Output+"/gameplay-before.txt")))
                throw new InvalidOperationException("Supply prop changed gameplay/navigation data.");
            foreach (Transform child in root.transform)
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)!=Folder+"/PF_SealedSupplyPack.prefab")
                    throw new InvalidOperationException("Supply pack must retain its standalone prefab link.");
                if (child.GetComponentsInChildren<Collider>().Length!=0 || child.GetComponentsInChildren<Light>().Length!=0)
                    throw new InvalidOperationException("Decorative packs must add no collider/light behavior.");
                foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.sharedMaterials.Any(m=>m==null)) throw new InvalidOperationException("Pack material reference missing.");
                    var b=renderer.bounds;
                    if (b.min.x<-12.36f || b.max.x>-11.64f || b.min.z<36.10f || b.max.z>37.10f || b.min.y<1.209f || b.max.y>1.30f)
                        throw new InvalidOperationException("Pack does not seat inside the existing tray.");
                }
            }
            File.WriteAllText(Output+"/validation.txt","PASS standalone prefab links, two placements, owned HDRP maps/filtering/color spaces, tray fit, unchanged gameplay/navigation.\n"+EnemyValidationRunner.ConsoleCounts());
        }

        public static void Capture()
        {
            var scene=BattlefieldBlockout.RequireBlockout();
            View("ration-packs-in-tray",new Vector3(-13.1f,1.95f,37.1f),new Vector3(-12,1.25f,36.6f));
            View("ration-pack-close",new Vector3(-12.48f,1.67f,36.54f),new Vector3(-12,1.245f,36.42f));
            void View(string name,Vector3 p,Vector3 at)=>BattlefieldBaseline.Capture(scene,name,p,Quaternion.LookRotation(at-p),false,Output,temporalAntialiasing:true);
        }

        private static GameObject FindRoot()=>BattlefieldBlockout.RequireBlockout().GetRootGameObjects()
            .SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).SingleOrDefault(t=>t.name==RootName)?.gameObject;

        private static void ConfigureModel()
        {
            string path=Folder+"/Models/SealedSupplyPack.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>())
                foreach (var material in renderer.sharedMaterials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/SupplyRationFoil.mat"));
            importer.SaveAndReimport();
        }

        private static void BakeMaterial()
        {
            BattlefieldCornerMaterials.EnsureFolder(Folder+"/Materials");
            var print=new Texture2D(2,2,TextureFormat.RGBA32,false,true);var label=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            print.LoadImage(File.ReadAllBytes("SourceArt/Props/SealedSupplyPack/SupplyRation_Print.png"));
            label.LoadImage(File.ReadAllBytes("SourceArt/Props/SealedSupplyPack/SupplyRation_LabelMask.png"));
            Color[] colors=print.GetPixels(),labels=label.GetPixels();Object.DestroyImmediate(print);Object.DestroyImmediate(label);
            var masks=new Color[colors.Length];var normals=new Color[colors.Length];var heights=new float[colors.Length];
            for (int y=0;y<Height;y++) for (int x=0;x<Width;x++)
            {
                int i=y*Width+x;float u=x/(float)(Width-1),v=(y%1024)/1023f,l=labels[i].r;
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                float crease=Mathf.PerlinNoise(u*31+13,v*27+7)-.5f;
                float grain=Mathf.PerlinNoise(x*.19f+23,y*.19f)-.5f;
                float flange=Mathf.Clamp01(1-edge/.065f);
                float rib=Mathf.Sin((edge==Mathf.Min(u,1-u)?v:u)*Mathf.PI*150)*flange;
                colors[i]*=1+(1-l)*(grain*.018f+crease*.035f);colors[i].a=1;
                masks[i]=new Color(Mathf.Lerp(.84f,.015f,l),1-flange*.04f,1,Mathf.Lerp(.37f+crease*.07f+rib*.025f,.20f,l));
                heights[i]=(1-l)*(crease*.018f*Mathf.Exp(-Mathf.Pow((edge-.12f)/.12f,2))+rib*.0025f+grain*.002f);
            }
            for (int y=0;y<Height;y++) for (int x=0;x<Width;x++)
            {
                float dx=heights[y*Width+Mathf.Min(Width-1,x+1)]-heights[y*Width+Mathf.Max(0,x-1)];
                float dy=heights[Mathf.Min(Height-1,y+1)*Width+x]-heights[Mathf.Max(0,y-1)*Width+x];
                var n=new Vector3(-dx*7,-dy*7,1).normalized;normals[y*Width+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            string path=Folder+"/Materials/SupplyRationFoil.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("HDRP/Lit")){name="SupplyRationFoil"};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseColorMap",Save("SupplyRation_BaseColor",colors,true,false));
            material.SetTexture("_MaskMap",Save("SupplyRation_Mask",masks,false,false));
            material.SetTexture("_NormalMap",Save("SupplyRation_Normal",normals,false,true));
            material.SetFloat("_MetallicRemapMin",0);material.SetFloat("_MetallicRemapMax",1);
            material.SetFloat("_SmoothnessRemapMin",0);material.SetFloat("_SmoothnessRemapMax",1);
            material.SetFloat("_AORemapMin",0);material.SetFloat("_AORemapMax",1);
            material.SetFloat("_NormalScale",.7f);material.SetFloat("_CoatMask",.06f);material.SetFloat("_EnableGeometricSpecularAA",1);
            HDMaterial.ValidateMaterial(material);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        }

        private static Texture2D Save(string name,Color[] pixels,bool srgb,bool normal)
        {
            string path=Folder+"/Textures/"+name+".png";var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false,!srgb);
            try {texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally {Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
            importer.maxTextureSize=Height;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
