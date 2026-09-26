// 최종 보스 아타칸: 그림 자동 설정 + 애니메이터 + 소환수 프리팹 + 왕좌의 간 배치
//  메뉴: Tools > Monsters > 왕좌의 간에 최종 보스 아타칸 배치
//        Tools > Monsters > 아타칸 대사 편집 열기
//  (늑대 · 나무정령 · 뱀파이어 맵과 왕좌의 간이 먼저 만들어져 있어야 함)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AtakhanImporter : AssetPostprocessor
{
    public const float PPU = 69f;          // 키 약 4.5유닛 (세리아의 약 3배)
    const int Foot = 24;

    static int Count(string f)
    {
        switch (f)
        {
            case "Atk_Idle": return 9;
            case "Atk_Walk": return 7;
            case "Atk_Run": return 8;
            case "Atk_Swing": return 7;
            case "Atk_Wave": return 4;
            case "Atk_Summon": return 9;
            case "Atk_Hit": return 3;
            case "Atk_Death": return 9;
            case "Atk_SummonFx": return 6;
        }
        return 0;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Atakhan/Atk_"), fx = p.Contains("/Monsters/Resources/Atakhan/"), por = p.EndsWith("/Portraits/Portrait_Atakhan.png");
        if (!sheet && !fx && !por) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
        string file = Path.GetFileNameWithoutExtension(p);
        if (por) { ti.spriteImportMode = SpriteImportMode.Single; return; }
        if (file == "Atk_WaveFx") { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 235f; return; }   // 높이 약 2.6유닛
        int n = Count(file); if (n == 0) return;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        int cw = w / n;
        bool isFx = file == "Atk_SummonFx";
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = isFx ? 150f : PPU;
        var pivot = isFx ? new Vector2(0.5f, 0.06f) : new Vector2(0.5f, (float)Foot / h);
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, h), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class AtakhanTools
{
    const string Dir = "Assets/Monsters/Atakhan/";
    const string Map12 = "Assets/Scenes/Map12_ThroneRoom.unity";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string sheet, string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Atk_{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Atk_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static AnimatorController BuildController()
    {
        string path = Dir + "Atakhan.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", "Idle", 7, true, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 6, 5, 4, 3, 2, 1 });
        sm.AddState("Walk").motion = Clip("Walk", "Walk", 8, true);
        sm.AddState("Run").motion = Clip("Run", "Run", 13, true);
        sm.AddState("SwingReady").motion = Clip("Swing", "SwingReady", 6, false, new[] { 0, 1, 2 });
        sm.AddState("SwingHit").motion = Clip("Swing", "SwingHit", 14, false, new[] { 3, 4, 5, 6 });
        sm.AddState("Wave").motion = Clip("Wave", "Wave", 9, false);
        sm.AddState("Summon").motion = Clip("Summon", "Summon", 9, false);
        var hit = sm.AddState("Hit"); hit.motion = Clip("Hit", "Hit", 10, false);
        sm.AddState("Death").motion = Clip("Death", "Death", 6, false);
        sm.defaultState = idle;
        var t = hit.AddTransition(idle); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // 이전 보스를 그 맵에서 그대로 복사해 소환수 프리팹으로 저장
    static GameObject MinionPrefab<T>(string scenePath, string file) where T : MonoBehaviour
    {
        string path = Dir + file + ".prefab";
        if (!File.Exists(scenePath)) { Debug.LogWarning("[아타칸] 소환수 원본 맵이 없습니다: " + scenePath); return AssetDatabase.LoadAssetAtPath<GameObject>(path); }
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        GameObject prefab = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var c = root.GetComponentInChildren<T>(true);
            if (c == null) continue;
            var copy = Object.Instantiate(c.gameObject);
            copy.SetActive(true);
            prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            break;
        }
        EditorSceneManager.CloseScene(scene, true);
        if (prefab == null) { Debug.LogWarning("[아타칸] " + scenePath + " 에서 " + typeof(T).Name + " 를 찾지 못했습니다."); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); }
        return prefab;
    }

    static Sprite Por => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Monsters/Resources/Portraits/Portrait_Atakhan.png");

    static MonsterTalkData TalkData()
    {
        const string dir = "Assets/Monsters/Resources/MonsterTalk", path = dir + "/AtakhanBoss.asset";
        var d = AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path);
        if (d != null) return d;
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        d = ScriptableObject.CreateInstance<MonsterTalkData>();
        d.monsterName = "아타칸"; d.portrait = Por; d.sightRange = 10f; d.oncePerGame = false;
        d.onSight = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 여기까지 올라온 인간은 네가 처음이다." },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 당신이… 이 왕국을 무너뜨린 아타칸이구나." },
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 무릎 꿇어라. 이 왕좌 앞에서는 모두가 그랬다." },
        };
        d.onDefeat = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 이 몸이… 무너지다니…" },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 끝났어. 이제 이 왕국에도 아침이 올 거야." },
        };
        AssetDatabase.CreateAsset(d, path); AssetDatabase.SaveAssets();
        return d;
    }

    [MenuItem("Tools/Monsters/아타칸 대사 편집 열기")]
    public static void EditTalk() { var d = TalkData(); Selection.activeObject = d; EditorGUIUtility.PingObject(d); }

    [MenuItem("Tools/Monsters/왕좌의 간에 최종 보스 아타칸 배치")]
    public static void Place()
    {
        if (!File.Exists(Map12)) { EditorUtility.DisplayDialog("아타칸", "왕좌의 간(Map12_ThroneRoom)이 없습니다.\n먼저 'Tools > Maps > 왕좌의 간 추가 (최종 보스 맵)'을 실행하세요.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        TalkData();
        var ctrl = BuildController();
        var scene = EditorSceneManager.OpenScene(Map12);

        var wolf = MinionPrefab<WolfBoss>("Assets/Scenes/Map3b_Town4_WolfBoss.unity", "Minion_Wolf");
        var tree = MinionPrefab<TreeBossEnemy>("Assets/Scenes/Map6_SpiritForest3_TreeBoss.unity", "Minion_TreeSpirit");
        var vamp = MinionPrefab<VampireBoss>("Assets/Scenes/Map11_RuinedCastle4_Vampire.unity", "Minion_Vampire");

#if UNITY_2023_1_OR_NEWER
        foreach (var o in Object.FindObjectsByType<AtakhanBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(o.gameObject);
        var stage = Object.FindFirstObjectByType<StageBounds>();
#else
        foreach (var o in Object.FindObjectsOfType<AtakhanBoss>()) Object.DestroyImmediate(o.gameObject);
        var stage = Object.FindObjectOfType<StageBounds>();
#endif
        float cx = stage != null ? (stage.left + stage.right) / 2f : 0f;

        var go = new GameObject("Atakhan (최종 보스)");
        go.transform.position = new Vector3(cx, FeetY + 0.05f, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Atk_Idle.png").OfType<Sprite>().OrderBy(s => s.name).FirstOrDefault();
        sr.sortingOrder = -1; sr.flipX = true;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 50f; rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(1.6f, 3.8f); body.offset = new Vector2(0, 1.9f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(1.9f, 3.8f); hit.offset = new Vector2(0, 1.9f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 3000f; hp.hpBarOffset = new Vector2(0, 4.9f); hp.deathAnimTime = 1.6f;
        hp.isBoss = true; hp.bossId = "Atakhan";
        var a = go.AddComponent<AtakhanBoss>(); a.expReward = 1000;
        a.wolfPrefab = wolf; a.treePrefab = tree; a.vampirePrefab = vamp;
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        string miss = (wolf == null ? " 늑대" : "") + (tree == null ? " 나무정령" : "") + (vamp == null ? " 뱀파이어" : "");
        EditorUtility.DisplayDialog("아타칸 배치 완료",
            "왕좌의 간 중앙에 최종 보스 아타칸을 배치했습니다.\n체력 3000 / 대검 = 세리아 최대 체력 20% / 검기 = 50% / 경험치 1000"
            + (miss.Length > 0 ? "\n\n※ 소환수 원본을 찾지 못함:" + miss + "\n  (해당 보스 맵을 먼저 만든 뒤 다시 실행하세요)" : ""), "확인");
    }
}
