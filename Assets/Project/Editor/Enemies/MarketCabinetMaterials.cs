using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class MarketCabinetMaterials
    {
        internal const string Folder = "Assets/Project/Art/Props/MarketStorageCabinet";
        public static void Bake()
        {
            BattlefieldCornerMaterials.EnsureFolder(Folder + "/Materials"); BattlefieldCornerMaterials.EnsureFolder(Folder + "/Textures");
            Finish("CabinetCoat", new Color(.18f,.23f,.25f), .18f,.29f,1024,31,false);
            Finish("CabinetDoorPaint", new Color(.235f,.285f,.30f), .12f,.31f,2048,79,true);
            Finish("CabinetMetal", new Color(.45f,.49f,.51f), .92f,.46f,512,113,false);
            Finish("CabinetRubber", new Color(.055f,.068f,.070f), 0,.17f,512,149,false);
            Finish("CabinetAccent", new Color(.53f,.28f,.12f), .12f,.28f,512,181,false);
            string label = Folder + "/Textures/CabinetLabel.png";
            File.Copy("SourceArt/Props/MarketStorageCabinet/CabinetLabel.png", label, true); Import(label, true, false, 1024);
            var material = Material("CabinetLabel"); material.SetColor("_BaseColor",Color.white); material.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(label));
            material.SetFloat("_Metallic",.15f); material.SetFloat("_Smoothness",.25f); HDMaterial.ValidateMaterial(material);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        }
        private static void Finish(string name,Color paint,float metal,float smooth,int size,int seed,bool twoDoors)
        {
            var colors=new Color[size*size];var mask=new Color[colors.Length];var normals=new Color[colors.Length];var heights=new float[colors.Length];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                int i=y*size+x;float u=(x+.5f)/size,v=(y+.5f)/size;
                float edge=Mathf.Min(Mathf.Min(twoDoors?Mathf.Repeat(u*2,1):u,twoDoors?1-Mathf.Repeat(u*2,1):1-u),Mathf.Min(v,1-v));
                float grain=Mathf.PerlinNoise(x*.17f+seed,y*.17f)-.5f;
                float mottling=Mathf.PerlinNoise(u*19+seed,v*17)-.5f;
                float chipped=Mathf.Clamp01((Mathf.PerlinNoise(u*127+seed,v*131)-.70f)*6)*Mathf.Clamp01(1-edge/.012f);
                float abrasion=Mathf.Clamp01(1-edge/.045f)*(.35f+grain*.5f);
                float hairline=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*713+Mathf.PerlinNoise(u*12+seed,v*19)*19)),28)*Mathf.Pow(Mathf.PerlinNoise(u*43+seed,v*59),7);
                if(name=="CabinetRubber"){chipped=0;hairline=0;}
                if(name=="CabinetMetal")chipped*=.15f;
                float wear=Mathf.Clamp01(chipped*.75f+abrasion*.12f+hairline*.38f);
                colors[i]=Color.Lerp(paint*(1+mottling*.045f+grain*.018f),new Color(.32f,.36f,.37f),wear);colors[i].a=1;
                mask[i]=new Color(Mathf.Lerp(metal,.78f,chipped),1-chipped*.08f,1,Mathf.Clamp01(smooth+grain*.055f+abrasion*.11f-chipped*.07f));
                heights[i]=-chipped*.025f-hairline*.008f+grain*.0015f;
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=heights[y*size+Mathf.Min(size-1,x+1)]-heights[y*size+Mathf.Max(0,x-1)];float dy=heights[Mathf.Min(size-1,y+1)*size+x]-heights[Mathf.Max(0,y-1)*size+x];
                var n=new Vector3(-dx*3,-dy*3,1).normalized;normals[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            var material=Material(name);material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseColorMap",Save(name+"_BaseColor",colors,size,true,false));material.SetTexture("_MaskMap",Save(name+"_Mask",mask,size,false,false));material.SetTexture("_NormalMap",Save(name+"_Normal",normals,size,false,true));
            material.SetFloat("_MetallicRemapMin",0);material.SetFloat("_MetallicRemapMax",1);material.SetFloat("_SmoothnessRemapMin",0);material.SetFloat("_SmoothnessRemapMax",1);material.SetFloat("_AORemapMin",0);material.SetFloat("_AORemapMax",1);
            material.SetFloat("_NormalScale",.6f);material.SetFloat("_EnableGeometricSpecularAA",1);HDMaterial.ValidateMaterial(material);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        }
        private static Material Material(string name)
        {
            string path=Folder+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("HDRP/Lit")){name=name};AssetDatabase.CreateAsset(material,path);}return material;
        }
        private static Texture2D Save(string name,Color[] pixels,int size,bool srgb,bool normal)
        {
            string path=Folder+"/Textures/"+name+".png";var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,!srgb);
            try{texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}finally{Object.DestroyImmediate(texture);}
            int maxImportSize = name.StartsWith("CabinetDoorPaint_", StringComparison.Ordinal) ? 1024 : size;
            Import(path,srgb,normal,maxImportSize);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static void Import(string path,bool srgb,bool normal,int size)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=8;importer.maxTextureSize=size;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
    }
}
