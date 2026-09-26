// 메뉴: Tools > Skills > 마을 NPC 에 스킬 교관 설정
//  시작 마을 이장 = A 검기 · S 번개 / 정령의 숲 요정 = D 메테오 / 성곽 저주받은 기사 = F 성검 유성우
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SkillTeacherTools
{
    static readonly (string scene, string npc, string[] ids, string title)[] Setup =
    {
        ("Assets/Scenes/Map0_Town.unity", "NPC_VillageChief", new[] { "A", "S" }, "마을 이장 — 스킬 습득"),
        ("Assets/Scenes/Map4_SpiritForest.unity", "NPC_ForestFairy", new[] { "D" }, "숲의 요정 — 스킬 습득"),
        ("Assets/Scenes/Map7_RuinedCastle.unity", "NPC_CursedKnight", new[] { "F" }, "저주받은 기사 — 스킬 습득"),
    };

    [MenuItem("Tools/Skills/마을 NPC 에 스킬 교관 설정")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string first = EditorSceneManager.GetActiveScene().path, log = "";
        foreach (var s in Setup)
        {
            if (!File.Exists(s.scene)) { log += $"\n✗ {Path.GetFileNameWithoutExtension(s.scene)} 맵 없음"; continue; }
            var scene = EditorSceneManager.OpenScene(s.scene);
            var npc = GameObject.Find(s.npc);
            if (npc == null) { log += $"\n✗ {s.npc} 없음 (NPC 배치 메뉴를 먼저 실행)"; continue; }
            var t = npc.GetComponent<SkillTeacher>(); if (t == null) t = npc.AddComponent<SkillTeacher>();
            t.skillIds = s.ids; t.windowTitle = s.title;
            EditorUtility.SetDirty(t); EditorSceneManager.SaveScene(scene);
            log += $"\n✓ {s.npc} : {string.Join(", ", s.ids)}";
        }
        if (!string.IsNullOrEmpty(first)) EditorSceneManager.OpenScene(first);
        EditorUtility.DisplayDialog("스킬 교관 설정", "마을 NPC 스킬 교관 설정 결과:" + log, "확인");
    }
}
