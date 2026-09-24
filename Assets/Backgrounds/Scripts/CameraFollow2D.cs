// 카메라가 플레이어를 좌우로 부드럽게 따라가고, 맵 끝에서는 멈춥니다.
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public StageBounds stage;
    public float smooth = 8f;        // 클수록 빠르게 따라감
    public float lookAhead = 1.0f;   // 바라보는 방향으로 조금 더 보여주기

    Camera cam; float fixedY; float look;

    void Awake() { cam = GetComponent<Camera>(); fixedY = transform.position.y; }

    void LateUpdate()
    {
        if (target == null) return;
        var sr = target.GetComponent<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;
        look = Mathf.Lerp(look, dir * lookAhead, Time.deltaTime * 3f);

        float x = Mathf.Lerp(transform.position.x, target.position.x + look, Time.deltaTime * smooth);
        if (stage != null)
        {
            float half = cam.orthographicSize * cam.aspect;
            float min = stage.left + half, max = stage.right - half;
            x = min < max ? Mathf.Clamp(x, min, max) : (stage.left + stage.right) * 0.5f;
        }
        transform.position = new Vector3(x, fixedY, transform.position.z);
    }
}
