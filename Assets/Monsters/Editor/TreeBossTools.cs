// 타락한 거대 나무정령: 스프라이트 설정 + 애니메이터 + 대사 파일 + 맵(정령의 숲3) 구성
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TreeBossImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/TreeBoss/TreeBoss_"), res = p.Contains("/Monsters/Resources/TreeBoss/");
        if (!sheet && !res) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        int cw, n; float ppu; Vector2 pivot;
        if (res) { n = 4; cw = w / n; ppu = 110; pivot = new Vector2(0.5f, 0.03f); }          // 뿌리: 높이 약 3유닛, 밑동 기준
        else { cw = 640; n = w / cw; ppu = 118; pivot = new Vector2(0.5f, 12f / 520f); }      // 보스: 키 약 4유닛
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = ppu;
        string file = Path.GetFileNameWithoutExtension(p);
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, h), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class TreeBossTools
{
    const string Dir = "Assets/Monsters/TreeBoss/";
    const string Map5 = "Assets/Scenes/Map5_SpiritForest2.unity";
    const string Map6 = "Assets/Scenes/Map6_SpiritForest3_TreeBoss.unity";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string name, float fps, bool loop)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{Dir}TreeBoss_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}TreeBoss_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static AnimatorController BuildController()
    {
        string path = Dir + "TreeBoss.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", 7, true);
        var walk = sm.AddState("Walk"); walk.motion = Clip("Walk", 6, true);
        var death = sm.AddState("Death"); death.motion = Clip("Death", 7, false);
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static GameObject Create(Vector3 pos)
    {
        var go = new GameObject("Corrupted Tree Spirit (Boss)");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = -1; sr.flipX = true;
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "TreeBoss_Idle.png").OfType<Sprite>().FirstOrDefault();
        go.AddComponent<Animator>().runtimeAnimatorController = BuildController();
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 50f; rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(3.2f, 3.8f); body.offset = new Vector2(0, 1.9f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(3f, 3.6f); hit.offset = new Vector2(0, 1.8f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 300f; hp.hpBarOffset = new Vector2(0, 4.3f); hp.deathAnimTime = 1.3f;
        hp.isBoss = true; hp.bossId = "CorruptedTreeSpirit";
        var ai = go.AddComponent<TreeBossEnemy>(); ai.rootDamage = 50f; ai.expReward = 200;
        Undo.RegisterCreatedObjectUndo(go, "Create Tree Boss");
        return go;
    }

    // 대사 파일 (없으면 예시 대사로 만듦)
    [MenuItem("Tools/Monsters/나무정령 보스 대사 편집 열기")]
    public static void EditTalk() { var d = TalkData(); Selection.activeObject = d; EditorGUIUtility.PingObject(d); }

    static MonsterTalkData TalkData()
    {
        const string dir = "Assets/Monsters/Resources/MonsterTalk", path = dir + "/TreeBossEnemy.asset";
        var d = AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path);
        if (d != null) return d;
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        d = ScriptableObject.CreateInstance<MonsterTalkData>();
        d.monsterName = "타락한 거대 나무정령";
        d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Monsters/Resources/Portraits/Portrait_TreeBoss.png");
        d.sightRange = 10f; d.oncePerGame = false;
        d.onSight = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) …누가… 숲의 잠을 깨우는가…" },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 이게… 숲을 지키던 정령이라고?" },
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 뿌리가… 모든 것을… 삼키리라…!" },
        };
        d.onDefeat = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 아아… 이제야… 숲의 목소리가… 들리는구나…" },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) …편히 쉬어." },
        };
        AssetDatabase.CreateAsset(d, path); AssetDatabase.SaveAssets();
        return d;
    }

    [MenuItem("Tools/Maps/정령의 숲3 추가 (중간 보스 나무정령)")]
    public static void BuildMap()
    {
        if (!File.Exists(Map5)) { EditorUtility.DisplayDialog("맵 추가", "정령의 숲2(Map5_SpiritForest2)가 없습니다.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("맵 추가", "정령의 숲2 오른쪽 끝 → 정령의 숲3 (중간 보스: 타락한 거대 나무정령)\n진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        TalkData();

        var s5 = EditorSceneManager.OpenScene(Map5);
        var stage = Find<StageBounds>()[0];
        foreach (var p in Find<MapPortal>()) if (p.name.StartsWith("Portal_Right")) Object.DestroyImmediate(p.gameObject);
        Portal("Portal_Right (→ 정령의 숲3)", stage.right - 0.7f, "Map6_SpiritForest3_TreeBoss", "Left");
        EnsureSpawns(stage);
        EditorSceneManager.SaveScene(s5);

        EditorSceneManager.SaveScene(s5, Map6, true);
        var s6 = EditorSceneManager.OpenScene(Map6);
        foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
        foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
        stage = Find<StageBounds>()[0];
        Create(new Vector3(10f, FeetY + 0.05f, 0));
        Portal("Portal_Left (→ 정령의 숲2)", stage.left + 0.7f, "Map5_SpiritForest2", "Right");
        var seria = GameObject.Find("Seria");
        if (seria != null) seria.transform.position = new Vector3(stage.left + 2f, FeetY + 0.05f, 0);
        EditorSceneManager.SaveScene(s6);

        var list = EditorBuildSettings.scenes.Where(s => s.path != Map6).ToList();
        int i5 = list.FindIndex(s => s.path == Map5);
        list.Insert(i5 >= 0 ? i5 + 1 : list.Count, new EditorBuildSettingsScene(Map6, true));
        EditorBuildSettings.scenes = list.ToArray();
        EditorUtility.DisplayDialog("맵 추가 완료", "정령의 숲3(중간 보스)이 만들어졌습니다. 지금 열려 있습니다.\n대사: Tools > Monsters > 나무정령 보스 대사 편집 열기", "확인");
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
