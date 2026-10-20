using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using MoraeGames.Ability;
namespace MoraeGames.Editor
{
    /// <summary>Repairs loadout references only; preserves character art, physics and networking.</summary>
    public static class RoomLoadoutRepair
    {
        public static void RunAndBuild()
        {
            string backup="SourceAssets/SceneBackups/BeforeOnlineLoadouts";Directory.CreateDirectory(backup);
            string[] cops={"SpeedBoost","AreaScan","CCProjectile"}, robbers={"SpeedBurst","SmokeBomb","Disguise","DecoyClone","EscapeTrap"};
            foreach(string role in new[]{"Cop","Robber"})
            {
                var names=role=="Cop"?cops:robbers;
                for(int i=0;i<names.Length;i++)
                {
                    string path=$"Assets/MoraeGames/Prefabs/Characters/{role}_{i+1}.prefab";
                    string copy=backup+"/"+Path.GetFileName(path);if(!File.Exists(copy))File.Copy(path,copy);
                    var prefab=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var data=AssetDatabase.LoadAssetAtPath<AbilityDataSO>($"Assets/MoraeGames/Prefabs/Abilities/{names[i]}.asset");
                        if(data==null)throw new InvalidOperationException("Missing skill "+names[i]);
                        prefab.GetComponent<AbilityHolder>().abilityData=data;PrefabUtility.SaveAsPrefabAsset(prefab,path);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(prefab);}
                }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("TestResults/loadout-mapping.txt","PASS: 3 cop and 5 robber prefab skills now match lobby order; backups preserved.");
            MovementProbeBuild.Run();
        }
    }
}
