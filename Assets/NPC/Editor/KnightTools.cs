// 저주받은 기사 NPC (멸망한 왕국 성곽)
//  그림 자동 설정 + 메뉴: Tools > NPC > 멸망한 왕국 성곽에 저주받은 기사 배치 / 저주받은 기사 대사 편집 열기
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class KnightImporter : AssetPostprocessor
{
    const int CW = 240, CH = 264, Foot = 8;

    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/NPC/Knight/Knight_")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 4096;
        byte[] head = new byte[24];
        using (var fs = File.OpenRead(p)) fs.Read(head, 0, 24);
        int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        int n = w / CW;
        ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = 160;   // 키 약 1.45유닛
        string file = Path.GetFileNameWithoutExtension(p);
        var metas = new SpriteMetaData[n];
        for (int i = 0; i < n; i++)
            metas[i] = new SpriteMetaData { name = $"{file}_{i}", rect = new Rect(i * CW, 0, CW, CH), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, (float)Foot / CH) };
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
    }
}

public static class KnightTools
{
    const string Map7 = "Assets/Scenes/Map7_RuinedCastle.unity";
    const string DialoguePath = "Assets/NPC/Dialogues/Knight_Dialogue.asset";
    const string Res = "Assets/NPC/Resources/NPC/";
    const float FeetY = -4.0f;

    static Sprite[] Sheet(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
    static Sprite Face(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Res + "Portrait_Knight" + (n == null ? "" : "_" + n) + ".png");

    // 대화 데이터 (없으면 예시 대사로 새로 만듦)
    public static DialogueData GetOrCreateDialogue()
    {
        var d = AssetDatabase.LoadAssetAtPath<DialogueData>(DialoguePath);
        if (d != null) return d;
        if (!AssetDatabase.IsValidFolder("Assets/NPC/Dialogues")) AssetDatabase.CreateFolder("Assets/NPC", "Dialogues");
        d = ScriptableObject.CreateInstance<DialogueData>();
        d.seriaPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(Res + "Portrait_Seria.png");
        d.lines = new[]
        {
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) …누구냐. 이곳은 산 자가 올 곳이 아니다.", face = Face("Wary") },
            new DialogueData.Line { speaker = Speaker.Seria, text = "(예시) 당신은… 이 성의 기사인가요?" },
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) …세리아…? 정말… 너인가…?", face = Face("Longing") },
            new DialogueData.Line { speaker = Speaker.Npc,   text = "(예시) 대사는 Knight_Dialogue 에서 바꿀 수 있다. 줄마다 Face 에 표정을 넣을 수 있지.", face = Face("Neutral") },
        };
        AssetDatabase.CreateAsset(d, DialoguePath);
        AssetDatabase.SaveAssets();
        return d;
    }

    [MenuItem("Tools/NPC/멸망한 왕국 성곽에 저주받은 기사 배치")]
    public static void Place()
    {
        if (!File.Exists(Map7)) { EditorUtility.DisplayDialog("NPC 배치", "멸망한 왕국 성곽(Map7_RuinedCastle)이 없습니다.\n먼저 'Tools > Maps > 멸망한 왕국 성곽 추가'를 실행하세요.", "확인"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var dlg = GetOrCreateDialogue();
        var scene = EditorSceneManager.OpenScene(Map7);
        var old = GameObject.Find("NPC_CursedKnight"); if (old) Object.DestroyImmediate(old);
#if UNITY_2023_1_OR_NEWER
        var stage = Object.FindFirstObjectByType<StageBounds>();
#else
        var stage = Object.FindObjectOfType<StageBounds>();
#endif
        float x = stage != null ? (stage.left + stage.right) / 2f : 0f;      // 맵 한가운데

        var go = new GameObject("NPC_CursedKnight");
        go.transform.position = new Vector3(x, FeetY + 0.02f, 0);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = -2;
        var npc = go.AddComponent<NpcTalker>();
        npc.npcName = "저주받은 기사";
        npc.idleFrames = Sheet("Assets/NPC/Knight/Knight_Idle.png");   // 평소: 정면을 보고 숨 쉼
        npc.talkFrames = Sheet("Assets/NPC/Knight/Knight_Talk.png");   // 대화: 세리아 쪽으로 몸을 돌림
        npc.spriteFacesLeft = true;                                     // 대화 그림이 왼쪽을 보고 있음
        npc.fps = 5f;
        npc.portrait = Face(null);
        npc.dialogue = dlg;
        npc.iconOffset = new Vector2(0, 1.85f);
        if (npc.idleFrames.Length > 0) sr.sprite = npc.idleFrames[0];
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("NPC 배치 완료", $"멸망한 왕국 성곽 가운데(x = {x:0.#})에 저주받은 기사를 배치했습니다.\n대사는 Tools > NPC > 저주받은 기사 대사 편집 열기 에서 수정합니다.", "확인");
    }

    [MenuItem("Tools/NPC/저주받은 기사 대사 편집 열기")]
    public static void EditDialogue()
    {
        var d = GetOrCreateDialogue();
        Selection.activeObject = d; EditorGUIUtility.PingObject(d);
    }
}
