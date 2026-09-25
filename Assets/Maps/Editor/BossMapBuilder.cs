// 메뉴: Tools > Maps > 시작의 마을4 추가 (중간 보스: 대형 마수 늑대)
//  시작의 마을3(Map3) 오른쪽 끝 → 시작의 마을4(Map3b) → 오른쪽 끝 → 정령의 숲(Map4)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossMapBuilder
{
    const string Map3 = "Assets/Scenes/Map3_Golem.unity";            // 시작의 마을3 (독버섯)
    const string Map3b = "Assets/Scenes/Map3b_Town4_WolfBoss.unity"; // 시작의 마을4
    const string Map4 = "Assets/Scenes/Map4_SpiritForest.unity";      // 정령의 숲
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/시작의 마을4 추가 (중간 보스 늑대)")]
    public static void Build()
    {
        foreach (var p in new[] { Map3, Map4 })
            if (!File.Exists(p)) { EditorUtility.DisplayDialog("맵 추가", p + " 가 없습니다. 이전 맵 메뉴들을 먼저 실행하세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("맵 추가", "시작의 마을3 → 시작의 마을4 (대형 마수 늑대) → 정령의 숲\n진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 시작의 마을3: 오른쪽 → 시작의 마을4
        var s3 = EditorSceneManager.OpenScene(Map3);
        var stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Right");
        Portal("Portal_Right (→ 시작의 마을4)", stage.right - 0.7f, "Map3b_Town4_WolfBoss", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s3);

        // 시작의 마을4: 시작의 마을3 복사 → 몬스터 제거, 보스 배치
        EditorSceneManager.SaveScene(s3, Map3b, true);
        var s3b = EditorSceneManager.OpenScene(Map3b);
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        WolfTools.SpawnAt(8f);
        Portal("Portal_Left (→ 시작의 마을3)", stage.left + 0.7f, "Map3_Golem", "Right");
        Portal("Portal_Right (→ 정령의 숲)", stage.right - 0.7f, "Map4_SpiritForest", "Left");
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);
        EditorSceneManager.SaveScene(s3b);

        // 정령의 숲: 왼쪽 → 시작의 마을4
        var s4 = EditorSceneManager.OpenScene(Map4);
        stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Left");
        Portal("Portal_Left (→ 시작의 마을4)", stage.left + 0.7f, "Map3b_Town4_WolfBoss", "Right");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s4);

        var list = EditorBuildSettings.scenes.Where(s => s.path != Map3b).ToList();
        int i3 = list.FindIndex(s => s.path == Map3);
        list.Insert(i3 >= 0 ? i3 + 1 : list.Count, new EditorBuildSettingsScene(Map3b, true));
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene(Map3b);
        EditorUtility.DisplayDialog("맵 추가 완료", "시작의 마을4(중간 보스)가 만들어졌습니다. 지금 열려 있습니다.", "확인");
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
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
