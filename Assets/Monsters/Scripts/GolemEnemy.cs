// 돌 골렘 AI
//  평소: 아주 천천히 어슬렁 / 플레이어 발견: 아주 천천히 다가옴
//  던지기 범위 안: 멈춰서 돌을 던짐 (포물선으로 플레이어가 있던 곳을 향해), 쿨타임 후 반복
//  무거워서 맞아도 밀리지 않고, 던지는 동작도 끊기지 않음
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class GolemEnemy : MonoBehaviour
{
    [Header("감지")]
    public float detectRange = 11f;       // 이 안에 들어오면 다가옴
    public float throwRange = 8f;         // 이 안이면 돌 던지기
    public float heightTolerance = 3f;

    [Header("이동 (매우 느림)")]
    public float walkSpeed = 0.45f;
    public float wanderRadius = 1.5f;

    [Header("돌 던지기")]
    public float rockDamage = 10f;
    public float throwCooldown = 3f;
    public float releaseDelay = 0.4f;     // 던지는 동작 시작 후 돌이 손을 떠나는 시간
    public float throwAnimTime = 0.65f;
    public Vector2 handOffset = new Vector2(0.9f, 1.9f);
    public float rockFlightTime = 0.9f;

    [Header("기타")]
    public float touchDamage = 5f;        // 몸에 닿았을 때
    public int expReward = 15;
    public bool spriteFacesLeft = false;  // 골렘 원본 그림은 오른쪽을 봄

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float homeX, wanderTarget, nextWanderTime, nextThrowTime;
    bool throwing, aggro;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
    static void SetVel(Rigidbody2D r, Vector2 v) => r.linearVelocity = v;
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
    static void SetVel(Rigidbody2D r, Vector2 v) => r.velocity = v;
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); anim = GetComponent<Animator>(); sr = GetComponent<SpriteRenderer>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) body = c;
        homeX = wanderTarget = transform.position.x;
        nextWanderTime = Time.time + Random.Range(2f, 5f);
        nextThrowTime = Time.time + 1f;

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        health.OnHurt += dir => { aggro = true; Vel = new Vector2(0, Vel.y); };   // 밀리지 않음, 화나서 추격
        health.OnDied += () =>
        {
            StopAllCoroutines(); throwing = false;
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
        };
        health.OnStunned += () => { StopAllCoroutines(); throwing = false; nextThrowTime = Time.time + 2.5f; };   // 스턴되면 던지기 취소
        health.OnRespawned += () => { aggro = false; throwing = false; wanderTarget = homeX; nextThrowTime = Time.time + 1f; };
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    void Face(float dir) { if (dir != 0) sr.flipX = spriteFacesLeft ? dir > 0 : dir < 0; }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); anim.SetFloat("Speed", 0f); anim.speed = 0.3f; return; }
        anim.speed = 1f;
        if (throwing) return;

        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        float dx = alive ? player.position.x - transform.position.x : 0f;
        float dy = alive ? player.position.y - transform.position.y : 0f;
        float dist = Mathf.Abs(dx);
        bool canSee = alive && Mathf.Abs(dy) <= heightTolerance && (dist <= detectRange || (aggro && dist <= detectRange * 1.5f));

        float moveX = 0f;
        if (canSee)
        {
            Face(Mathf.Sign(dx));
            if (dist <= throwRange)
            {
                if (Time.time >= nextThrowTime) { StartCoroutine(Throw()); return; }
            }
            else moveX = Mathf.Sign(dx);                              // 천천히 다가옴
        }
        else
        {
            aggro = false;
            if (wanderRadius > 0 && Time.time >= nextWanderTime)
            { wanderTarget = homeX + Random.Range(-wanderRadius, wanderRadius); nextWanderTime = Time.time + Random.Range(4f, 7f); }
            if (Mathf.Abs(wanderTarget - transform.position.x) > 0.1f) { moveX = Mathf.Sign(wanderTarget - transform.position.x); Face(moveX); }
        }

        Vel = new Vector2(moveX * walkSpeed, Vel.y);
        anim.SetFloat("Speed", Mathf.Abs(moveX));
    }

    IEnumerator Throw()
    {
        throwing = true;
        Vel = new Vector2(0, Vel.y);
        anim.SetFloat("Speed", 0f);
        anim.SetTrigger("Throw");
        float dir = sr.flipX == spriteFacesLeft ? 1f : -1f;
        yield return new WaitForSeconds(releaseDelay);

        if (player != null && !health.IsDead)
        {
            Vector3 hand = transform.position + new Vector3(handOffset.x * dir, handOffset.y, 0);
            Vector3 target = player.position + new Vector3(0, 0.9f, 0);           // 플레이어 가슴 높이 (숙이면 머리 위로 지나감)
            GolemRock.Throw(hand, target, rockFlightTime, rockDamage);
        }
        yield return new WaitForSeconds(Mathf.Max(0f, throwAnimTime - releaseDelay));
        nextThrowTime = Time.time + throwCooldown;
        throwing = false;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || health.IsStunned || playerHealth == null || playerHealth.IsDead || other.attachedRigidbody != playerRb) return;
        // 방어로 막으면 → 패링: 몬스터 스턴
        var guard = player != null ? player.GetComponent<SeriaController>() : null;
        if (guard != null && guard.TryBlock(transform.position, true))
        {
            health.Stun(guard.parryStunTime);
            OnParried();
            return;
        }
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(touchDamage);
        if (playerHealth.CurrentHP < before && playerRb != null)
            SetVel(playerRb, new Vector2(Mathf.Sign(player.position.x - transform.position.x) * 5f, 3f));
    }

    void OnParried() { }                        // 무거워서 밀리지 않음

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.8f, 0, 0.5f); Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1, 0.3f, 0.2f, 0.6f); Gizmos.DrawWireSphere(transform.position, throwRange);
    }
}

// 골렘이 던진 돌: 포물선으로 날아가 플레이어에 맞으면 데미지, 땅에 떨어지면 부서짐
public class GolemRock : MonoBehaviour
{
    public float damage = 10f;
    Vector2 vel; const float Gravity = -14f;
    SpriteRenderer sr; Sprite[] spin; float life, animT; int frame;

    public static void Throw(Vector3 from, Vector3 target, float flightTime, float damage)
    {
        var go = new GameObject("Golem Rock");
        go.transform.position = from;
        var r = go.AddComponent<GolemRock>();
        r.damage = damage;
        // 정해진 시간에 목표 지점에 닿는 포물선 속도
        float T = Mathf.Max(0.3f, flightTime);
        r.vel = new Vector2((target.x - from.x) / T, (target.y - from.y - 0.5f * Gravity * T * T) / T);
    }

    void Start()
    {
        sr = gameObject.AddComponent<SpriteRenderer>(); sr.sortingOrder = 22;
        spin = Resources.LoadAll<Sprite>("Golem/Golem_Rock");
        System.Array.Sort(spin, (a, b) => a.name.CompareTo(b.name));
        if (spin.Length > 0) sr.sprite = spin[0];
    }

    void Update()
    {
        float dt = Time.deltaTime; life += dt;
        vel.y += Gravity * dt;
        transform.position += (Vector3)(vel * dt);
        // 회전하는 프레임 (앞의 6장 반복)
        animT += dt; if (spin != null && spin.Length >= 6 && animT > 0.06f) { animT = 0; frame = (frame + 1) % 6; sr.sprite = spin[frame]; }

        foreach (var c in Physics2D.OverlapCircleAll(transform.position, 0.32f))
        {
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;           // 몬스터는 통과
            var hp = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp != null)
            {
                if (hp.IsDead) continue;
                var guard = hp.GetComponent<SeriaController>();
                if (guard != null && guard.TryBlock(transform.position, false)) { Break(); return; }   // 방어로 막음 (패링은 아님)
                float before = hp.CurrentHP;
                hp.TakeDamage(damage);
                if (hp.CurrentHP < before)
                {
#if UNITY_6000_0_OR_NEWER
                    c.attachedRigidbody.linearVelocity = new Vector2(Mathf.Sign(vel.x) * 6f, 4f);
#else
                    c.attachedRigidbody.velocity = new Vector2(Mathf.Sign(vel.x) * 6f, 4f);
#endif
                }
                Break(); return;
            }
            if (!c.isTrigger) { Break(); return; }                                   // 땅·벽
        }
        if (life > 4f) Destroy(gameObject);
    }

    void Break()
    {
        GolemImpact.Play(transform.position);
        Destroy(gameObject);
    }
}

// 돌이 부서지는 착탄 이펙트
public class GolemImpact : MonoBehaviour
{
    public static void Play(Vector3 pos)
    {
        var go = new GameObject("Golem Rock Impact");
        go.transform.position = pos + new Vector3(0, -0.3f, 0);
        go.AddComponent<GolemImpact>();
    }

    IEnumerator Start()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>(); sr.sortingOrder = 23;
        var f = Resources.LoadAll<Sprite>("Golem/Golem_Impact");
        System.Array.Sort(f, (a, b) => Idx(a.name).CompareTo(Idx(b.name)));
        foreach (var s in f) { sr.sprite = s; yield return new WaitForSeconds(1f / 14f); }
        Destroy(gameObject);
    }
    static int Idx(string n) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out var v) ? v : 0; }
}
