// 스켈레톤 궁수: 스프라이트 자동 설정 + 애니메이터 + 성곽2 맵 만들기/배치
//  메뉴: Tools > Monsters > 성곽2 만들기 + 스켈레톤 궁수 배치
//        Tools > Monsters > 선택 위치에 스켈레톤 궁수 1마리 추가
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SkeletonArcherImporter : AssetPostprocessor
{
    public const float PPU = 130f;     // 검사 스켈레톤과 같은 크기
    const int Foot = 8;

    static int Count(string file)
    {
        switch (file)
        {
            case "Archer_Idle": return 3;
            case "Archer_Walk": return 6;
            case "Archer_Attack": return 6;
            case "Archer_Hit": return 3;
            case "Archer_Death": return 4;
        }
        return 0;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Archer/Archer_"), arrow = p.EndsWith("/Resources/Archer/Archer_Arrow.png");
        bool portrait = p.EndsWith("/Portraits/Portrait_SkeletonArcher.png");
        if (!sheet && !arrow && !portrait) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        if (portrait) { ti.spriteImportMode = SpriteImportMode.Single; return; }
        if (arrow) { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = PPU; return; }   // 가운데 기준

        string file = Path.GetFileNameWithoutExtension(p);
        int n = Count(file); if (n == 0) return;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        int cw = w / n;
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = PPU;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, h), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, (float)Foot / h) };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class SkeletonArcherTools
{
    const string Dir = "Assets/Monsters/Archer/";
    const string Map8 = "Assets/Scenes/Map8_RuinedCastle1.unity";
    const string Map9 = "Assets/Scenes/Map9_RuinedCastle2.unity";
    const string TalkDir = "Assets/Monsters/Resources/MonsterTalk";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string sheet, string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Archer_{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Archer_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "SkeletonArcher.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", "Idle", 5, true, new[] { 0, 1, 2, 1 });
        sm.AddState("Walk").motion = Clip("Walk", "Walk", 10, true);
        sm.AddState("AttackDraw").motion = Clip("Attack", "AttackDraw", 12, false, new[] { 0, 1, 2, 3, 4 });
        sm.AddState("AttackRelease").motion = Clip("Attack", "AttackRelease", 10, false, new[] { 5 });
        sm.AddState("Hit").motion = Clip("Hit", "Hit", 10, false);
        sm.AddState("Death").motion = Clip("Death", "Death", 8, false);
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject Create(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Skeleton Archer");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Archer_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 3f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(0.8f, 1.35f); body.offset = new Vector2(0, 0.68f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(0.9f, 1.35f); hit.offset = new Vector2(0, 0.68f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 125f; hp.hpBarOffset = new Vector2(0, 1.7f); hp.deathAnimTime = 0.9f;
        var a = go.AddComponent<SkeletonArcherEnemy>(); a.arrowDamage = 25f; a.expReward = 100;
        Undo.RegisterCreatedObjectUndo(go, "Create Skeleton Archer");
        return go;
    }

    static void EnsureTalk()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Monsters/Resources")) AssetDatabase.CreateFolder("Assets/Monsters", "Resources");
        if (!AssetDatabase.IsValidFolder(TalkDir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        string path = TalkDir + "/SkeletonArcherEnemy.asset";
        if (AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path) != null) return;
        var d = ScriptableObject.CreateInstance<MonsterTalkData>();
        d.monsterName = "스켈레톤 궁수";
        d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Monsters/Resources/Portraits/Portrait_SkeletonArcher.png");
        d.sightRange = 9f;
        AssetDatabase.CreateAsset(d, path); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Monsters/성곽2 만들기 + 스켈레톤 궁수 배치")]
    public static void BuildCastle2()
    {
        if (!File.Exists(Map8)) { EditorUtility.DisplayDialog("성곽2", "멸망한 왕국 성곽1(Map8_RuinedCastle1)이 없습니다.", "확인"); return; }
        bool exists = File.Exists(Map9);
        if (!EditorUtility.DisplayDialog("성곽2", exists
                ? "성곽2(Map9)가 이미 있습니다.\n스켈레톤 궁수만 다시 배치할까요?"
                : "성곽1 오른쪽 끝 → 멸망한 왕국 성곽2 (새 맵)\n스켈레톤 궁수를 배치합니다. 진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureTalk();
        var ctrl = BuildController();

        if (!exists)
        {
            // 성곽1: 오른쪽 → 성곽2
            var s8 = EditorSceneManager.OpenScene(Map8);
            var stage8 = Find<StageBounds>()[0];
            foreach (var p in Find<MapPortal>()) if (p.name.StartsWith("Portal_Right")) Object.DestroyImmediate(p.gameObject);
            Portal("Portal_Right (→ 성곽2)", stage8.right - 0.7f, "Map9_RuinedCastle2", "Left");
            EnsureSpawns(stage8);
            EditorSceneManager.SaveScene(s8);

            // 성곽2: 성곽1 복사 → 몬스터·포탈 정리
            EditorSceneManager.SaveScene(s8, Map9, true);
            var s9 = EditorSceneManager.OpenScene(Map9);
            foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
            foreach (var n in Find<NpcTalker>()) Object.DestroyImmediate(n.gameObject);
            foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
            var stage9 = Find<StageBounds>()[0];
            Portal("Portal_Left (→ 성곽1)", stage9.left + 0.7f, "Map8_RuinedCastle1", "Right");
            EnsureSpawns(stage9);
            var seria = GameObject.Find("Seria"); if (seria != null) seria.transform.position = new Vector3(stage9.left + 2f, FeetY + 0.05f, 0);
            EditorSceneManager.SaveScene(s9);

            // 빌드 목록: 성곽1 바로 뒤
            var list = EditorBuildSettings.scenes.Where(s => s.path != Map9).ToList();
            int i = list.FindIndex(s => s.path == Map8);
            list.Insert(i >= 0 ? i + 1 : list.Count, new EditorBuildSettingsScene(Map9, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        var scene = EditorSceneManager.OpenScene(Map9);
        foreach (var o in Find<SkeletonArcherEnemy>()) Object.DestroyImmediate(o.gameObject);
        var stage = Find<StageBounds>()[0];
        var xs = new[] { 0.4f, 0.65f, 0.88f }.Select(k => Mathf.Lerp(stage.left, stage.right, k)).ToArray();   // 3마리
        foreach (var x in xs) Create(new Vector3(x, FeetY, 0), ctrl);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("성곽2 완료", $"멸망한 왕국 성곽2(Map9)에 스켈레톤 궁수 {xs.Length}마리를 배치했습니다.\n(x = {string.Join(", ", xs.Select(x => x.ToString("0.#")))})\n\n체력 125 / 공격력 25 / 경험치 100", "확인");
    }

    [MenuItem("Tools/Monsters/선택 위치에 스켈레톤 궁수 1마리 추가")]
    public static void AddOne()
    {
        EnsureTalk();
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "SkeletonArcher.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = Create(new Vector3(view != null ? view.pivot.x : 0f, FeetY, 0), ctrl);
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
