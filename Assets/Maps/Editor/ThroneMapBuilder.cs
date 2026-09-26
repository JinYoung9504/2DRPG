// 메뉴: Tools > Maps > 왕좌의 간 추가 (최종 보스 맵)
//  성곽4(뱀파이어) 오른쪽 끝 → 왕좌의 간 (최종 보스 맵, 몬스터는 이후 추가)
//  배경 3겹: 원경(피의 달·성) → 중경(왕좌의 간 기둥) → 전경(바닥·붉은 카펫)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ThroneImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Maps/ThroneRoom/Throne_")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
        if (p.EndsWith("Throne_Far.png"))   { ti.spritePixelsPerUnit = 93.2f;  set.spriteAlignment = (int)SpriteAlignment.Center; }        // 높이 11유닛 (성 부분 5유닛)
        if (p.EndsWith("Throne_Mid.png"))   { ti.spritePixelsPerUnit = 114.6f; set.spriteAlignment = (int)SpriteAlignment.BottomCenter; }  // 높이 5.6유닛
        if (p.EndsWith("Throne_Front.png")) { ti.spritePixelsPerUnit = 110.2f; set.spriteAlignment = (int)SpriteAlignment.BottomCenter; }  // 폭 36유닛 (맵 전체)
        ti.SetTextureSettings(set);
    }
}

public static class ThroneMapBuilder
{
    const string Prev = "Assets/Scenes/Map11_RuinedCastle4_Vampire.unity";
    const string Map12 = "Assets/Scenes/Map12_ThroneRoom.unity";
    const string Dir = "Assets/Maps/ThroneRoom/";
    const float FeetY = -4.0f;
    const float Half = 17.5f;                  // 보스전 맵: 화면 약 2개 폭 (-17.5 ~ 17.5)

    [MenuItem("Tools/Maps/왕좌의 간 추가 (최종 보스 맵)")]
    public static void Build()
    {
        if (!File.Exists(Prev)) { EditorUtility.DisplayDialog("맵 추가", "성곽4(Map11_RuinedCastle4_Vampire)가 없습니다.\n먼저 'Tools > Maps > 성곽4 추가 (중간 보스 뱀파이어)'를 실행하세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("맵 추가", "성곽4(뱀파이어) 오른쪽 끝 → 왕좌의 간 (최종 보스 맵)\n진행할까요?" + (File.Exists(Map12) ? "\n\n※ 이미 있는 왕좌의 간은 새로 만들어집니다." : ""), "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 성곽4: 오른쪽 → 왕좌의 간
        var s11 = EditorSceneManager.OpenScene(Prev);
        var st11 = Find<StageBounds>()[0];
        foreach (var p in Find<MapPortal>()) if (p.name.StartsWith("Portal_Right")) Object.DestroyImmediate(p.gameObject);
        Portal("Portal_Right (→ 왕좌의 간)", st11.right - 0.7f, "Map12_ThroneRoom", "Left");
        EnsureSpawn("Spawn_Right", st11.right - 2f);
        EditorSceneManager.SaveScene(s11);

        // 왕좌의 간: 성곽4 복사 → 몬스터·포탈 정리, 배경 교체
        EditorSceneManager.SaveScene(s11, Map12, true);
        var s12 = EditorSceneManager.OpenScene(Map12);
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
        foreach (var n in Find<NpcTalker>()) Object.DestroyImmediate(n.gameObject);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);

        var stage = Find<StageBounds>()[0];
        stage.left = -Half; stage.right = Half;
        if (!BuildBackground(stage.gameObject)) return;

        Portal("Portal_Left (→ 성곽4)", stage.left + 0.7f, "Map11_RuinedCastle4_Vampire", "Right");
        EnsureSpawn("Spawn_Left", stage.left + 2f);
        EnsureSpawn("Spawn_Right", stage.right - 2f);
        var seria = GameObject.Find("Seria"); if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);
        EditorSceneManager.SaveScene(s12);

        var list = EditorBuildSettings.scenes.Where(s => s.path != Map12).ToList();
        int i = list.FindIndex(s => s.path == Prev);
        list.Insert(i >= 0 ? i + 1 : list.Count, new EditorBuildSettingsScene(Map12, true));
        EditorBuildSettings.scenes = list.ToArray();
        EditorUtility.DisplayDialog("맵 추가 완료", "왕좌의 간(Map12, 최종 보스 맵)이 만들어졌습니다. 지금 열려 있습니다.\n최종 보스는 이후 추가합니다.", "확인");
    }

    static bool BuildBackground(GameObject root)
    {
        var far = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "Throne_Far.png");
        var mid = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "Throne_Mid.png");
        var front = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "Throne_Front.png");
        if (far == null || mid == null || front == null) { EditorUtility.DisplayDialog("맵 추가", "왕좌의 간 배경 그림을 찾을 수 없습니다: " + Dir, "확인"); return false; }

        root.name = "Throne Background";
        // 기존 성곽 배경(원경·타일 바닥)은 끄기
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.GetComponent<ParallaxFit>() != null || sr.drawMode == SpriteDrawMode.Tiled) sr.gameObject.SetActive(false);
        foreach (Transform c in root.transform.Cast<Transform>().ToArray())
            if (c.name.StartsWith("Throne ")) Object.DestroyImmediate(c.gameObject);
        // 양쪽 벽을 새 맵 끝으로
        foreach (Transform c in root.transform)
            if (c.name == "Wall") c.position = new Vector3(c.position.x < 0 ? -Half - 0.5f : Half + 0.5f, c.position.y, c.position.z);

        var cam = Camera.main;
        Layer(root, "Throne Far (원경)", far, new Vector3(0, 1f, 12), -110, 0.85f, cam);         // 거의 화면에 붙어 아주 천천히
        Layer(root, "Throne Mid (중경)", mid, new Vector3(0, -3.3f, 8), -80, 0.55f, cam);       // 중간 속도
        Layer(root, "Throne Front (전경)", front, new Vector3(0, -5f, 5), -50, 0f, cam);        // 바닥: 캐릭터와 같이 움직임
        if (cam != null) cam.backgroundColor = new Color(0.08f, 0.02f, 0.04f);
        return true;
    }

    static void Layer(GameObject root, string name, Sprite s, Vector3 pos, int order, float follow, Camera cam)
    {
        var g = new GameObject(name); g.transform.SetParent(root.transform, false); g.transform.position = pos;
        var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order;
        if (follow > 0f) { var b = g.AddComponent<ParallaxBand>(); b.follow = follow; b.cam = cam; }
    }

    static T[] Find<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<T>();
#endif
    }
    static void EnsureSpawn(string n, float x)
    {
        var g = GameObject.Find(n); if (g == null) g = new GameObject(n);
        g.transform.position = new Vector3(x, FeetY + 0.05f, 0);
    }
    static void Portal(string name, float x, string target, string side)
    {
        var g = new GameObject(name); g.transform.position = new Vector3(x, FeetY, 0);
        var box = g.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(1f, 4f); box.offset = new Vector2(0, 2f);
        var p = g.AddComponent<MapPortal>(); p.targetScene = target; p.spawnSide = side;
    }
}
