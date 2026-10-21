using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoraeGames.Editor
{
    public static class SkillVfxSetup
    {
        [MenuItem("MoraeGames/Apply Kenney Skill VFX")]
        public static void Apply()
        {
            const string art="Assets/MoraeGames/Art/KenneyParticles/";
            const string output="Assets/MoraeGames/Resources/SkillVfx/";
            Directory.CreateDirectory(output); AssetDatabase.Refresh();
            var shader=Shader.Find("MoraeGames/SkillParticle");
            if(shader==null) throw new InvalidOperationException("Missing particle shader");
            foreach(string name in new[]{"smoke_01","spark_06","star_04","twirl_01","magic_02"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(art+name+".png");
                importer.textureType=TextureImporterType.Default;
                importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
                importer.wrapMode=TextureWrapMode.Clamp; importer.maxTextureSize=256;
                importer.SaveAndReimport();
                string path=output+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader=shader; material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(art+name+".png"));
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        public static void ApplyAndBuild() { Apply(); MovementProbeBuild.Run(); }
    }
}
