// 메뉴: Tools > Maps > 맵 3 추가 (멧돼지 맵 → 골렘 맵)
//  Map2_Boar 오른쪽 끝 → Map3_Golem (골렘만) / Map3_Golem 왼쪽 끝 → Map2_Boar
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Map3Builder
{
    const string Map1 = "Assets/Scenes/Map1_Slime.unity";
    const string Map2 = "Assets/Scenes/Map2_Boar.unity";
    const string Map3 = "Assets/Scenes/Map3_Golem.unity";
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/맵 3 추가 (멧돼지 맵 → 골렘 맵)")]
    public static void Build()
    {
        if (!File.Exists(Map2))
        {
            EditorUtility.DisplayDialog("맵 3 추가", "Assets/Scenes/Map2_Boar 가 없습니다.\n먼저 Tools > Maps > 맵 2개 구성하기 를 실행하세요.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("맵 3 추가",
            "• Map2_Boar 오른쪽 끝 → Map3_Golem 으로 이동하는 길을 만듭니다.\n• Map3_Golem : 같은 배경, 골렘만 등장 (왼쪽 끝 → Map2)\n• 테스트로 Map1·Map2 에 놓았던 골렘은 지웁니다.\n\n진행할까요?", "만들기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Map1: 테스트용 골렘 정리
        if (File.Exists(Map1))
        {
            var s1 = EditorSceneManager.OpenScene(Map1);
            if (RemoveAll<GolemEnemy>() > 0) EditorSceneManager.SaveScene(s1);
        }

        // Map2: 골렘 정리 + 오른쪽 끝 포탈
        var s2 = EditorSceneManager.OpenScene(Map2);
        RemoveAll<GolemEnemy>();
        var stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Right");
        Portal("Portal_Right (→ Map3)", stage.right - 0.7f, "Map3_Golem", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s2);

        // Map3: Map2 복사 → 멧돼지 제거, 골렘 배치, 포탈 정리
        EditorSceneManager.SaveScene(s2, Map3, true);
        var s3 = EditorSceneManager.OpenScene(Map3);
        RemoveAll<BoarEnemy>();
        RemoveAll<SlimeEnemy>();
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        GolemTools.SpawnAt(new[] { -8f, 6f, 20f });
        Portal("Portal_Left (→ Map2)", stage.left + 0.7f, "Map2_Boar", "Right");
        EnsureSpawns(stage);
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);
        EditorSceneManager.SaveScene(s3);

        // 빌드 목록: Map2 바로 뒤에 Map3
        var list = EditorBuildSettings.scenes.Where(s => s.path != Map3).ToList();
        int i2 = list.FindIndex(s => s.path == Map2);
        list.Insert(i2 >= 0 ? i2 + 1 : list.Count, new EditorBuildSettingsScene(Map3, true));
        EditorBuildSettings.scenes = list.ToArray();

        EditorUtility.DisplayDialog("맵 3 추가 완료", "Map3_Golem 이 만들어졌습니다.\n지금 Map3 가 열려 있습니다.\n\n처음부터 확인하려면 Title 씬에서 Play → 멧돼지 맵 오른쪽 끝으로 가 보세요.", "확인");
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
    static int RemoveAll<T>() where T : Component { var a = Find<T>(); foreach (var c in a) Object.DestroyImmediate(c.gameObject); return a.Length; }
    static void RemovePortal(string prefix) { foreach (var p in Find<MapPortal>()) if (p.name.StartsWith(prefix)) Object.DestroyImmediate(p.gameObject); }

    static void EnsureSpawns(StageBounds stage)
    {
        foreach (var (n, x) in new[] { ("Spawn_Left", stage.left + 2f), ("Spawn_Right", stage.right - 2f) })
            if (GameObject.Find(n) == null) new GameObject(n).transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }

    static void Portal(string name, float x, string target, string side)
    {
        var g = new GameObject(name);
        g.transform.position = new Vector3(x, FeetY, 0);
        var box = g.AddComponent<BoxCollider2D>();
        box.isTrigger = true; box.size = new Vector2(1f, 4f); box.offset = new Vector2(0, 2f);
        var p = g.AddComponent<MapPortal>(); p.targetScene = target; p.spawnSide = side;
    }
}
