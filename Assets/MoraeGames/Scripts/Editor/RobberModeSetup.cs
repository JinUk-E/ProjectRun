using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using MoraeGames.UI;

namespace MoraeGames.Editor
{
    /// <summary>Updates the existing UI in place without rebuilding scenes or gameplay objects.</summary>
    public static class RobberModeSetup
    {
        [MenuItem("MoraeGames/Apply Robber Only Mode UI")]
        public static void Apply()
        {
            Directory.CreateDirectory("SourceAssets/SceneBackups/BeforeRobberMode");
            foreach(string name in new[]{"MainMenu","Lobby"})
            {
                string path="Assets/MoraeGames/Scenes/"+name+".unity";
                string backup="SourceAssets/SceneBackups/BeforeRobberMode/"+name+".unity";
                if(!File.Exists(backup)) File.Copy(path,backup);
                var scene=EditorSceneManager.OpenScene(path);
                foreach(var root in scene.GetRootGameObjects())
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.text=text.text.Replace("01  팀 선택","01  도둑 협동")
                        .Replace("어느 팀으로 뛸까요?","AI 경찰을 피해 함께 탈출하세요")
                        .Replace("경찰 · 추격과 체포","AI 경찰 · 추격과 체포")
                        .Replace("도둑 · 탈취와 구출","플레이어 · 도둑")
                        .Replace("연습: AI와 대결   /   온라인: 방 만들기·참가","혼자 또는 함께 도둑으로 플레이 · 경찰은 AI")
                        .Replace("Role: Not Selected","도둑 협동 · 스킬을 선택하세요");
                }
                if(name=="Lobby")
                {
                    RectTransform cop=null,robber=null;
                    foreach(var root in scene.GetRootGameObjects())
                    foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    {
                        if(button.name=="CopButton") cop=(RectTransform)button.transform;
                        if(button.name=="RobberButton") robber=(RectTransform)button.transform;
                    }
                    if(cop!=null && robber!=null)
                    {
                        robber.anchorMin=Vector2.Min(cop.anchorMin,robber.anchorMin);
                        robber.anchorMax=Vector2.Max(cop.anchorMax,robber.anchorMax);
                        cop.gameObject.SetActive(false);
                        robber.GetComponent<UnityEngine.UI.Button>().interactable=false;
                        var label=robber.GetComponentInChildren<TMP_Text>();
                        label.text="도둑 협동 모드";
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
        public static void ApplyAndBuild() { Apply(); MovementProbeBuild.Run(); }
    }
}
