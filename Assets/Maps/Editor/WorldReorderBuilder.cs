// 메뉴: Tools > Maps > 맵 재구성 (버섯 추가, 골렘 → 정령의 숲1)
//  Map3_Golem          → 시작의 마을3 : 골렘 제거, 독버섯 배치
//  Map4_SpiritForest   → 정령의 숲 (몬스터 없음), 오른쪽 끝 → 정령의 숲1
//  Map4b_SpiritForest1 → 정령의 숲1 (새 맵, 숲 배경) : 골렘, 오른쪽 끝 → 정령의 숲2
//  Map5_SpiritForest2  → 정령의 숲2 : 나무 정령 (수치 갱신), 왼쪽 끝 → 정령의 숲1
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldReorderBuilder
{
    const string Map3 = "Assets/Scenes/Map3_Golem.unity";
    const string Map4 = "Assets/Scenes/Map4_SpiritForest.unity";
    const string Map4b = "Assets/Scenes/Map4b_SpiritForest1.unity";
    const string Map5 = "Assets/Scenes/Map5_SpiritForest2.unity";
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/맵 재구성 (버섯 추가, 골렘 → 정령의 숲1)")]
    public static void Build()
    {
        foreach (var p in new[] { Map3, Map4, Map5 })
            if (!File.Exists(p)) { EditorUtility.DisplayDialog("맵 재구성", p + " 가 없습니다. 이전 맵 메뉴들을 먼저 실행하세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("맵 재구성",
            "• 시작의 마을3 (Map3) : 골렘 → 독버섯 3마리\n• 정령의 숲 (Map4) → 정령의 숲1 (새 맵, 골렘) → 정령의 숲2 (나무 정령)\n• 골렘·나무 정령 수치 갱신\n\n진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 시작의 마을3: 골렘 → 버섯
        var s3 = EditorSceneManager.OpenScene(Map3);
        RemoveAll<GolemEnemy>();
        MushroomTools.SpawnAt(new[] { -8f, 5f, 18f });
        EditorSceneManager.SaveScene(s3);

        // 정령의 숲: 오른쪽 끝 → 정령의 숲1
        var s4 = EditorSceneManager.OpenScene(Map4);
        var stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Right");
        Portal("Portal_Right (→ 정령의 숲1)", stage.right - 0.7f, "Map4b_SpiritForest1", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s4);

        // 정령의 숲1: 정령의 숲 복사 → 골렘 배치
        EditorSceneManager.SaveScene(s4, Map4b, true);
        var s4b = EditorSceneManager.OpenScene(Map4b);
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        GolemTools.SpawnAt(new[] { -6f, 8f, 20f });
        Portal("Portal_Left (→ 정령의 숲)", stage.left + 0.7f, "Map4_SpiritForest", "Right");
        Portal("Portal_Right (→ 정령의 숲2)", stage.right - 0.7f, "Map5_SpiritForest2", "Left");
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);
        EditorSceneManager.SaveScene(s4b);

        // 정령의 숲2: 왼쪽 끝 → 정령의 숲1, 나무 정령 새 수치로 다시 배치
        var s5 = EditorSceneManager.OpenScene(Map5);
        stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Left");
        Portal("Portal_Left (→ 정령의 숲1)", stage.left + 0.7f, "Map4b_SpiritForest1", "Right");
        var ents = Find<TreeEntEnemy>().Select(t => t.transform.position.x).ToArray();
        EntTools.SpawnAt(ents.Length > 0 ? ents : new[] { 5f, 20f });
        EditorSceneManager.SaveScene(s5);

        // 빌드 목록: Map4 바로 뒤에 Map4b
        var list = EditorBuildSettings.scenes.Where(s => s.path != Map4b).ToList();
        int i4 = list.FindIndex(s => s.path == Map4);
        list.Insert(i4 >= 0 ? i4 + 1 : list.Count, new EditorBuildSettingsScene(Map4b, true));
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene(Map3);
        EditorUtility.DisplayDialog("맵 재구성 완료", "시작의 마을3에 독버섯, 정령의 숲1에 골렘을 배치했습니다.\n지금 시작의 마을3이 열려 있습니다.", "확인");
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
    static void RemoveAll<T>() where T : Component { foreach (var c in Find<T>()) Object.DestroyImmediate(c.gameObject); }
    static void RemovePortal(string prefix) { foreach (var p in Find<MapPortal>()) if (p.name.StartsWith(prefix)) Object.DestroyImmediate(p.gameObject); }
    static void EnsureSpawns(StageBounds stage)
    {
        foreach (var (n, x) in new[] { ("Spawn_Left", stage.left + 2f), ("Spawn_Right", stage.right - 2f) })
            if (GameObject.Find(n) == null) new GameObject(n).transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }
    static void Portal(string name, float x, string target, string side)
    {
        var g = new GameObject(name); g.transform.position = new Vector3(x, FeetY, 0);
        var box = g.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(1f, 4f); box.offset = new Vector2(0, 2f);
        var p = g.AddComponent<MapPortal>(); p.targetScene = target; p.spawnSide = side;
    }
}
