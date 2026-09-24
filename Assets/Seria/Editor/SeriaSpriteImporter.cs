// 세리아 스프라이트 자동 설정 + 애니메이션/Animator 자동 생성 도구
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class SeriaSpriteImporter : AssetPostprocessor
{
    public const int CellW = 256, CellH = 160;
    public const float PPU = 100f;          // 캐릭터 키 약 1.4 유닛
    const float PivotY = 8f / CellH;        // 발 위치

    static bool IsSheet(string p) => p.Replace('\\', '/').Contains("/Seria/Sprites/Seria_");
    static bool IsFx(string p) { p = p.Replace('\\', '/'); return p.Contains("/Seria/Effects/") || p.Contains("/Seria/UI/"); }

    void OnPreprocessTexture()
    {
        if (!IsSheet(assetPath) && !IsFx(assetPath)) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = PPU;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;

        if (IsFx(assetPath)) { ti.spriteImportMode = SpriteImportMode.Single; return; }

        ti.spriteImportMode = SpriteImportMode.Multiple;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(assetPath)) fs.Read(head, 0, 24);
        int width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        int count = width / CellW;
        var metas = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
            metas[i] = new SpriteMetaData
            {
                name = $"{baseName}_{i}",
                rect = new Rect(i * CellW, 0, CellW, CellH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, PivotY)
            };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }

    static readonly (string name, float fps, bool loop)[] Anims =
    {
        ("Idle", 8, true), ("Walk", 12, true), ("Jump", 12, false), ("Fall", 10, true),
        ("Attack1", 14, false), ("Attack2", 14, false), ("Skill", 14, false),
        ("Hit", 12, false), ("Die", 8, false),
    };

    [MenuItem("Tools/Seria/애니메이션 만들기 (Build Animations)")]
    public static void BuildAnimations()
    {
        const string root = "Assets/Seria";
        string animDir = root + "/Animations";
        if (!AssetDatabase.IsValidFolder(animDir)) AssetDatabase.CreateFolder(root, "Animations");

        string ctrlPath = animDir + "/Seria.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VelY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        foreach (var t in new[] { "Attack", "Heavy", "Skill", "Hit", "Die" })
            ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var st = new Dictionary<string, AnimatorState>();

        foreach (var a in Anims)
        {
            string sheet = $"{root}/Sprites/Seria_{a.name}.png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>()
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
            if (sprites.Length == 0) { Debug.LogError("스프라이트 없음: " + sheet); continue; }

            var clip = new AnimationClip { frameRate = a.fps };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / a.fps, value = sprites[i] };
            keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / a.fps, value = sprites[sprites.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var set = AnimationUtility.GetAnimationClipSettings(clip);
            set.loopTime = a.loop;
            AnimationUtility.SetAnimationClipSettings(clip, set);
            string clipPath = $"{animDir}/Seria_{a.name}.anim";
            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);

            var s2 = sm.AddState(a.name); s2.motion = clip; st[a.name] = s2;
        }
        sm.defaultState = st["Idle"];

        AnimatorStateTransition T(string from, string to, bool exitTime = false)
        {
            var t = st[from].AddTransition(st[to]);
            t.hasExitTime = exitTime; t.exitTime = 1f; t.duration = 0f;
            return t;
        }
        var G = AnimatorConditionMode.If; var NG = AnimatorConditionMode.IfNot;

        T("Idle", "Walk").AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        T("Walk", "Idle").AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        foreach (var s in new[] { "Idle", "Walk" })
        {
            var j = T(s, "Jump"); j.AddCondition(NG, 0, "Grounded"); j.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VelY");
            var f = T(s, "Fall"); f.AddCondition(NG, 0, "Grounded"); f.AddCondition(AnimatorConditionMode.Less, -0.1f, "VelY");
        }
        T("Jump", "Fall").AddCondition(AnimatorConditionMode.Less, -0.1f, "VelY");
        T("Jump", "Idle").AddCondition(G, 0, "Grounded");
        T("Fall", "Idle").AddCondition(G, 0, "Grounded");

        foreach (var s in new[] { "Idle", "Walk", "Jump", "Fall" })
        {
            T(s, "Attack1").AddCondition(G, 0, "Attack");
            T(s, "Attack2").AddCondition(G, 0, "Heavy");
            T(s, "Skill").AddCondition(G, 0, "Skill");
        }
        var combo = T("Attack1", "Attack2", true); combo.exitTime = 0.6f; combo.AddCondition(G, 0, "Attack");
        foreach (var s in new[] { "Attack1", "Attack2", "Skill", "Hit" }) T(s, "Idle", true);

        var anyHit = sm.AddAnyStateTransition(st["Hit"]); anyHit.duration = 0; anyHit.canTransitionToSelf = false; anyHit.AddCondition(G, 0, "Hit");
        var anyDie = sm.AddAnyStateTransition(st["Die"]); anyDie.duration = 0; anyDie.canTransitionToSelf = false; anyDie.AddCondition(G, 0, "Die");
        AssetDatabase.SaveAssets();

        var go = new GameObject("Seria");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath($"{root}/Sprites/Seria_Idle.png").OfType<Sprite>().FirstOrDefault();
        go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f;
        var col = go.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.6f, 1.3f); col.offset = new Vector2(0, 0.65f);
        var ctl = go.AddComponent<SeriaController>();
        ctl.slashFx = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Effects/Seria_FX_Slash_Crescent.png");
        ctl.skillFx = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Effects/Seria_FX_Burst_Large_A.png");
        Selection.activeGameObject = go;
        Debug.Log("세리아 애니메이션 생성 완료! 씬에 'Seria' 오브젝트가 추가되었습니다.");
    }
}
