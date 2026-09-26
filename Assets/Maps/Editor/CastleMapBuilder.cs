// 메뉴: Tools > Maps > 멸망한 왕국 성곽 추가
//  정령의 숲3(나무정령 보스) 오른쪽 끝 → 멸망한 왕국 성곽 (몬스터 없음) → 멸망한 왕국 성곽1 (몬스터 맵, 아직 비어 있음)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CastleMapBuilder
{
    const string Prev = "Assets/Scenes/Map6_SpiritForest3_TreeBoss.unity";
    const string Map7 = "Assets/Scenes/Map7_RuinedCastle.unity";
    const string Map8 = "Assets/Scenes/Map8_RuinedCastle1.unity";
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/멸망한 왕국 성곽 추가")]
    public static void Build()
    {
        if (!File.Exists(Prev)) { EditorUtility.DisplayDialog("맵 추가", "정령의 숲3(Map6_SpiritForest3_TreeBoss)이 없습니다.\n먼저 'Tools > Maps > 정령의 숲3 추가'를 실행하세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("맵 추가",
            "정령의 숲3 → 멸망한 왕국 성곽 (몬스터 없음) → 멸망한 왕국 성곽1 (몬스터 맵)\n진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 정령의 숲3: 오른쪽 → 성곽
        var s6 = EditorSceneManager.OpenScene(Prev);
        var stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Right");
        Portal("Portal_Right (→ 멸망한 왕국 성곽)", stage.right - 0.7f, "Map7_RuinedCastle", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s6);

        // 성곽: 정령의 숲3 복사 → 몬스터·포탈 제거, 배경 교체
        EditorSceneManager.SaveScene(s6, Map7, true);
        var s7 = EditorSceneManager.OpenScene(Map7);
        Clean();
        stage = Find<StageBounds>()[0];
        SwapBackground(stage.gameObject);
        Portal("Portal_Left (→ 정령의 숲3)", stage.left + 0.7f, "Map6_SpiritForest3_TreeBoss", "Right");
        Portal("Portal_Right (→ 성곽1)", stage.right - 0.7f, "Map8_RuinedCastle1", "Left");
        EnsureSpawns(stage);
        PlaceSeria(stage.left + 2f);
        EditorSceneManager.SaveScene(s7);

        // 성곽1: 성곽 복사 → 포탈만 정리 (몬스터는 이후 추가)
        EditorSceneManager.SaveScene(s7, Map8, true);
        var s8 = EditorSceneManager.OpenScene(Map8);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        Portal("Portal_Left (→ 멸망한 왕국 성곽)", stage.left + 0.7f, "Map7_RuinedCastle", "Right");
        PlaceSeria(stage.left + 2f);
        EditorSceneManager.SaveScene(s8);

        // 빌드 목록
        var list = EditorBuildSettings.scenes.Where(s => s.path != Map7 && s.path != Map8).ToList();
        int i = list.FindIndex(s => s.path == Prev);
        list.InsertRange(i >= 0 ? i + 1 : list.Count, new[] { new EditorBuildSettingsScene(Map7, true), new EditorBuildSettingsScene(Map8, true) });
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene(Map7);
        EditorUtility.DisplayDialog("맵 추가 완료", "멸망한 왕국 성곽(Map7)과 성곽1(Map8)이 만들어졌습니다.\n성곽1의 몬스터는 이후 추가합니다.", "확인");
    }

    static void Clean()
    {
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
        foreach (var n in Find<NpcTalker>()) Object.DestroyImmediate(n.gameObject);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
    }

    static void SwapBackground(GameObject root)
    {
        root.name = "Castle Background";
        var far = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Backgrounds/Castle/Castle_Far.png");
        var ground = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Backgrounds/Castle/Castle_Ground.png");
        if (far == null || ground == null) { Debug.LogError("성곽 배경 그림을 찾을 수 없습니다: Assets/Backgrounds/Castle/"); return; }
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.GetComponent<ParallaxFit>() != null) sr.sprite = far;
            else if (sr.drawMode == SpriteDrawMode.Tiled) { var size = sr.size; sr.sprite = ground; sr.drawMode = SpriteDrawMode.Tiled; sr.size = size; }
        }
        var cam = Camera.main; if (cam != null) cam.backgroundColor = new Color(0.25f, 0.08f, 0.15f);
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
    static void PlaceSeria(float x) { var s = GameObject.Find("Seria"); if (s != null) s.transform.position = new Vector3(x, FeetY + 0.05f, 0); }
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
