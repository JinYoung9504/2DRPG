// 메뉴: Tools > Maps > 맵 2개 구성하기
//  지금 열려 있는 씬(세리아·마을 배경·HUD 등)을 바탕으로
//  Map1_Slime (슬라임, 오른쪽 끝 → Map2) / Map2_Boar (멧돼지, 왼쪽 끝 → Map1) 두 씬을 만듭니다.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapBuilder
{
    const string Map1 = "Assets/Scenes/Map1_Slime.unity";
    const string Map2 = "Assets/Scenes/Map2_Boar.unity";
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/맵 2개 구성하기 (슬라임 맵 → 멧돼지 맵)")]
    public static void Build()
    {
        if (GameObject.Find("Seria") == null || Find<StageBounds>().Length == 0)
        {
            EditorUtility.DisplayDialog("맵 구성", "지금 씬에 'Seria'와 마을 배경(Town Background)이 있어야 합니다.\n먼저 Tools > Background > 마을 배경 만들기 를 실행하세요.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("맵 구성",
            "지금 씬을 바탕으로 두 개의 맵을 만듭니다.\n\n• Map1_Slime : 슬라임만, 오른쪽 끝 → Map2\n• Map2_Boar : 멧돼지만, 왼쪽 끝 → Map1\n\nAssets/Scenes 폴더에 저장됩니다. 진행할까요?", "만들기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        // ── 맵 1: 슬라임 ──
        var scene = EditorSceneManager.GetActiveScene();
        RemoveAll<BoarEnemy>();
        SlimeTools.SpawnSlimes();                       // 기존 슬라임 정리 후 3마리 배치
        var stage = Find<StageBounds>()[0];
        ResetMarkers();
        Spawn("Spawn_Left", stage.left + 2f);
        Spawn("Spawn_Right", stage.right - 2f);
        Portal("Portal_Right (→ Map2)", stage.right - 0.7f, "Map2_Boar", "Left");
        EditorSceneManager.SaveScene(scene, Map1);

        // ── 맵 2: 멧돼지 (맵 1을 복사해서 몬스터만 교체) ──
        EditorSceneManager.SaveScene(scene, Map2, true);
        var scene2 = EditorSceneManager.OpenScene(Map2);
        RemoveAll<SlimeEnemy>();
        BoarTools.SpawnAt(new[] { -10f, 4f, 18f });
        stage = Find<StageBounds>()[0];
        ResetMarkers();
        Spawn("Spawn_Left", stage.left + 2f);
        Spawn("Spawn_Right", stage.right - 2f);
        Portal("Portal_Left (→ Map1)", stage.left + 0.7f, "Map1_Slime", "Right");
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);   // 에디터에서 바로 Play 할 때 시작 위치
        EditorSceneManager.SaveScene(scene2);

        // ── 빌드 목록에 등록 (타이틀 → Map1 → Map2) ──
        const string Title = "Assets/Scenes/Title.unity";   // 타이틀 씬이 있으면 항상 맨 앞
        var list = new List<EditorBuildSettingsScene>();
        if (System.IO.File.Exists(Title)) list.Add(new EditorBuildSettingsScene(Title, true));
        list.Add(new EditorBuildSettingsScene(Map1, true)); list.Add(new EditorBuildSettingsScene(Map2, true));
        list.AddRange(EditorBuildSettings.scenes.Where(s => s.path != Map1 && s.path != Map2 && s.path != Title));
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene(Map1);
        EditorUtility.DisplayDialog("맵 구성 완료",
            "Map1_Slime 과 Map2_Boar 가 만들어졌습니다.\n지금 Map1 이 열려 있습니다. Play 후 오른쪽 끝(빛 기둥)으로 가 보세요.", "확인");
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }

    static void RemoveAll<T>() where T : Component
    {
        foreach (var c in Find<T>()) Object.DestroyImmediate(c.gameObject);
    }

    static void ResetMarkers()
    {
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        foreach (var n in new[] { "Spawn_Left", "Spawn_Right" })
        {
            var g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }
    }

    static void Spawn(string name, float x)
    {
        var g = new GameObject(name);
        g.transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }

    static void Portal(string name, float x, string target, string side)
    {
        var g = new GameObject(name);
        g.transform.position = new Vector3(x, FeetY, 0);
        var box = g.AddComponent<BoxCollider2D>();
        box.isTrigger = true; box.size = new Vector2(1f, 4f); box.offset = new Vector2(0, 2f);
        var p = g.AddComponent<MapPortal>();
        p.targetScene = target; p.spawnSide = side;
    }
}
