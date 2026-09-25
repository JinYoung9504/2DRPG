// 마을 이장 그림 자동 설정 + 메뉴: Tools > NPC > 시작 마을에 마을 이장 배치
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class NpcImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool sheet = p.Contains("/NPC/Chief/Chief_"), res = p.Contains("/NPC/Resources/NPC/") || p.Contains("/Settings/Resources/Settings/");
        if (!sheet && !res) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        if (res) { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 100; return; }
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        const int CW = 280, CH = 340; int n = w / CW;
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = 200;   // 키 약 1.3유닛 (세리아와 비슷)
        string file = Path.GetFileNameWithoutExtension(p);
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * CW, 0, CW, CH), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 8f / CH) };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class NpcTools
{
    const string Map0 = "Assets/Scenes/Map0_Town.unity";
    const float FeetY = -4.0f;

    static Sprite[] Sheet(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

    [MenuItem("Tools/NPC/시작 마을에 마을 이장 배치")]
    public static void PlaceChief()
    {
        if (!File.Exists(Map0)) { EditorUtility.DisplayDialog("NPC 배치", "시작 마을(Map0_Town)이 없습니다.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(Map0);
        var old = GameObject.Find("NPC_VillageChief"); if (old) Object.DestroyImmediate(old);

#if UNITY_2023_1_OR_NEWER
        var stage = Object.FindFirstObjectByType<StageBounds>();
#else
        var stage = Object.FindObjectOfType<StageBounds>();
#endif
        float x = stage != null ? stage.left + (stage.right - stage.left) * 2f / 3f : 10f;   // 오른쪽 1/3 지점

        var go = new GameObject("NPC_VillageChief");
        go.transform.position = new Vector3(x, FeetY + 0.02f, 0);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = -2; sr.flipX = true;   // 처음엔 왼쪽(들어오는 쪽)을 봄
        var npc = go.AddComponent<NpcTalker>();
        npc.npcName = "마을 이장";
        npc.idleFrames = Sheet("Assets/NPC/Chief/Chief_Idle.png");
        npc.talkFrames = Sheet("Assets/NPC/Chief/Chief_Talk.png");
        npc.portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NPC/Resources/NPC/Portrait_Chief.png");
        if (npc.idleFrames.Length > 0) sr.sprite = npc.idleFrames[0];
        npc.dialogue = GetOrCreateChiefDialogue();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("NPC 배치 완료", $"시작 마을 x = {x:0.#} 위치에 마을 이장을 배치했습니다.\n대사는 Assets/NPC/Dialogues/Chief_Dialogue 에서 수정합니다.", "확인");
    }

    const string DialoguePath = "Assets/NPC/Dialogues/Chief_Dialogue.asset";

    // 마을 이장 대화 데이터 (없으면 예시 대사로 새로 만듦)
    public static DialogueData GetOrCreateChiefDialogue()
    {
        var d = AssetDatabase.LoadAssetAtPath<DialogueData>(DialoguePath);
        if (d != null) return d;
        if (!AssetDatabase.IsValidFolder("Assets/NPC/Dialogues")) AssetDatabase.CreateFolder("Assets/NPC", "Dialogues");
        d = ScriptableObject.CreateInstance<DialogueData>();
        d.seriaPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NPC/Resources/NPC/Portrait_Seria.png");
        d.lines = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 오, 세리아 왔구나." },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 이장님, 무슨 일 있으세요?" },
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 대사는 Chief_Dialogue 에서 바꿀 수 있단다." },
        };
        AssetDatabase.CreateAsset(d, DialoguePath);
        AssetDatabase.SaveAssets();
        return d;
    }

    [MenuItem("Tools/NPC/마을 이장 대사 편집 열기")]
    public static void EditChiefDialogue()
    {
        var d = GetOrCreateChiefDialogue();
        // 시작 마을의 이장에 연결 (안 되어 있으면)
        if (File.Exists(Map0) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            var sc = EditorSceneManager.OpenScene(Map0);
            var go = GameObject.Find("NPC_VillageChief");
            if (go != null) { var npc = go.GetComponent<NpcTalker>(); if (npc != null && npc.dialogue != d) { npc.dialogue = d; EditorUtility.SetDirty(npc); EditorSceneManager.SaveScene(sc); } }
        }
        Selection.activeObject = d;
        EditorGUIUtility.PingObject(d);
    }
}
