// 골렘 스프라이트 자동 설정 + 메뉴: Tools > Monsters > 골렘 배치
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class GolemImporter : AssetPostprocessor
{
    // 파일 이름: (칸 가로, 칸 세로, PPU, 피벗)
    static bool Info(string file, out int cw, out int ch, out float ppu, out Vector2 pivot)
    {
        cw = ch = 0; ppu = 100; pivot = new Vector2(0.5f, 0.5f);
        switch (file)
        {
            case "Golem_Idle": case "Golem_Walk": case "Golem_Throw":
                cw = CELL_W; ch = CELL_H; ppu = 100; pivot = new Vector2(0.5f, 8f / CELL_H); return true;   // 발 기준
            case "Golem_Rock":   cw = ROCK; ch = IMP_H; ppu = ROCK_PPU; pivot = new Vector2(0.5f, 0.5f); return true;
            case "Golem_Impact": cw = IMP_W; ch = IMP_H; ppu = IMP_PPU; pivot = new Vector2(0.5f, 0.05f); return true;
        }
        return false;
    }
    public const int CELL_W = 420, CELL_H = 270, ROCK = 50, IMP_W = 64, IMP_H = 106;
    public const float ROCK_PPU = 75f, IMP_PPU = 42f;

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Monsters/Golem/") && !p.Contains("/Monsters/Resources/Golem/")) return;
        string file = Path.GetFileNameWithoutExtension(p);
        if (!Info(file, out int cw, out int ch, out float ppu, out Vector2 pivot)) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = ppu; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int n = w / cw;
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * cw, 0, cw, ch), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class GolemTools
{
    const string Dir = "Assets/Monsters/Golem/";
    const float FeetY = -4.0f;

    static AnimationClip MakeClip(string name, float fps, bool loop)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Golem_{name}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Golem_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static AnimatorController BuildController()
    {
        string path = Dir + "Golem.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = MakeClip("Idle", 6, true);
        var walk = sm.AddState("Walk"); walk.motion = MakeClip("Walk", 6, true);      // 매우 느린 걸음
        var thr = sm.AddState("Throw"); thr.motion = MakeClip("Throw", 8, false);
        sm.defaultState = idle;
        var t1 = idle.AddTransition(walk); t1.hasExitTime = false; t1.duration = 0; t1.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
        var t2 = walk.AddTransition(idle); t2.hasExitTime = false; t2.duration = 0; t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
        foreach (var s in new[] { idle, walk }) { var t = s.AddTransition(thr); t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.If, 0, "Throw"); }
        var back = thr.AddTransition(idle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    public static GameObject CreateGolem(Vector3 pos, AnimatorController ctrl)
    {
        var go = new GameObject("Golem");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Golem_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1;
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 10f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(1.6f, 2.2f); body.offset = new Vector2(0, 1.1f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(1.5f, 2.1f); hit.offset = new Vector2(0, 1.05f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 30f; hp.hpBarOffset = new Vector2(0, 2.7f);
        var ge = go.AddComponent<GolemEnemy>(); ge.expReward = 15; ge.touchDamage = 5f; ge.rockDamage = 10f;
        Undo.RegisterCreatedObjectUndo(go, "Create Golem");
        return go;
    }

    [MenuItem("Tools/Monsters/골렘 배치 (Spawn Golems)")]
    public static void SpawnGolems() => SpawnAt(new[] { 6f, 20f });

    public static void SpawnAt(float[] xs)
    {
#if UNITY_2023_1_OR_NEWER
        var olds = Object.FindObjectsByType<GolemEnemy>(FindObjectsSortMode.None);
#else
        var olds = Object.FindObjectsOfType<GolemEnemy>();
#endif
        foreach (var o in olds) Undo.DestroyObjectImmediate(o.gameObject);
        var ctrl = BuildController();
        foreach (var x in xs) CreateGolem(new Vector3(x, FeetY + 0.05f, 0), ctrl);
        Debug.Log($"골렘 {xs.Length}마리 배치 완료.");
    }

    [MenuItem("Tools/Monsters/선택 위치에 골렘 1마리 추가")]
    public static void AddOne()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "Golem.controller");
        if (ctrl == null) ctrl = BuildController();
        var view = SceneView.lastActiveSceneView;
        Selection.activeGameObject = CreateGolem(new Vector3(view != null ? view.pivot.x : 0f, FeetY + 0.05f, 0), ctrl);
    }
}
