// 스테이지(맵)의 왼쪽·오른쪽 끝. 카메라와 배경이 이 범위를 기준으로 움직입니다.
using UnityEngine;

public class StageBounds : MonoBehaviour
{
    public float left = -30f;
    public float right = 30f;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(left, -6), new Vector3(left, 6));
        Gizmos.DrawLine(new Vector3(right, -6), new Vector3(right, 6));
    }
}
