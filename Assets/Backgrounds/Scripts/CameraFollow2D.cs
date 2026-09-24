// 카메라가 플레이어를 좌우로 부드럽게 따라가고, 맵 끝에서는 멈춥니다.
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public StageBounds stage;
    public float smoothTime = 0.12f; // 작을수록 빠르게 따라감 (부드러운 감속 추적)
    public float lookAhead = 1.0f;   // 바라보는 방향으로 조금 더 보여주기

    Camera cam; float fixedY; float look; float velX, lookVel;

    void Awake() { cam = GetComponent<Camera>(); fixedY = transform.position.y; }

    // 맵 이동 직후 카메라를 플레이어 위치로 즉시 이동
    public void SnapToTarget()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();
        look = 0f; velX = lookVel = 0f;
        transform.position = new Vector3(Clamp(target.position.x), transform.position.y, transform.position.z);
    }

    float Clamp(float x)
    {
        if (stage == null) return x;
        float half = cam.orthographicSize * cam.aspect;
        float min = stage.left + half, max = stage.right - half;
        return min < max ? Mathf.Clamp(x, min, max) : (stage.left + stage.right) * 0.5f;
    }

    void LateUpdate()
    {
        if (target == null) return;
        var sr = target.GetComponent<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;
        look = Mathf.SmoothDamp(look, dir * lookAhead, ref lookVel, 0.5f);

        float x = Mathf.SmoothDamp(transform.position.x, target.position.x + look, ref velX, smoothTime);
        if (stage != null)
        {
            float half = cam.orthographicSize * cam.aspect;
            float min = stage.left + half, max = stage.right - half;
            x = min < max ? Mathf.Clamp(x, min, max) : (stage.left + stage.right) * 0.5f;
        }
        transform.position = new Vector3(x, fixedY, transform.position.z);
    }
}
