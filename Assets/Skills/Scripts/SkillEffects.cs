// 스킬 이펙트 재생 + 데미지 판정
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SkillFX
{
    static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    // Resources/Skills/FX_이름.png 에서 프레임을 순서대로 불러옴
    public static Sprite[] Frames(string name)
    {
        if (cache.TryGetValue(name, out var f)) return f;
        var list = new List<Sprite>(Resources.LoadAll<Sprite>("Skills/FX_" + name));
        list.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
        f = list.ToArray(); cache[name] = f;
        if (f.Length == 0) Debug.LogError("[Skill] 이펙트를 찾을 수 없습니다: Resources/Skills/FX_" + name);
        return f;
    }
    static int Index(string n) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out var v) ? v : 0; }

    public static SpriteRenderer MakeRenderer(string name, Vector3 pos, bool flip)
    {
        var go = new GameObject("FX " + name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 20; sr.flipX = flip;
        return sr;
    }

    // 범위 안 적 모두에게 데미지 (적 1마리당 1회)
    public static int DamageArea(Vector2 center, Vector2 size, float damage, float attackerX)
    {
        int hits = 0; var done = new HashSet<EnemyHealth>();
        foreach (var c in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            var e = c.GetComponentInParent<EnemyHealth>();
            if (e != null && !e.IsDead && done.Add(e)) { e.TakeDamage(damage, attackerX); hits++; }
        }
        return hits;
    }
}

// 검기: 앞으로 날아가다 첫 번째 적에게 맞으면 타격 이펙트와 함께 사라짐
public class SwordWave : MonoBehaviour
{
    public float damage = 15f, speed = 11f, maxDistance = 9f;
    public float dir = 1f, attackerX;
    SpriteRenderer sr; Sprite[] f; float traveled;

    public static void Launch(Vector3 pos, float dir, float damage)
    {
        var sr = SkillFX.MakeRenderer("SwordWave", pos, dir < 0);
        var w = sr.gameObject.AddComponent<SwordWave>();
        w.dir = dir; w.damage = damage; w.attackerX = pos.x;
    }

    IEnumerator Start()
    {
        sr = GetComponent<SpriteRenderer>(); f = SkillFX.Frames("SwordWave");
        if (f.Length < 9) { Destroy(gameObject); yield break; }
        // 1~2: 발사
        for (int i = 0; i < 2; i++) { sr.sprite = f[i]; yield return new WaitForSeconds(0.05f); }
        // 3~7: 전진 (반복)
        int k = 2; float t = 0;
        while (traveled < maxDistance)
        {
            float step = speed * Time.deltaTime;
            transform.position += new Vector3(dir * step, 0, 0); traveled += step;
            t += Time.deltaTime; if (t > 0.05f) { t = 0; k = k >= 6 ? 2 : k + 1; sr.sprite = f[k]; }
            if (HitCheck()) break;
            yield return null;
        }
        // 8~9: 타격 / 소멸
        for (int i = 7; i < 9; i++) { sr.sprite = f[i]; yield return new WaitForSeconds(0.07f); }
        Destroy(gameObject);
    }

    bool HitCheck()
    {
        foreach (var c in Physics2D.OverlapBoxAll(transform.position, new Vector2(1.1f, 1.2f), 0f))
        {
            var e = c.GetComponentInParent<EnemyHealth>();
            if (e != null && !e.IsDead) { e.TakeDamage(damage, attackerX); return true; }
        }
        return false;
    }
}

// 번개 / 메테오: 지정 위치에 예고 → 낙하 → 타격(범위 데미지) → 잔여 이펙트
public class StrikeEffect : MonoBehaviour
{
    public string fxName; public float damage; public Vector2 area; public int hitFrame; public float fps = 12f;
    public float attackerX;

    public static void Cast(string fxName, Vector3 groundPos, float damage, Vector2 area, int hitFrame, float scale, float attackerX)
    {
        var sr = SkillFX.MakeRenderer(fxName, groundPos, false);
        sr.transform.localScale = Vector3.one * scale;
        var s = sr.gameObject.AddComponent<StrikeEffect>();
        s.fxName = fxName; s.damage = damage; s.area = area; s.hitFrame = hitFrame; s.attackerX = attackerX;
    }

    IEnumerator Start()
    {
        var sr = GetComponent<SpriteRenderer>(); var f = SkillFX.Frames(fxName);
        for (int i = 0; i < f.Length; i++)
        {
            sr.sprite = f[i];
            if (i == hitFrame)
            {
                SkillFX.DamageArea((Vector2)transform.position + new Vector2(0, area.y / 2f), area, damage, attackerX);
                CameraShake.Shake(0.15f, damage >= 100 ? 0.25f : 0.12f);
            }
            yield return new WaitForSeconds(i < 2 ? 1.6f / fps : 1f / fps);   // 예고 프레임은 조금 길게
        }
        for (float t = 1f; t > 0; t -= Time.deltaTime / 0.25f) { sr.color = new Color(1, 1, 1, t); yield return null; }
        Destroy(gameObject);
    }
}

// 메테오: 하늘 맨 위 마법진 → 작은 돌 2개가 먼저 빠르게 낙하 → 큰 돌이 뒤따라 낙하 → 대폭발
public class MeteorStrike : MonoBehaviour
{
    public float damage, width = 6f, attackerX;
    public float smallDamage = 10f;          // 작은 돌 1개당 데미지
    public float smallSpread = 1.8f;         // 작은 돌이 떨어지는 위치 (목표 좌우로 이만큼)
    Vector3 ground; float top;

    public static void Cast(Vector3 groundPos, float damage, float width, float attackerX, float smallDamage = 10f)
    {
        var sr = SkillFX.MakeRenderer("Meteor", groundPos, false);
        sr.sortingOrder = 25;
        var m = sr.gameObject.AddComponent<MeteorStrike>();
        m.damage = damage; m.width = width; m.attackerX = attackerX; m.ground = groundPos; m.smallDamage = smallDamage;
    }

    IEnumerator Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        var f = SkillFX.Frames("Meteor"); var sky = SkillFX.Frames("MeteorSky");
        if (f.Length < 8 || sky.Length < 2) { Destroy(gameObject); yield break; }

        float scale = width / f[0].bounds.size.x;                 // 가로 폭 = width 유닛
        transform.localScale = Vector3.one * scale;
        float H = f[0].bounds.size.y * scale;
        var cam = Camera.main;
        top = cam != null ? cam.transform.position.y + cam.orthographicSize : ground.y + 9f;

        // 1) 하늘 맨 위 마법진
        transform.position = new Vector3(ground.x, top - H * 0.97f, ground.z);
        for (int i = 0; i < 2; i++) { sr.sprite = sky[i]; yield return new WaitForSeconds(0.18f); }

        // 2) 작은 돌 2개 먼저 (빠르게, 약간 시간차)
        StartCoroutine(SmallMeteor(ground + new Vector3(-smallSpread, 0, 0), 0f));
        StartCoroutine(SmallMeteor(ground + new Vector3(smallSpread, 0, 0), 0.15f));
        sr.sprite = sky[1];
        yield return new WaitForSeconds(0.5f);

        // 3) 큰 돌 낙하: 화면 위 바깥에서 가속하며 떨어짐
        sr.sprite = f[2];
        float startY = top + 1f - H * 0.33f, endY = ground.y;
        const float fall = 0.45f;
        for (float t = 0; t < fall; t += Time.deltaTime)
        {
            float k = t / fall; k *= k;
            transform.position = new Vector3(ground.x, Mathf.Lerp(startY, endY, k), ground.z);
            yield return null;
        }
        transform.position = ground;

        // 4) 착지 → 대폭발 → 불씨
        for (int i = 3; i < f.Length; i++)
        {
            sr.sprite = f[i];
            if (i == 4)
            {
                SkillFX.DamageArea((Vector2)ground + new Vector2(0, 2.5f), new Vector2(width, 5f), damage, attackerX);
                CameraShake.Shake(0.35f, 0.3f);
            }
            yield return new WaitForSeconds(1f / 12f);
        }
        for (float t = 1f; t > 0; t -= Time.deltaTime / 0.3f) { sr.color = new Color(1, 1, 1, t); yield return null; }
        Destroy(gameObject);
    }

    // 작은 돌: 오른쪽 위에서 비스듬히 빠르게 떨어져 작은 폭발 (데미지 smallDamage)
    IEnumerator SmallMeteor(Vector3 target, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        var small = SkillFX.Frames("MeteorSmall");
        if (small.Length == 0) yield break;
        var sr = SkillFX.MakeRenderer("MeteorSmall", target, false); sr.sortingOrder = 24; sr.sprite = small[0];
        float drop = top + 1.5f - target.y;
        Vector3 start = target + new Vector3(drop * 0.27f, drop, 0);   // 꼬리 방향(오른쪽 위)에서 날아옴
        const float fall = 0.28f;
        for (float t = 0; t < fall; t += Time.deltaTime)
        {
            float k = t / fall; k *= k;
            sr.transform.position = Vector3.Lerp(start, target, k);
            yield return null;
        }
        Destroy(sr.gameObject);

        // 작은 폭발 (큰 폭발 프레임을 작게 재생)
        var f = SkillFX.Frames("Meteor");
        var ex = SkillFX.MakeRenderer("MeteorSmallHit", target, false); ex.sortingOrder = 24;
        float w = 2.2f; ex.transform.localScale = Vector3.one * (w / f[0].bounds.size.x);
        for (int i = 3; i < f.Length; i++)
        {
            ex.sprite = f[i];
            if (i == 4)
            {
                SkillFX.DamageArea((Vector2)target + new Vector2(0, 1.2f), new Vector2(w, 2.4f), smallDamage, attackerX);
                CameraShake.Shake(0.12f, 0.1f);
            }
            yield return new WaitForSeconds(1f / 14f);
        }
        Destroy(ex.gameObject);
    }
}

// 간단한 화면 흔들림 (카메라 추적이 끝난 뒤 적용)
[DefaultExecutionOrder(50)]
public class CameraShake : MonoBehaviour
{
    float until, power;
    public static void Shake(float time, float strength)
    {
        var cam = Camera.main; if (cam == null) return;
        var s = cam.GetComponent<CameraShake>();
        if (s == null) s = cam.gameObject.AddComponent<CameraShake>();
        s.until = Time.time + time; s.power = strength;
    }
    void LateUpdate()
    {
        if (Time.time >= until) return;
        transform.position += (Vector3)(Random.insideUnitCircle * power);
    }
}
