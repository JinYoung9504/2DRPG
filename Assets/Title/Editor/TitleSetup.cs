// 타이틀 그림 자동 설정 + 메뉴: Tools > Title > 타이틀 화면 + 저장 기능 설치
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TitleArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Title/Art/") && !p.Contains("/Title/Resources/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
    }
}

public static class TitleSetup
{
    const string TitlePath = "Assets/Scenes/Title.unity";
    const string Art = "Assets/Title/Art/";

    static Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + n);

    [MenuItem("Tools/Title/타이틀 화면 + 저장 기능 설치")]
    public static void Install()
    {
        var gameScenes = EditorBuildSettings.scenes.Where(s => s.enabled && s.path != TitlePath).Select(s => s.path).ToList();
        if (gameScenes.Count == 0)
        {
            EditorUtility.DisplayDialog("타이틀 설치", "빌드 목록에 게임 씬이 없습니다.\n먼저 Tools > Maps > 맵 2개 구성하기 를 실행하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        // 1) 게임 씬마다 우측 상단 저장 버튼 추가
        int added = 0;
        foreach (var path in gameScenes)
        {
            var sc = EditorSceneManager.OpenScene(path);
            if (GameObject.Find("Seria") == null) continue;
            var old = GameObject.Find("Save Button");
            if (old != null) Object.DestroyImmediate(old);
            var sb = new GameObject("Save Button").AddComponent<SaveButton>();
            sb.icon = S("Icon_Save.png"); sb.toastSaved = S("Toast_Saved.png");
            EditorSceneManager.SaveScene(sc);
            added++;
        }

        // 2) 타이틀 씬 만들기
        var title = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0, 0, -10);
        var ts = new GameObject("Title Screen").AddComponent<TitleScreen>();
        ts.firstScene = Path.GetFileNameWithoutExtension(gameScenes[0]);
        ts.background = S("Title_Background.png");
        ts.backgroundBlur = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Title/Resources/Title_BG_Blur.png");
        ts.startMask = S("Btn_Start_Mask.png"); ts.continueMask = S("Btn_Continue_Mask.png");
        ts.toastNoSave = S("Toast_NoSave.png");
        EditorSceneManager.SaveScene(title, TitlePath);

        // 3) 빌드 목록: 타이틀을 맨 앞(게임 시작 화면)으로
        var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(TitlePath, true) };
        list.AddRange(EditorBuildSettings.scenes.Where(s => s.path != TitlePath));
        EditorBuildSettings.scenes = list.ToArray();

        EditorUtility.DisplayDialog("타이틀 설치 완료",
            $"• Title 씬 생성 (게임 시작 화면)\n• 시작하기 → {ts.firstScene}\n• 저장 버튼 추가: 게임 씬 {added}개\n\n지금 Title 씬이 열려 있습니다. Play 해 보세요.", "확인");
    }

    [MenuItem("Tools/Title/저장 데이터 삭제 (테스트용)")]
    public static void DeleteSave()
    {
        SaveSystem.Delete();
        Debug.Log("저장 데이터를 삭제했습니다.");
    }
}
