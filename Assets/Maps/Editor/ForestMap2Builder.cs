// 메뉴: Tools > Maps > 정령의 숲 2 추가 (나무 정령)
//  Map4_SpiritForest 오른쪽 끝 → Map5_SpiritForest2 (나무 정령) / 왼쪽 끝 → Map4
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ForestMap2Builder
{
    const string Map4 = "Assets/Scenes/Map4_SpiritForest.unity";
    const string Map5 = "Assets/Scenes/Map5_SpiritForest2.unity";
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/정령의 숲 2 추가 (나무 정령)")]
    public static void Build()
    {
        if (!File.Exists(Map4)) { EditorUtility.DisplayDialog("맵 추가", "Map4_SpiritForest 가 없습니다.\n먼저 'Tools > Maps > 시작 마을 + 정령의 숲 구성'을 실행하세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("맵 추가", "정령의 숲(Map4) 오른쪽 끝 → 정령의 숲 2(Map5, 나무 정령 2마리)\n진행할까요?", "만들기", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var s4 = EditorSceneManager.OpenScene(Map4);
        var stage = Find<StageBounds>()[0];
        foreach (var p in Find<MapPortal>()) if (p.name.StartsWith("Portal_Right")) Object.DestroyImmediate(p.gameObject);
        Portal("Portal_Right (→ Map5)", stage.right - 0.7f, "Map5_SpiritForest2", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s4);

        EditorSceneManager.SaveScene(s4, Map5, true);
        var s5 = EditorSceneManager.OpenScene(Map5);
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        EntTools.SpawnAt(new[] { 5f, 20f });
        Portal("Portal_Left (→ Map4)", stage.left + 0.7f, "Map4_SpiritForest", "Right");
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);
        EditorSceneManager.SaveScene(s5);

        var list = EditorBuildSettings.scenes.Where(s => s.path != Map5).ToList();
        int i4 = list.FindIndex(s => s.path == Map4);
        list.Insert(i4 >= 0 ? i4 + 1 : list.Count, new EditorBuildSettingsScene(Map5, true));
        EditorBuildSettings.scenes = list.ToArray();
        EditorUtility.DisplayDialog("맵 추가 완료", "정령의 숲 2(Map5)가 만들어졌습니다. 지금 열려 있습니다.", "확인");
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
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
