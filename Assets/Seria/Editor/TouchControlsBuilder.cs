// 메뉴: Tools > Seria > 모바일 터치 버튼 만들기
using UnityEditor;
using UnityEngine;

public static class TouchControlsBuilder
{
    [MenuItem("Tools/Seria/모바일 터치 버튼 만들기 (Build Touch Controls)")]
    public static void Build()
    {
        var player = GameObject.Find("Seria");
        if (player == null) { Debug.LogError("씬에 'Seria' 오브젝트가 없습니다."); return; }

        var old = GameObject.Find("Touch Controls");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var go = new GameObject("Touch Controls");
        Undo.RegisterCreatedObjectUndo(go, "Build Touch Controls");
        var tc = go.AddComponent<TouchControls>();
        tc.player = player.GetComponent<SeriaController>();
        const string d = "Assets/Seria/UI/";
        tc.left = AssetDatabase.LoadAssetAtPath<Sprite>(d + "Btn_Left.png");
        tc.right = AssetDatabase.LoadAssetAtPath<Sprite>(d + "Btn_Right.png");
        tc.jump = AssetDatabase.LoadAssetAtPath<Sprite>(d + "Btn_Jump.png");
        tc.attack = AssetDatabase.LoadAssetAtPath<Sprite>(d + "Btn_Attack.png");
        tc.heavy = AssetDatabase.LoadAssetAtPath<Sprite>(d + "Btn_Heavy.png");
        tc.skill = AssetDatabase.LoadAssetAtPath<Sprite>(d + "Btn_Skill.png");

        // 모바일은 가로 화면 고정
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        Selection.activeGameObject = go;
        Debug.Log("터치 버튼 생성 완료! PC에서 미리 보려면 'Touch Controls' 의 Always Show 를 켜고 Play 하세요.");
    }
}
