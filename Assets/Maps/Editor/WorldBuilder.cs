// 메뉴: Tools > Maps > 시작 마을 + 정령의 숲 구성
//  Map0_Town (시작 마을, 몬스터 없음, 게임 시작 지점) ⇄ Map1_Slime ⇄ Map2_Boar ⇄ Map3_Golem ⇄ Map4_SpiritForest (정령의 숲)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldBuilder
{
    const string Title = "Assets/Scenes/Title.unity";
    const string Map0 = "Assets/Scenes/Map0_Town.unity";
    const string Map1 = "Assets/Scenes/Map1_Slime.unity";
    const string Map2 = "Assets/Scenes/Map2_Boar.unity";
    const string Map3 = "Assets/Scenes/Map3_Golem.unity";
    const string Map4 = "Assets/Scenes/Map4_SpiritForest.unity";
    const float FeetY = -4.0f;

    [MenuItem("Tools/Maps/시작 마을 + 정령의 숲 구성")]
    public static void Build()
    {
        foreach (var p in new[] { Map1, Map2, Map3 })
            if (!File.Exists(p))
            {
                EditorUtility.DisplayDialog("맵 구성", p + " 가 없습니다.\n먼저 'Tools > Maps > 맵 2개 구성하기'와 '맵 3 추가'를 실행하세요.", "확인");
                return;
            }
        if (!EditorUtility.DisplayDialog("맵 구성",
            "• Map0_Town : 시작 마을 (몬스터 없음, 게임 시작 지점) → 오른쪽 포탈 → 슬라임 맵\n" +
            "• Map4_SpiritForest : 정령의 숲 (골렘 맵 오른쪽 끝에서 이동)\n" +
            "• 타이틀 '시작하기' → 시작 마을로 변경\n\n진행할까요?", "만들기", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // ── Map1: 왼쪽 끝 → 시작 마을 ──
        var s1 = EditorSceneManager.OpenScene(Map1);
        var stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Left");
        Portal("Portal_Left (→ Map0)", stage.left + 0.7f, "Map0_Town", "Right");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s1);

        // ── Map0: Map1 복사 → 몬스터·포탈 제거, 오른쪽 끝 → 슬라임 맵 ──
        EditorSceneManager.SaveScene(s1, Map0, true);
        var s0 = EditorSceneManager.OpenScene(Map0);
        RemoveMonsters();
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        Portal("Portal_Right (→ Map1)", stage.right - 0.7f, "Map1_Slime", "Left");
        PlaceSeria(0f);
        EditorSceneManager.SaveScene(s0);

        // ── Map3: 오른쪽 끝 → 정령의 숲 ──
        var s3 = EditorSceneManager.OpenScene(Map3);
        stage = Find<StageBounds>()[0];
        RemovePortal("Portal_Right");
        Portal("Portal_Right (→ Map4)", stage.right - 0.7f, "Map4_SpiritForest", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s3);

        // ── Map4: Map3 복사 → 몬스터·포탈 제거, 배경을 정령의 숲으로 ──
        EditorSceneManager.SaveScene(s3, Map4, true);
        var s4 = EditorSceneManager.OpenScene(Map4);
        RemoveMonsters();
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        SwapBackground(stage.gameObject, "Forest Background", "Assets/Backgrounds/Forest/Forest_Far.png", "Assets/Backgrounds/Forest/Forest_Ground.png");
        Portal("Portal_Left (→ Map3)", stage.left + 0.7f, "Map3_Golem", "Right");
        PlaceSeria(stage.left + 2f);
        EditorSceneManager.SaveScene(s4);

        // ── 타이틀: 시작하기 → 시작 마을 ──
        if (File.Exists(Title))
        {
            var st = EditorSceneManager.OpenScene(Title);
            foreach (var ts in Find<TitleScreen>()) { ts.firstScene = "Map0_Town"; EditorUtility.SetDirty(ts); }
            EditorSceneManager.SaveScene(st);
        }

        // ── 빌드 목록: Title → Map0 → Map1 → Map2 → Map3 → Map4 → (그 외) ──
        var order = new[] { Title, Map0, Map1, Map2, Map3, Map4 };
        var list = order.Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        list.AddRange(EditorBuildSettings.scenes.Where(s => !order.Contains(s.path)));
        EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.OpenScene(Map4);
        EditorUtility.DisplayDialog("맵 구성 완료",
            "시작 마을(Map0)과 정령의 숲(Map4)이 만들어졌습니다.\n지금 정령의 숲이 열려 있습니다.\n\nTitle 씬에서 Play → '시작하기'로 확인하세요.", "확인");
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }

    static void RemoveMonsters()
    {
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
    }

    static void RemovePortal(string prefix)
    {
        foreach (var p in Find<MapPortal>()) if (p.name.StartsWith(prefix)) Object.DestroyImmediate(p.gameObject);
    }

    static void EnsureSpawns(StageBounds stage)
    {
        foreach (var (n, x) in new[] { ("Spawn_Left", stage.left + 2f), ("Spawn_Right", stage.right - 2f) })
            if (GameObject.Find(n) == null) new GameObject(n).transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }

    static void PlaceSeria(float x)
    {
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }

    static void Portal(string name, float x, string target, string side)
    {
        var g = new GameObject(name);
        g.transform.position = new Vector3(x, FeetY, 0);
        var box = g.AddComponent<BoxCollider2D>();
        box.isTrigger = true; box.size = new Vector2(1f, 4f); box.offset = new Vector2(0, 2f);
        var p = g.AddComponent<MapPortal>(); p.targetScene = target; p.spawnSide = side;
    }

    // 배경 그림 교체: 원경(ParallaxFit)과 바닥(타일) 스프라이트만 바꿈
    static void SwapBackground(GameObject root, string newName, string farPath, string groundPath)
    {
        root.name = newName;
        var far = AssetDatabase.LoadAssetAtPath<Sprite>(farPath);
        var ground = AssetDatabase.LoadAssetAtPath<Sprite>(groundPath);
        if (far == null || ground == null) { Debug.LogError("정령의 숲 배경 그림을 찾을 수 없습니다: Assets/Backgrounds/Forest/"); return; }
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.GetComponent<ParallaxFit>() != null) sr.sprite = far;
            else if (sr.drawMode == SpriteDrawMode.Tiled) { var size = sr.size; sr.sprite = ground; sr.drawMode = SpriteDrawMode.Tiled; sr.size = size; }
        }
    }
}
