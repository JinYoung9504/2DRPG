// 섬광보 이펙트: 출발 지점 빛 → 푸른 궤적 → 도착 지점 빛
using System.Collections;
using UnityEngine;

public class BlinkFX : MonoBehaviour
{
    Vector3 from, to; float dir; int order;
    const float ChestY = 0.7f;              // 세리아 가슴 높이

    public static void Play(Vector2 from, Vector2 to, float dir, int order)
    {
        var g = new GameObject("FX Blink");
        var fx = g.AddComponent<BlinkFX>();
        fx.from = from; fx.to = to; fx.dir = dir; fx.order = order;
    }

    IEnumerator Start()
    {
        StartCoroutine(Burst("BlinkStart", from, 0.22f, 1.0f));
        StartCoroutine(Trail());
        yield return new WaitForSeconds(0.06f);
        StartCoroutine(Burst("BlinkEnd", to, 0.3f, 1.1f));
        CameraShake.Shake(0.06f, 0.05f);
        yield return new WaitForSeconds(0.6f);
        Destroy(gameObject);
    }

    IEnumerator Trail()
    {
        var f = SkillFX.Frames("BlinkTrail");
        if (f.Length == 0) yield break;
        var r = SkillFX.MakeRenderer("BlinkTrail", to + new Vector3(0, ChestY, 0), dir < 0);
        r.sortingOrder = order + 3; r.transform.SetParent(transform, true);
        float len = Mathf.Abs(to.x - from.x) + 0.9f;
        float sx = len / Mathf.Max(0.01f, f[0].bounds.size.x);
        r.transform.localScale = new Vector3(sx, 0.9f, 1);
        for (int i = 0; i < f.Length; i++) { r.sprite = f[i]; yield return new WaitForSeconds(0.04f); }
        Destroy(r.gameObject);
    }

    IEnumerator Burst(string name, Vector3 pos, float time, float scale)
    {
        var f = SkillFX.Frames(name);
        if (f.Length == 0) yield break;
        var r = SkillFX.MakeRenderer(name, pos + new Vector3(0, ChestY, 0), dir < 0);
        r.sprite = f[0]; r.sortingOrder = order + 4; r.transform.SetParent(transform, true);
        for (float t = 0; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            r.transform.localScale = Vector3.one * scale * (0.6f + 0.6f * k);
            r.color = new Color(1, 1, 1, 1f - k * k);
            yield return null;
        }
        Destroy(r.gameObject);
    }
}
