// 뱀파이어 중간 보스: 스프라이트 자동 설정 + 애니메이터 + 성곽4(보스 맵) 만들기
//  메뉴: Tools > Maps > 성곽4 추가 (중간 보스 뱀파이어)
//        Tools > Monsters > 뱀파이어 보스 대사 편집 열기
//  (스켈레톤 검사 · 궁수 · 네크로맨서 업데이트가 먼저 적용되어 있어야 함)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class VampireImporter : AssetPostprocessor
{
    public const float PPU = 64f;      // 키 약 2.3유닛 (세리아의 약 1.6배)
    const int Foot = 8;

    static int Count(string file)
    {
        switch (file)
        {
            case "Vamp_Idle": return 3;
            case "Vamp_Walk": return 11;
            case "Vamp_Melee": return 5;
            case "Vamp_Cast": return 4;
            case "Vamp_Summon": return 4;
            case "Vamp_Hit": return 2;
            case "Vamp_Death": return 6;
        }
        return 0;
    }

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/Monsters/Vampire/Vamp_"), fx = p.Contains("/Monsters/Resources/Vampire/");
        bool portrait = p.Contains("/Portraits/Portrait_Vampire");
        if (!sheet && !fx && !portrait) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
        if (portrait) { ti.spriteImportMode = SpriteImportMode.Single; return; }
        if (fx)
        {
            ti.spriteImportMode = SpriteImportMode.Single;
            string f = Path.GetFileNameWithoutExtension(p);
            ti.spritePixelsPerUnit = f == "Vamp_Spear" ? 120f : f == "Vamp_Spikes" ? 115f : 100f;
            var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
            set.spriteAlignment = (int)(f == "Vamp_Spikes" ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(set); return;
        }
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

public static class VampireTools
{
    const string Dir = "Assets/Monsters/Vampire/";
    const string Por = "Assets/Monsters/Resources/Portraits/";
    const string Map10 = "Assets/Scenes/Map10_RuinedCastle3.unity";
    const string Map11 = "Assets/Scenes/Map11_RuinedCastle4_Vampire.unity";
    const float FeetY = -4.0f;

    static AnimationClip Clip(string sheet, string name, float fps, bool loop, int[] order = null)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{Dir}Vamp_{sheet}.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        var sprites = order == null ? all : order.Select(i => all[i]).ToArray();
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = loop ? sprites[0] : sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var set = AnimationUtility.GetAnimationClipSettings(clip); set.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, set);
        string path = $"{Dir}Vamp_{name}.anim"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static AnimatorController BuildController()
    {
        string path = Dir + "Vampire.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle", "Idle", 4, true, new[] { 0, 1, 2, 1 });
        sm.AddState("Walk").motion = Clip("Walk", "Walk", 12, true);
        sm.AddState("MeleeReady").motion = Clip("Melee", "MeleeReady", 10, false, new[] { 0, 1 });
        sm.AddState("MeleeSwing").motion = Clip("Melee", "MeleeSwing", 12, false, new[] { 2, 3, 4 });
        sm.AddState("Cast").motion = Clip("Cast", "Cast", 8, false);
        sm.AddState("Summon").motion = Clip("Summon", "Summon", 6, false);
        sm.AddState("Hit").motion = Clip("Hit", "Hit", 10, false);
        sm.AddState("Death").motion = Clip("Death", "Death", 6, false);
        sm.defaultState = idle;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // 소환할 네크로맨서 프리팹 (네크로맨서가 다시 스켈레톤 검사·궁수를 소환)
    static GameObject NecroPrefab()
    {
        string path = Dir + "Summon_Necromancer.prefab";
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (old != null) return old;
        NecromancerTools.GetSummons(out var sword, out var archer);
        var temp = NecromancerTools.Create(Vector3.zero, NecromancerTools.GetController(), sword, archer);
        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    public static GameObject Create(Vector3 pos)
    {
        var go = new GameObject("Vampire (중간 보스)");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "Vamp_Idle.png").OfType<Sprite>().FirstOrDefault();
        sr.sortingOrder = -1; sr.flipX = true;                     // 왼쪽(세리아가 오는 쪽)을 봄
        go.AddComponent<Animator>().runtimeAnimatorController = BuildController();
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f; rb.mass = 20f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        var body = go.AddComponent<CapsuleCollider2D>(); body.size = new Vector2(1.1f, 2.1f); body.offset = new Vector2(0, 1.05f);
        var hit = go.AddComponent<BoxCollider2D>(); hit.isTrigger = true; hit.size = new Vector2(1.2f, 2.1f); hit.offset = new Vector2(0, 1.05f);
        var hp = go.AddComponent<EnemyHealth>(); hp.maxHP = 350f; hp.hpBarOffset = new Vector2(0, 2.7f); hp.deathAnimTime = 1.3f;
        hp.isBoss = true; hp.bossId = "VampireLord";
        var v = go.AddComponent<VampireBoss>(); v.damage = 50f; v.expReward = 400;
        v.necromancerPrefab = NecroPrefab();
        Undo.RegisterCreatedObjectUndo(go, "Create Vampire");
        return go;
    }

    [MenuItem("Tools/Monsters/뱀파이어 보스 대사 편집 열기")]
    public static void EditTalk() { var d = TalkData(); Selection.activeObject = d; EditorGUIUtility.PingObject(d); }

    static Sprite Face(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Por + "Portrait_Vampire_" + n + ".png");

    static MonsterTalkData TalkData()
    {
        const string dir = "Assets/Monsters/Resources/MonsterTalk", path = dir + "/VampireBoss.asset";
        var d = AssetDatabase.LoadAssetAtPath<MonsterTalkData>(path);
        if (d != null) return d;
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Monsters/Resources", "MonsterTalk");
        d = ScriptableObject.CreateInstance<MonsterTalkData>();
        d.monsterName = "뱀파이어";
        d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(Por + "Portrait_Vampire.png");
        d.sightRange = 11f; d.oncePerGame = false;
        d.onSight = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 이 성에 아직도 산 자의 피 냄새가 나다니… 오랜만이군.", face = Face("Sneer") },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 당신이… 이 성을 이렇게 만든 거야?" },
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 후후… 그 피, 한 방울도 남김없이 마셔주지.", face = Face("Feed") },
        };
        d.onDefeat = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 크윽… 이 몸이… 인간 따위에게…", face = Face("Hurt") },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 끝났어. 이제 이 성도 쉴 수 있을 거야." },
        };
        AssetDatabase.CreateAsset(d, path); AssetDatabase.SaveAssets();
        return d;
    }

    [MenuItem("Tools/Maps/성곽4 추가 (중간 보스 뱀파이어)")]
    public static void BuildMap()
    {
        if (!File.Exists(Map10)) { EditorUtility.DisplayDialog("맵 추가", "멸망한 왕국 성곽3(Map10_RuinedCastle3)이 없습니다.\n먼저 'Tools > Monsters > 성곽3 만들기 + 네크로맨서 배치'를 실행하세요.", "확인"); return; }
        bool exists = File.Exists(Map11);
        if (!EditorUtility.DisplayDialog("맵 추가", exists
                ? "성곽4(Map11)가 이미 있습니다.\n뱀파이어만 다시 배치할까요?"
                : "성곽3 오른쪽 끝 → 멸망한 왕국 성곽4 (중간 보스: 뱀파이어)\n진행할까요?", "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        TalkData();

        if (!exists)
        {
            var s10 = EditorSceneManager.OpenScene(Map10);
            var st10 = Find<StageBounds>()[0];
            foreach (var p in Find<MapPortal>()) if (p.name.StartsWith("Portal_Right")) Object.DestroyImmediate(p.gameObject);
            Portal("Portal_Right (→ 성곽4 뱀파이어)", st10.right - 0.7f, "Map11_RuinedCastle4_Vampire", "Left");
            EnsureSpawns(st10);
            EditorSceneManager.SaveScene(s10);

            EditorSceneManager.SaveScene(s10, Map11, true);
            var s11 = EditorSceneManager.OpenScene(Map11);
            foreach (var e in Find<EnemyHealth>()) Object.DestroyImmediate(e.gameObject);
            foreach (var n in Find<NpcTalker>()) Object.DestroyImmediate(n.gameObject);
            foreach (var p in Find<MapPortal>()) Object.DestroyImmediate(p.gameObject);
            var st11 = Find<StageBounds>()[0];
            Portal("Portal_Left (→ 성곽3)", st11.left + 0.7f, "Map10_RuinedCastle3", "Right");
            EnsureSpawns(st11);
            var seria = GameObject.Find("Seria"); if (seria != null) seria.transform.position = new Vector3(st11.left + 2f, FeetY + 0.05f, 0);
            EditorSceneManager.SaveScene(s11);

            var list = EditorBuildSettings.scenes.Where(s => s.path != Map11).ToList();
            int i = list.FindIndex(s => s.path == Map10);
            list.Insert(i >= 0 ? i + 1 : list.Count, new EditorBuildSettingsScene(Map11, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        var scene = EditorSceneManager.OpenScene(Map11);
        foreach (var o in Find<VampireBoss>()) Object.DestroyImmediate(o.gameObject);
        var stage = Find<StageBounds>()[0];
        Create(new Vector3(Mathf.Lerp(stage.left, stage.right, 0.7f), FeetY + 0.05f, 0));
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("맵 추가 완료", "멸망한 왕국 성곽4(Map11)에 중간 보스 뱀파이어를 배치했습니다.\n\n체력 350 / 공격력 50 / 경험치 400\n10초마다 네크로맨서 소환 (이 맵 소환수는 경험치 없음)\n\n대사: Tools > Monsters > 뱀파이어 보스 대사 편집 열기", "확인");
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
