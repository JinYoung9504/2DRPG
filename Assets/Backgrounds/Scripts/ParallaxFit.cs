// 원경 배경: 화면 높이에 맞춰 크기를 자동 조절하고,
// 카메라가 맵 왼쪽 끝 → 오른쪽 끝으로 갈 때 그림도 왼쪽 끝 → 오른쪽 끝이 보이도록
// 카메라보다 느리게 움직입니다(패럴랙스).
using UnityEngine;

[DefaultExecutionOrder(100)]   // 카메라가 움직인 뒤에 실행
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxFit : MonoBehaviour
{
    public Camera cam;
    public StageBounds stage;

    SpriteRenderer sr;

    void Awake() { sr = GetComponent<SpriteRenderer>(); if (cam == null) cam = Camera.main; }

    void LateUpdate()
    {
        if (cam == null || sr.sprite == null) return;

        // 화면 높이에 딱 맞게 크기 조절
        float spriteH = sr.sprite.bounds.size.y;
        float scale = cam.orthographicSize * 2f / spriteH;
        transform.localScale = new Vector3(scale, scale, 1);

        float halfView = cam.orthographicSize * cam.aspect;
        float halfImg = sr.sprite.bounds.extents.x * scale;
        float slack = Mathf.Max(0f, halfImg - halfView);

        float t = 0.5f;
        if (stage != null)
        {
            float min = stage.left + halfView, max = stage.right - halfView;
            if (max > min) t = Mathf.InverseLerp(min, max, cam.transform.position.x);
        }
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(c.x + Mathf.Lerp(slack, -slack, t), c.y, transform.position.z);
    }
}
