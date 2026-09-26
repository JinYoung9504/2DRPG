// 몬스터끼리는 서로 부딪히지 않고 겹칠 수 있게 함 (소환수가 쌓여 위로 떠오르는 문제 방지)
//  새로 생긴 몬스터를 자동으로 찾아 다른 몬스터들과의 충돌을 끔 (땅 · 벽 · 세리아와의 충돌은 그대로)
using System.Collections.Generic;
using UnityEngine;

public class EnemyNoStack : MonoBehaviour
{
    readonly List<EnemyHealth> known = new List<EnemyHealth>();
    float next, nextFull;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var g = new GameObject("Enemy No-Stack"); DontDestroyOnLoad(g); g.AddComponent<EnemyNoStack>();
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.1f;
        known.RemoveAll(e => e == null);
#if UNITY_2023_1_OR_NEWER
        var all = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<EnemyHealth>();
#endif
        // 콜라이더가 꺼졌다 켜지면(리스폰 · 등장) 설정이 풀릴 수 있어 1초마다 전체 다시 적용
        bool full = Time.unscaledTime >= nextFull;
        if (full) { nextFull = Time.unscaledTime + 1f; known.Clear(); }
        foreach (var e in all)
        {
            if (known.Contains(e)) continue;
            var mine = e.GetComponentsInChildren<Collider2D>(true);
            foreach (var o in known)
                foreach (var a in mine)
                    foreach (var b in o.GetComponentsInChildren<Collider2D>(true))
                        if (a != null && b != null) Physics2D.IgnoreCollision(a, b, true);
            known.Add(e);
        }
    }
}
