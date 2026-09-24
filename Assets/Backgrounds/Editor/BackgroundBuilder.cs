// 메뉴: Tools > Background > 마을 배경 만들기
using UnityEditor;
using UnityEngine;

public static class BackgroundBuilder
{
    // 배경 그림 기준 수치 (화면 높이 10유닛, 화면 아래 끝 y = -5)
    const float GroundHeight = 128f / BackgroundImporter.PPU;   // 바닥 타일 높이
    const float FeetY = -4.0f;                                  // 캐릭터 발이 닿는 높이

    [MenuItem("Tools/Background/마을 배경 만들기 (Build Town)")]
    public static void BuildTown()
    {
        const string dir = "Assets/Backgrounds/Town/";
        var far = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "Town_Far.png");
        var ground = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "Town_Ground.png");
        if (far == null || ground == null) { Debug.LogError("배경 그림을 찾을 수 없습니다: " + dir); return; }

        var old = GameObject.Find("Town Background");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("Town Background");
        Undo.RegisterCreatedObjectUndo(root, "Build Town");
        var stage = root.AddComponent<StageBounds>();
        float width = stage.right - stage.left, center = (stage.left + stage.right) * 0.5f;

        // 카메라
        var cam = Camera.main;
        if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.45f, 0.65f, 0.95f);
        var follow = cam.GetComponent<CameraFollow2D>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.stage = stage;

        // 원경
        var farGo = new GameObject("Far (원경)");
        farGo.transform.SetParent(root.transform);
        var fsr = farGo.AddComponent<SpriteRenderer>();
        fsr.sprite = far; fsr.sortingOrder = -100;
        var px = farGo.AddComponent<ParallaxFit>(); px.cam = cam; px.stage = stage;
        farGo.transform.position = new Vector3(0, 0, 10);

        // 바닥 (타일 반복)
        var gGo = new GameObject("Ground (바닥)");
        gGo.transform.SetParent(root.transform);
        var gsr = gGo.AddComponent<SpriteRenderer>();
        gsr.sprite = ground; gsr.drawMode = SpriteDrawMode.Tiled;
        gsr.size = new Vector2(width + 20f, GroundHeight);
        gsr.sortingOrder = -50;
        gGo.transform.position = new Vector3(center, -5f + GroundHeight * 0.5f, 5);

        // 바닥 충돌체 (발 높이까지)
        var floor = new GameObject("Floor Collider");
        floor.transform.SetParent(root.transform);
        var box = floor.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width + 20f, 4f);
        floor.transform.position = new Vector3(center, FeetY - 2f, 0);

        // 양쪽 벽 (맵 밖으로 못 나가게)
        foreach (var x in new[] { stage.left - 0.5f, stage.right + 0.5f })
        {
            var wall = new GameObject("Wall");
            wall.transform.SetParent(root.transform);
            var wb = wall.AddComponent<BoxCollider2D>();
            wb.size = new Vector2(1f, 30f);
            wall.transform.position = new Vector3(x, 5f, 0);
        }

        // 플레이어 연결
        var player = GameObject.Find("Seria");
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Move Player");
            player.transform.position = new Vector3(0, FeetY + 0.05f, 0);
            follow.target = player.transform;
        }
        else Debug.LogWarning("씬에 'Seria' 오브젝트가 없습니다. Main Camera의 Camera Follow 2D > Target 에 플레이어를 직접 넣어주세요.");

        Selection.activeGameObject = root;
        Debug.Log("마을 배경 생성 완료! 이전에 만든 바닥(Square)이 있다면 삭제하세요.");
    }
}
