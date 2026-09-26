// 정령의 숲 중간 보스: 타락한 거대 나무정령
//  - 세리아를 향해 매우 천천히 걸어옴
//  - 공격: 땅 / 하늘에서 대각선으로 솟아오르는 뿌리·가지 (1발당 50)
//      · 땅 : 초록 경고 표시 → 세리아 쪽으로 차례차례 대각선 뿌리가 솟아오름
//      · 하늘: 화면 위 경고 표시 → 가지가 하늘에서 대각선으로 내리꽂힘
//      · 3번째 공격마다 땅 + 하늘 동시
//  - 원거리 공격이라 방어하면 막을 수 있지만(BLOCK) 패링 스턴은 없음
//  - 공격 중엔 맞아도 멈추지 않음, 쓰러지면 다시 나타나지 않음(보스)
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class TreeBossEnemy : MonoBehaviour
{
    [Header("수치")]
    public float rootDamage = 50f;
    public float touchDamage = 10f;        // 몸에 닿았을 때 (따로 정하지 않아 임시)
    public int expReward = 200;

    [Header("행동")]
    public float detectRange = 14f;
    public float walkSpeed = 0.35f;        // 매우 느림
    public float keepDistance = 3.5f;      // 이보다 가까우면 더 다가오지 않음
    public float attackInterval = 3.2f;
    public float warnTime = 0.9f;          // 경고 표시 후 공격까지

    [Header("땅 뿌리")]
    public int groundCount = 3;
    public float groundSpacing = 1.8f;
    public float groundDelay = 0.18f;      // 뿌리 사이 시간차
    public float rootTilt = 25f;           // 대각선 기울기(도)

    [Header("하늘 가지")]
    public int skyCount = 3;
    public float skySpacing = 2.2f;

    public bool spriteFacesLeft = false;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health; Color baseColor;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float nextAttack; bool attacking; int attackCount; Coroutine co;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); anim = GetComponent<Animator>(); sr = GetComponent<SpriteRenderer>(); baseColor = sr.color;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) body = c;
        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        health.isBoss = true; if (string.IsNullOrEmpty(health.bossId)) health.bossId = "CorruptedTreeSpirit";
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.3f;
        nextAttack = Time.time + 2f;
        health.OnDied += () =>
        {
            if (co != null) StopCoroutine(co); attacking = false; sr.color = baseColor;
            anim.speed = 1f; anim.Play("Death", 0, 0f);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
        };
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    void Face(float d) { if (d != 0) sr.flipX = spriteFacesLeft ? d > 0 : d < 0; }

    void Update()
    {
        if (health.IsDead || attacking) return;
        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        if (!alive) { Vel = new Vector2(0, Vel.y); anim.Play("Idle"); return; }
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) > detectRange) { Vel = new Vector2(0, Vel.y); anim.Play("Idle"); return; }
        Face(Mathf.Sign(dx));

        if (Mathf.Abs(dx) > keepDistance) { Vel = new Vector2(Mathf.Sign(dx) * walkSpeed, Vel.y); anim.Play("Walk"); }
        else { Vel = new Vector2(0, Vel.y); anim.Play("Idle"); }

        if (Time.time >= nextAttack) co = StartCoroutine(Attack());
    }

    IEnumerator Attack()
    {
        attacking = true; attackCount++;
        Vel = new Vector2(0, Vel.y);
        anim.Play("Idle", 0, 0f);
        bool ground = attackCount % 3 != 2, sky = attackCount % 3 != 1;      // 땅 → 하늘 → 둘 다 → 반복
        DamagePopup.ShowText(transform.position + new Vector3(0, 4.4f, 0), "!", new Color(0.5f, 1f, 0.4f));

        // 공격 준비: 몸이 초록빛으로 맥동
        float tx = player.position.x, dir = Mathf.Sign(tx - transform.position.x); if (dir == 0) dir = 1;
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        { sr.color = Color.Lerp(baseColor, new Color(0.6f, 1f, 0.55f), Mathf.PingPong(t * 6f, 1f)); yield return null; }
        sr.color = baseColor;

        if (ground)
            for (int i = 0; i < groundCount; i++)
            {
                float x = tx + dir * (i - 1) * groundSpacing;
                StartCoroutine(GroundRoot(new Vector3(x, transform.position.y, 0), dir, warnTime + i * groundDelay));
            }
        if (sky)
            for (int i = 0; i < skyCount; i++)
            {
                float x = tx + (i - (skyCount - 1) / 2f) * skySpacing;
                StartCoroutine(SkyBranch(new Vector3(x, transform.position.y, 0), dir, warnTime + 0.25f + i * 0.15f));
            }

        yield return new WaitForSeconds(warnTime + 1.4f);
        nextAttack = Time.time + attackInterval;
        attacking = false; co = null;
    }

    // ── 땅에서 대각선으로 솟는 뿌리 ──
    IEnumerator GroundRoot(Vector3 pos, float dir, float delay)
    {
        var warn = RootFX.Warn(pos, false);
        yield return new WaitForSeconds(delay);
        if (warn) Destroy(warn);
        if (health.IsDead) yield break;
        var f = RootFX.Frames();
        var r = RootFX.Make(pos, -dir * rootTilt, false);
        CameraShake.Shake(0.1f, 0.08f);
        bool hit = false;
        for (int i = 0; i < f.Length; i++)
        {
            r.sprite = f[i];
            if (i >= 1 && !hit) hit = RootFX.Hit(r, rootDamage, dir, this);
            yield return new WaitForSeconds(0.07f);
        }
        for (float t = 0; t < 0.35f; t += Time.deltaTime) { if (!hit) hit = RootFX.Hit(r, rootDamage, dir, this); yield return null; }
        yield return RootFX.FadeOut(r, 0.3f);
    }

    // ── 하늘에서 대각선으로 내리꽂히는 가지 ──
    IEnumerator SkyBranch(Vector3 ground, float dir, float delay)
    {
        var cam = Camera.main;
        float top = cam != null ? cam.transform.position.y + cam.orthographicSize : ground.y + 9f;
        var warn = RootFX.Warn(new Vector3(ground.x, top - 0.4f, 0), true);
        var warn2 = RootFX.Warn(ground, false);
        yield return new WaitForSeconds(delay);
        if (warn) Destroy(warn); if (warn2) Destroy(warn2);
        if (health.IsDead) yield break;
        var f = RootFX.Frames();
        var r = RootFX.Make(ground, 180f - dir * rootTilt, true);        // 거꾸로 (끝이 아래), 세리아 반대쪽 위에서 날아옴
        r.sprite = f[2];
        float len = r.sprite != null ? r.sprite.bounds.size.y : 3f;
        Vector3 up = r.transform.up;                                   // 밑동 → 끝 방향 (아래쪽 대각선)
        Vector3 end = ground - up * len;                                // 끝이 땅에 닿는 위치
        Vector3 start = end - up * (top - ground.y + len + 1f);          // 화면 위 바깥에서 출발
        bool hit = false;
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f; k *= k;
            r.transform.position = Vector3.Lerp(start, end, k);
            if (!hit) hit = RootFX.Hit(r, rootDamage, -dir, this);
            yield return null;
        }
        r.transform.position = end;
        CameraShake.Shake(0.15f, 0.12f);
        r.sprite = f[3];
        for (float t = 0; t < 0.35f; t += Time.deltaTime) { if (!hit) hit = RootFX.Hit(r, rootDamage, -dir, this); yield return null; }
        yield return RootFX.FadeOut(r, 0.3f);
    }

    public void OnRootHit(PlayerHealth hp, float dir)
    {
        var guard = hp.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(transform.position, false)) return;   // 방어 가능, 스턴 없음
        float before = hp.CurrentHP;
        hp.TakeDamage(rootDamage);
        if (hp.CurrentHP < before && playerRb != null)
        {
#if UNITY_6000_0_OR_NEWER
            playerRb.linearVelocity = new Vector2(dir * 6f, 7f);
#else
            playerRb.velocity = new Vector2(dir * 6f, 7f);
#endif
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || playerHealth == null || playerHealth.IsDead || other.attachedRigidbody != playerRb) return;
        var guard = player.GetComponent<SeriaController>();
        if (guard != null && guard.TryBlock(transform.position, false)) return;
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(touchDamage);
        if (playerHealth.CurrentHP < before)
        {
            float d = Mathf.Sign(player.position.x - transform.position.x); if (d == 0) d = 1;
#if UNITY_6000_0_OR_NEWER
            playerRb.linearVelocity = new Vector2(d * 6f, 4f);
#else
            playerRb.velocity = new Vector2(d * 6f, 4f);
#endif
        }
    }
}

// 뿌리 그림·경고 표시·판정 도우미
public static class RootFX
{
    static Sprite[] frames; static Sprite warnSprite;

    public static Sprite[] Frames()
    {
        if (frames != null && frames.Length > 0) return frames;
        frames = Resources.LoadAll<Sprite>("TreeBoss/TreeBoss_Root");
        System.Array.Sort(frames, (a, b) => a.name.CompareTo(b.name));
        return frames;
    }

    public static SpriteRenderer Make(Vector3 pos, float angle, bool sky)
    {
        var g = new GameObject(sky ? "Sky Branch" : "Ground Root");
        g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, angle));
        var r = g.AddComponent<SpriteRenderer>(); r.sortingOrder = 21;
        return r;
    }

    // 초록 경고 (땅: 납작한 빛, 하늘: 위쪽 빛)
    public static GameObject Warn(Vector3 pos, bool sky)
    {
        if (warnSprite == null)
        {
            const int w = 64, h = 32; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x - w / 2f + 0.5f) / (w / 2f), dy = (y - h / 2f + 0.5f) / (h / 2f); float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d); a = a * a + (d > 0.75f && d < 0.95f ? 0.6f : 0f);
                tex.SetPixel(x, y, new Color(0.45f, 1f, 0.35f, Mathf.Clamp01(a)));
            }
            tex.Apply(); warnSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
        var g = new GameObject("Root Warning"); g.transform.position = pos + new Vector3(0, sky ? 0 : 0.08f, 0);
        var r = g.AddComponent<SpriteRenderer>(); r.sprite = warnSprite; r.sortingOrder = 19;
        g.transform.localScale = sky ? new Vector3(0.8f, 0.8f, 1) : new Vector3(1f, 0.5f, 1);
        g.AddComponent<WarnPulse>();
        return g;
    }

    // 기울어진 뿌리 모양의 판정 박스
    public static bool Hit(SpriteRenderer r, float dmg, float dir, TreeBossEnemy owner)
    {
        if (r == null || r.sprite == null) return false;
        float len = r.sprite.bounds.size.y * r.transform.localScale.y;
        Vector2 up = r.transform.up;
        Vector2 center = (Vector2)r.transform.position + up * len * 0.5f;
        foreach (var c in Physics2D.OverlapBoxAll(center, new Vector2(0.9f, len * 0.9f), r.transform.eulerAngles.z))
        {
            var hp = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp == null || hp.IsDead) continue;
            owner.OnRootHit(hp, dir);
            return true;
        }
        return false;
    }

    public static IEnumerator FadeOut(SpriteRenderer r, float time)
    {
        for (float t = 0; t < time && r != null; t += Time.deltaTime) { r.color = new Color(1, 1, 1, 1 - t / time); yield return null; }
        if (r != null) Object.Destroy(r.gameObject);
    }
}

public class WarnPulse : MonoBehaviour
{
    SpriteRenderer r; Vector3 s0; float t;
    void Start() { r = GetComponent<SpriteRenderer>(); s0 = transform.localScale; }
    void Update()
    {
        t += Time.deltaTime;
        float k = 0.75f + 0.25f * Mathf.Sin(t * 14f);
        r.color = new Color(1, 1, 1, Mathf.Min(1f, t * 3f) * k);
        transform.localScale = s0 * (0.8f + 0.2f * Mathf.Min(1f, t * 2f));
    }
}
