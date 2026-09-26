// 스켈레톤 병사: 스프라이트 자동 설정 + 애니메이터 생성 + 성곽1 배치
//  메뉴: Tools > Monsters > 성곽1에 스켈레톤 배치
//        Tools > Monsters > 선택 위치에 스켈레톤 1마리 추가
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SkeletonImporter : AssetPostprocessor
{
    public const float PPU = 130f;     // 키 약 1.45유닛 (세리아와 비슷)
    const int Foot = 8;

    static int Count(string file)
    {
        switch (file)
        {
            case "Skeleton_Idle": return 4;
            case "Skeleton_Walk": return 9;
            case "Skeleton_Attack": return 5;
            case "Skeleton_Hit": return 2;
            case "Skeleton_Death": return 11;
        }
        return 0;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Skeleton/Skeleton_"), portrait = p.EndsWith("/Portraits/Portrait_Skeleton.png");
        if (!sheet && !portrait) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
        if (portrait) { ti.spriteImportMode = SpriteImportMode.Single; return; }

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

public static class SkeletonTools
{
    const string Dir = "Assets/Monsters/Skeleton/";
    const string Map8 = "Assets/Scenes/Map8_RuinedCastle1.unity";
    const string TalkDir = "Assets/Monsters/Resources/MonsterTalk";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string sheet, string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Skeleton_{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Skeleton_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Skeleton.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", "Idle", 5, true, new[] { 0, 1, 2, 3, 2, 1 });
        var walk = sm.AddState("Walk"); walk.motion = Clip("Walk", "Walk", 12, true);
        var ready = sm.AddState("AttackReady"); ready.motion = Clip("Attack", "AttackReady", 10, false, new[] { 0 });
        var swing = sm.AddState("AttackSwing"); swing.motion = Clip("Attack", "AttackSwing", 14, false, new[] { 1, 2, 3, 4 });
        var hit = sm.AddState("Hit"); hit.motion = Clip("Hit", "Hit", 10, false);
        var death = sm.AddState("Death"); death.motion = Clip("Death", "Death", 10, false);
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject Create(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Skeleton Soldier");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Skeleton_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 3f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(0.8f, 1.4f); body.offset = new Vector2(0, 0.7f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(0.9f, 1.4f); hit.offset = new Vector2(0, 0.7f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 150f; hp.hpBarOffset = new Vector2(0, 1.75f); hp.deathAnimTime = 1.2f;
        var sk = go.AddComponent<SkeletonEnemy>(); sk.slashDamage = 40f; sk.expReward = 50;
        Undo.RegisterCreatedObjectUndo(go, "Create Skeleton");
        return go;
    }

    // 몬스터 대사 파일 (비어 있으면 대화창 안 나옴)
    static void EnsureTalk()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Monsters/Resources")) AssetDatabase.CreateFolder("Assets/Monsters", "Resources");
        if (!AssetDatabase.IsValidFolder(TalkDir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        string path = TalkDir + "/SkeletonEnemy.asset";
        if (AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path) != null) return;
        var d = ScriptableObject.CreateInstance<MonsterTalkData>();
        d.monsterName = "스켈레톤 병사";
        d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Monsters/Resources/Portraits/Portrait_Skeleton.png");
        AssetDatabase.CreateAsset(d, path); AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Monsters/성곽1에 스켈레톤 배치")]
    public static void PlaceInCastle1()
    {
        if (!File.Exists(Map8)) { EditorUtility.DisplayDialog("스켈레톤 배치", "멸망한 왕국 성곽1(Map8_RuinedCastle1)이 없습니다.\n먼저 'Tools > Maps > 멸망한 왕국 성곽 추가'를 실행하세요.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureTalk();
        var ctrl = BuildController();
        var scene = EditorSceneManager.OpenScene(Map8);
#if UNITY_2023_1_OR_NEWER
        foreach (var o in Object.FindObjectsByType<SkeletonEnemy>(FindObjectsSortMode.None)) Object.DestroyImmediate(o.gameObject);
        var stage = Object.FindFirstObjectByType<StageBounds>();
#else
        foreach (var o in Object.FindObjectsOfType<SkeletonEnemy>()) Object.DestroyImmediate(o.gameObject);
        var stage = Object.FindObjectOfType<StageBounds>();
#endif
        float l = stage != null ? stage.left : -20f, r = stage != null ? stage.right : 20f;
        var xs = new[] { 0.35f, 0.6f, 0.85f }.Select(k => Mathf.Lerp(l, r, k)).ToArray();   // 3마리
        foreach (var x in xs) Create(new Vector3(x, FeetY, 0), ctrl);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("스켈레톤 배치 완료", $"성곽1에 스켈레톤 병사 {xs.Length}마리를 배치했습니다.\n(x = {string.Join(", ", xs.Select(x => x.ToString("0.#")))})\n\n체력 150 / 공격력 40 / 경험치 50", "확인");
    }

    [MenuItem("Tools/Monsters/선택 위치에 스켈레톤 1마리 추가")]
    public static void AddOne()
    {
        EnsureTalk();
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "Skeleton.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = Create(new Vector3(view != null ? view.pivot.x : 0f, FeetY, 0), ctrl);
    }
}
