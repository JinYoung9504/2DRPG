// 대형 마수 늑대: 새 SD 그림 자동 설정 + 애니메이터 + 배치
//  메뉴: Tools > Monsters > 늑대 보스 새 그림 적용 (시작의 마을4)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WolfImporter : AssetPostprocessor
{
    public const float PPU = 72f;           // 몸 높이 약 5유닛 (화면 세로의 절반)
    const int Foot = 32;                    // 그림 아래 여백(발 위치)

    public static int Count(string file)
    {
        switch (file)
        {
            case "Wolf_Idle": return 7;
            case "Wolf_Walk": return 11;
            case "Wolf_Run": return 8;
            case "Wolf_Hit": return 7;
            case "Wolf_Death": return 11;
            case "Wolf_Charge": return 5;
            case "Wolf_Claw": return 12;
        }
        return 0;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Wolf/Wolf_"), res = p.Contains("/Monsters/Resources/Wolf/");
        if (!sheet && !res) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
        if (res) { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 96; return; }
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

public static class WolfTools
{
    const string Dir = "Assets/Monsters/Wolf/";
    const string Map = "Assets/Scenes/Map3b_Town4_WolfBoss.unity";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string sheet, string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Wolf_{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Wolf_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Wolf_SD.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", "Idle", 8, true, new[] { 0, 1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1 });   // 숨쉬듯 왕복
        sm.AddState("Walk").motion = Clip("Walk", "Walk", 11, true);
        sm.AddState("Run").motion = Clip("Run", "Run", 14, true);
        sm.AddState("ChargeReady").motion = Clip("Charge", "ChargeReady", 5, false, new[] { 0, 1 });
        sm.AddState("ChargeRun").motion = Clip("Charge", "ChargeRun", 14, true, new[] { 2, 3, 4, 3 });
        sm.AddState("Claw").motion = Clip("Claw", "Claw", 18, false);
        var hit = sm.AddState("Hit"); hit.motion = Clip("Hit", "Hit", 16, false);
        sm.AddState("Death").motion = Clip("Death", "Death", 10, false);
        sm.defaultState = idle;
        var t = hit.AddTransition(idle); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static void Apply(GameObject go, AnimatorController ctrl)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Wolf_Idle.png").OfType<Sprite>().OrderBy(s => s.name).FirstOrDefault();
        var an = go.GetComponent<Animator>(); if (an == null) an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        var hp = go.GetComponent<EnemyHealth>(); if (hp != null) hp.deathAnimTime = 1.1f;
    }

    public static GameObject Create(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Crimson Wolf (Boss)");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = -1; sr.flipX = true;
        go.AddComponent<Animator>();
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 20f; rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.direction = CapsuleDirection2D.Horizontal; body.size = new Vector2(4.2f, 3.4f); body.offset = new Vector2(0, 1.7f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(3.8f, 3.2f); hit.offset = new Vector2(0, 1.6f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 150f; hp.hpBarOffset = new Vector2(0, 5.4f); hp.respawnTime = 30f;
        go.AddComponent<WolfBoss>();
        Apply(go, ctrl);
        Undo.RegisterCreatedObjectUndo(go, "Create Wolf Boss");
        return go;
    }

    public static void SpawnAt(float x) => Create(new Vector3(x, FeetY + 0.05f, 0), BuildController());

    [MenuItem("Tools/Monsters/늑대 보스 새 그림 적용 (시작의 마을4)")]
    public static void Upgrade()
    {
        if (!File.Exists(Map)) { EditorUtility.DisplayDialog("늑대 보스", "시작의 마을4(Map3b_Town4_WolfBoss)가 없습니다.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var ctrl = BuildController();
        var scene = EditorSceneManager.OpenScene(Map);
#if UNITY_2023_1_OR_NEWER
        var wolves = Object.FindObjectsByType<WolfBoss>(FindObjectsSortMode.None);
#else
        var wolves = Object.FindObjectsOfType<WolfBoss>();
#endif
        foreach (var w in wolves) { Apply(w.gameObject, ctrl); EditorUtility.SetDirty(w.gameObject); }
        if (wolves.Length == 0) SpawnAt(8f);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("늑대 보스", "시작의 마을4의 늑대 보스에 새 SD 그림을 적용했습니다.", "확인");
    }
}
