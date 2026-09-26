// 여러 겹 배경(패럴랙스) 한 장: 카메라를 follow 비율만큼 따라감
//  follow = 1 : 화면에 고정 (아주 먼 하늘)   follow = 0 : 땅처럼 월드에 고정
//  그 사이 값일수록 멀리 있는 것처럼 천천히 지나감
using UnityEngine;

[DefaultExecutionOrder(100)]   // 카메라가 움직인 뒤에 실행
public class ParallaxBand : MonoBehaviour
{
    public Camera cam;
    [Range(0f, 1f)] public float follow = 0.5f;
    public float baseX = 0f;       // 카메라가 여기 있을 때 그림 가운데가 여기

    void Awake() { if (cam == null) cam = Camera.main; }

    void LateUpdate()
    {
        if (cam == null) return;
        var p = transform.position;
        p.x = baseX + (cam.transform.position.x - baseX) * follow;
        transform.position = p;
    }
}
