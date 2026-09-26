// 세리아 스킬 F: 성검 유성우
//  1) 목표 지점 하늘에 마법진이 열림 (3단계로 커짐)
//  2) 마법진에서 검이 한 자루씩 비스듬히 우수수 떨어짐 (유성우)
//  3) 검이 땅에 꽂히면 푸른 빛이 튀고, 주변 적에게 데미지
//   · 적 1마리당 한 번만 맞음 (공격력 200) — 마법진 아래 적에게는 반드시 몇 자루가 겨냥해서 떨어짐
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordRain : MonoBehaviour
{
    float damage, width, attackerX, dir;
    Vector3 ground;
    readonly HashSet<EnemyHealth> hitOnce = new HashSet<EnemyHealth>();

    [Tooltip("떨어지는 검 수")] public int swordCount = 26;
    public float interval = 0.065f;       // 검 사이 간격
    public float skyHeight = 6.2f;        // 땅에서 마법진까지 높이
    public float fallSpeed = 24f;
    public float tilt = 14f;              // 비스듬히 떨어지는 각도(도)
    public Vector2 hitBox = new Vector2(1.3f, 2.6f);

    static Material trailMat;
    bool closing;

    public static void Cast(Vector3 groundPos, float damage, float width, float attackerX, float facing)
    {
        var g = new GameObject("Skill SwordRain");
        var s = g.AddComponent<SwordRain>();
        s.ground = groundPos; s.damage = damage; s.width = width; s.attackerX = attackerX; s.dir = facing;
    }

    IEnumerator Start()
    {
        Vector3 sky = ground + new Vector3(0, skyHeight, 0);

        // 1) 하늘 마법진 열기
        var circleFrames = SkillFX.Frames("SwordRainCircle");
        var circle = SkillFX.MakeRenderer("SwordRainCircle", sky, false); circle.sortingOrder = 18;
        float sc = circleFrames.Length > 0 ? width / Mathf.Max(0.01f, circleFrames[circleFrames.Length - 1].bounds.size.x) : 1f;   // 마지막(가장 큰) 장면 폭 = 스킬 폭
        for (int i = 0; i < circleFrames.Length; i++)
        {
            circle.sprite = circleFrames[i];
            for (float t = 0; t < 0.13f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0.75f, 1f, t / 0.13f);          // 장면마다 살짝 퍼지며 커짐
                circle.transform.localScale = new Vector3(sc * k, sc * k, 1);
                circle.color = new Color(1, 1, 1, i == 0 ? t / 0.13f : 1f);
                yield return null;
            }
        }
        circle.transform.localScale = new Vector3(sc, sc, 1); circle.color = Color.white;
        float finalScale = sc;
        StartCoroutine(Pulse(circle, finalScale));
        CameraShake.Shake(0.1f, 0.05f);
        yield return new WaitForSeconds(0.2f);

        // 2) 검 떨어뜨리기 — 마법진 아래 적 위치를 먼저 몇 자루 겨냥
        var targets = new List<float>();
        foreach (var c in Physics2D.OverlapBoxAll(ground + new Vector3(0, 1.5f, 0), new Vector2(width, 4f), 0f))
        {
            var e = c.GetComponentInParent<EnemyHealth>();
            if (e != null && !e.IsDead && !targets.Contains(e.transform.position.x)) targets.Add(e.transform.position.x);
        }
        var blades = SkillFX.Frames("SwordRainBlade");
        float drift = Mathf.Tan(tilt * Mathf.Deg2Rad) * skyHeight;          // 비스듬히 떨어지는 동안 옆으로 가는 거리
        for (int i = 0; i < swordCount; i++)
        {
            float landX;
            if (targets.Count > 0 && i % 4 == 1) landX = targets[(i / 4) % targets.Count] + Random.Range(-0.3f, 0.3f);
            else landX = ground.x + Random.Range(-width * 0.45f, width * 0.45f);
            var sprite = blades.Length > 0 ? blades[Random.Range(0, blades.Length)] : null;
            float size = Random.Range(0.75f, 1.15f);
            StartCoroutine(Fall(sprite, landX, drift, size));
            yield return new WaitForSeconds(interval * Random.Range(0.6f, 1.4f));
        }
        yield return new WaitForSeconds(skyHeight / fallSpeed + 0.4f);

        // 3) 마법진 닫기
        closing = true;
        for (float t = 0; t < 0.35f && circle != null; t += Time.deltaTime)
        {
            circle.color = new Color(1, 1, 1, 1f - t / 0.35f);
            circle.transform.localScale = Vector3.one * finalScale * (1f + t * 0.3f);
            yield return null;
        }
        Destroy(gameObject, 1.2f);
        if (circle != null) Destroy(circle.gameObject);
    }

    IEnumerator Pulse(SpriteRenderer r, float baseScale)
    {
        float t = 0;
        while (r != null && !closing)
        {
            t += Time.deltaTime;
            r.transform.localScale = new Vector3(baseScale * (1f + 0.02f * Mathf.Sin(t * 9f)), baseScale, 1);
            r.color = new Color(0.9f + 0.1f * Mathf.Sin(t * 12f), 0.95f, 1f, 1f);
            yield return null;
        }
    }

    IEnumerator Fall(Sprite sprite, float landX, float drift, float size)
    {
        var sr = SkillFX.MakeRenderer("SwordRainBlade", Vector3.zero, false);
        sr.sprite = sprite; sr.sortingOrder = 21 + Random.Range(0, 3);
        sr.transform.localScale = Vector3.one * size;
        Vector3 end = new Vector3(landX, ground.y, 0);
        Vector3 start = end + new Vector3(-dir * drift, skyHeight - 0.2f, 0);
        Vector3 v = (end - start).normalized;
        sr.transform.rotation = Quaternion.FromToRotation(Vector3.down, v);             // 칼끝이 떨어지는 방향
        AddTrail(sr.gameObject, size);

        float len = (end - start).magnitude, t = 0, dur = len / fallSpeed;
        while (t < dur)
        {
            t += Time.deltaTime;
            sr.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / dur));
            yield return null;
        }
        sr.transform.position = end + v * 0.25f;                                       // 땅에 살짝 박힘

        // 착탄
        Impact(end);
        foreach (var c in Physics2D.OverlapBoxAll(end + new Vector3(0, hitBox.y / 2f, 0), hitBox, 0f))
        {
            var e = c.GetComponentInParent<EnemyHealth>();
            if (e != null && !e.IsDead && hitOnce.Add(e)) e.TakeDamage(damage, attackerX);
        }
        if (Random.value < 0.3f) CameraShake.Shake(0.06f, 0.05f);

        // 꽂힌 검이 빛으로 흩어짐
        yield return new WaitForSeconds(0.25f);
        for (float k = 0; k < 0.3f && sr != null; k += Time.deltaTime)
        {
            sr.color = new Color(0.8f, 0.85f, 1f, 1f - k / 0.3f);
            sr.transform.position += Vector3.up * Time.deltaTime * 0.6f;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    void Impact(Vector3 pos)
    {
        var f = SkillFX.Frames("SwordRainImpact");
        if (f.Length == 0) return;
        var r = SkillFX.MakeRenderer("SwordRainImpact", pos, Random.value < 0.5f); r.sprite = f[0]; r.sortingOrder = 24;
        StartCoroutine(ImpactAnim(r));
    }

    IEnumerator ImpactAnim(SpriteRenderer r)
    {
        float s0 = Random.Range(0.7f, 1.0f);
        for (float t = 0; t < 0.35f && r != null; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            r.transform.localScale = new Vector3(s0 * (0.6f + 0.6f * k), s0 * (0.4f + 0.8f * Mathf.Sin(k * Mathf.PI * 0.5f)), 1);
            r.color = new Color(1, 1, 1, 1f - k * k);
            yield return null;
        }
        if (r != null) Destroy(r.gameObject);
    }

    static void AddTrail(GameObject g, float size)
    {
        if (trailMat == null) trailMat = new Material(Shader.Find("Sprites/Default"));
        var tr = g.AddComponent<TrailRenderer>();
        tr.material = trailMat; tr.time = 0.12f; tr.minVertexDistance = 0.05f;
        tr.startWidth = 0.22f * size; tr.endWidth = 0f;
        tr.startColor = new Color(0.65f, 0.75f, 1f, 0.8f); tr.endColor = new Color(0.35f, 0.3f, 1f, 0f);
        tr.sortingOrder = 20;
    }
}
