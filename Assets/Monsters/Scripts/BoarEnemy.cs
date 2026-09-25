// 멧돼지 AI - 돌진형
//  평소: 대기 / 어슬렁
//  플레이어 발견 → 준비(제자리 발구르기 + "!") → 일직선으로 빠르게 돌진 → 잠깐 숨 고르기 → 반복
//  돌진 중에는 방향을 못 바꿈 (점프·대쉬로 피할 수 있음), 맞아도 멈추지 않음(슈퍼아머)
//  벽에 부딪히면 돌진 중단
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class BoarEnemy : MonoBehaviour
{
    [Header("감지")]
    public float detectRange = 6f;
    public float loseRange = 10f;
    public float heightTolerance = 2.5f;

    [Header("평소 이동")]
    public float wanderSpeed = 1f;
    public float wanderRadius = 2f;
    public float returnSpeed = 2f;

    [Header("돌진")]
    public float windupTime = 0.6f;       // 돌진 전 준비 시간
    public float chargeSpeed = 10f;
    public float chargeMaxTime = 1.0f;    // 최대 돌진 시간
    public float recoverTime = 1.0f;      // 돌진 후 숨 고르기 (공격 찬스)

    [Header("공격")]
    public float chargeDamage = 3f;       // 돌진에 맞았을 때
    public float touchDamage = 1f;        // 평소 몸에 닿았을 때
    public Vector2 chargeKnockback = new Vector2(9f, 5f);
    public Vector2 touchKnockback = new Vector2(4f, 2f);

    [Header("보상")]
    public int expReward = 3;          // 처치 시 경험치

    [Header("그림 방향")]
    public bool spriteFacesLeft = false;  // 멧돼지 원본 그림은 오른쪽을 봄

    enum State { Idle, Windup, Charge, Recover, Return }
    State state = State.Idle;
    float stateEnd, chargeDir;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float homeX, wanderTarget, nextWanderTime, stunUntil;
    Color baseColor;

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
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;   // 떨림 방지
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) body = c;
        baseColor = sr.color;
        homeX = wanderTarget = transform.position.x;
        nextWanderTime = Time.time + Random.Range(1f, 4f);

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        health.OnDied += () => { if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward); };
        health.OnHurt += dir =>
        {
            if (state == State.Charge) return;          // 돌진 중엔 안 밀림
            stunUntil = Time.time + 0.2f;
            Vel = new Vector2(dir * 2f, 1.5f);          // 무거워서 조금만 밀림
            if (state == State.Idle || state == State.Return) Enter(State.Windup);   // 맞으면 바로 돌진 준비
        };
        health.OnRespawned += () => { state = State.Idle; wanderTarget = homeX; sr.color = baseColor; };
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    void Enter(State s)
    {
        state = s;
        switch (s)
        {
            case State.Windup:
                stateEnd = Time.time + windupTime;
                if (player != null) Face(Mathf.Sign(player.position.x - transform.position.x));
                DamagePopup.ShowText(transform.position + new Vector3(0, 1.9f, 0), "!", new Color(1f, 0.3f, 0.2f));
                break;
            case State.Charge:
                chargeDir = FacingDir();
                stateEnd = Time.time + chargeMaxTime;
                break;
            case State.Recover:
                stateEnd = Time.time + recoverTime;
                Vel = new Vector2(Vel.x * 0.3f, Vel.y);
                break;
        }
        sr.color = baseColor;
    }

    float FacingDir() => (spriteFacesLeft ? sr.flipX : !sr.flipX) ? 1f : -1f;
    void Face(float dir) { if (dir != 0) sr.flipX = spriteFacesLeft ? dir > 0 : dir < 0; }

    bool WallAhead(float dir)
    {
        Vector2 o = (Vector2)transform.position + new Vector2(0, 0.5f);
        foreach (var h in Physics2D.RaycastAll(o, new Vector2(dir, 0), 1.1f))
        {
            if (h.collider == body || h.collider.isTrigger) continue;
            if (h.collider.attachedRigidbody == playerRb && playerRb != null) continue;
            if (h.collider.GetComponentInParent<EnemyHealth>() != null) continue;   // 다른 몬스터는 벽 아님
            return true;
        }
        return false;
    }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); anim.SetFloat("Speed", 0f); anim.speed = 0.3f; state = State.Idle; return; }
        if (Time.time < stunUntil) { anim.SetFloat("Speed", 0f); anim.speed = 1f; return; }

        float dx = 0, dy = 0, dist = float.MaxValue;
        bool alive = player != null && (playerHealth == null || !playerHealth.IsDead);
        if (alive) { dx = player.position.x - transform.position.x; dy = player.position.y - transform.position.y; dist = Mathf.Abs(dx); }
        bool canSee = alive && Mathf.Abs(dy) <= heightTolerance;

        float moveX = 0, speed = 0;
        switch (state)
        {
            case State.Idle:
                if (canSee && dist <= detectRange) { Enter(State.Windup); break; }
                if (wanderRadius > 0 && Time.time >= nextWanderTime)
                { wanderTarget = homeX + Random.Range(-wanderRadius, wanderRadius); nextWanderTime = Time.time + Random.Range(3f, 6f); }
                if (Mathf.Abs(wanderTarget - transform.position.x) > 0.1f) { moveX = Mathf.Sign(wanderTarget - transform.position.x); speed = wanderSpeed; }
                break;

            case State.Windup:
                // 제자리에서 발구르기 + 붉게 달아오름
                if (alive) Face(Mathf.Sign(dx));
                float k = 1f - (stateEnd - Time.time) / windupTime;
                sr.color = Color.Lerp(baseColor, new Color(1f, 0.6f, 0.55f), Mathf.PingPong(k * 6f, 1f));
                Vel = new Vector2(0, Vel.y);
                anim.SetFloat("Speed", 1f); anim.speed = 2.2f;
                if (Time.time >= stateEnd) Enter(State.Charge);
                return;

            case State.Charge:
                Vel = new Vector2(chargeDir * chargeSpeed, Vel.y);
                anim.SetFloat("Speed", 1f); anim.speed = 2.8f;
                if (Time.time >= stateEnd || WallAhead(chargeDir)) Enter(State.Recover);
                return;

            case State.Recover:
                Vel = new Vector2(Mathf.MoveTowards(Vel.x, 0, 30f * Time.deltaTime), Vel.y);
                anim.SetFloat("Speed", 0f); anim.speed = 1f;
                if (Time.time >= stateEnd)
                {
                    if (canSee && dist <= loseRange) Enter(State.Windup);
                    else state = State.Return;
                }
                return;

            case State.Return:
                if (canSee && dist <= detectRange) { Enter(State.Windup); break; }
                if (Mathf.Abs(transform.position.x - homeX) < 0.15f) { state = State.Idle; wanderTarget = homeX; break; }
                moveX = Mathf.Sign(homeX - transform.position.x); speed = returnSpeed;
                break;
        }

        if (state == State.Idle || state == State.Return)
        {
            Vel = new Vector2(moveX * speed, Vel.y);
            Face(moveX);
            anim.SetFloat("Speed", Mathf.Abs(moveX * speed));
            anim.speed = 1f;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || health.IsStunned || playerHealth == null || playerHealth.IsDead) return;
        if (other.attachedRigidbody != playerRb) return;
        // 방어로 막으면 → 패링: 몬스터 스턴
        var guard = player != null ? player.GetComponent<SeriaController>() : null;
        if (guard != null && guard.TryBlock(transform.position, true))
        {
            health.Stun(guard.parryStunTime);
            OnParried();
            return;
        }

        bool charging = state == State.Charge;
        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(charging ? chargeDamage : touchDamage);
        if (playerHealth.CurrentHP < before && playerRb != null)
        {
            float dir = charging ? chargeDir : Mathf.Sign(player.position.x - transform.position.x);
            if (dir == 0) dir = 1;
            var kb = charging ? chargeKnockback : touchKnockback;
            SetVel(playerRb, new Vector2(dir * kb.x, kb.y));
        }
    }

    void OnParried()
    {
        float d = Mathf.Sign(transform.position.x - player.position.x); if (d == 0) d = 1;
        Vel = new Vector2(d * 5f, 2f);            // 돌진이 막혀 튕겨나감
        state = State.Idle; sr.color = baseColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.5f, 0, 0.6f); Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
