// 슬라임 AI
//  - 평소: 제자리에서 대기하다가 가끔 근처를 어슬렁거림
//  - 플레이어가 감지 범위에 들어오면: 플레이어를 향해 달려가고, 가까워지면 몸을 날려 덮침
//  - 플레이어가 멀어지면: 원래 자리로 돌아감
//  - 몸에 닿으면 플레이어에게 데미지 + 넉백
//  - 맞으면 뒤로 밀리고 잠깐 멈춤, 체력 0이면 사라졌다가 리스폰 (EnemyHealth)
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class SlimeEnemy : MonoBehaviour
{
    [Header("감지")]
    public float detectRange = 4.5f;      // 이 거리 안에 들어오면 추격 시작
    public float loseRange = 8f;          // 이 거리보다 멀어지면 추격 포기
    public float heightTolerance = 2.5f;  // 위아래로 이 이상 떨어져 있으면 무시

    [Header("이동")]
    public float wanderSpeed = 0.8f;
    public float wanderRadius = 1.5f;     // 평소 어슬렁거리는 범위 (0이면 가만히)
    public float approachSpeed = 1.4f;    // 플레이어에게 다가오는 속도 (이전 2.8 → 절반)
    public float returnSpeed = 1.5f;

    [Header("덮치기 (달려들기)")]
    public float lungeRange = 1.8f;       // 이 거리 안이면 몸을 날림
    public Vector2 lungeVelocity = new Vector2(5.5f, 5f);
    public float lungeCooldown = 1.4f;

    [Header("공격")]
    public float attackDamage = 1f;       // 몸에 닿았을 때 플레이어가 받는 데미지
    public Vector2 knockback = new Vector2(6f, 4f);

    [Header("보상")]
    public int expReward = 1;          // 처치 시 경험치

    [Header("그림 방향")]
    public bool spriteFacesLeft = true;   // 슬라임 원본 그림은 왼쪽을 봄

    enum State { Idle, Chase, Return }
    State state = State.Idle;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb;
    float homeX, wanderTarget, nextWanderTime, nextLungeTime;
    bool lunging;
    EnemyHealth health; float stunUntil;
    float animBase = 1f;

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
        homeX = wanderTarget = transform.position.x;
        nextWanderTime = Time.time + Random.Range(1f, 3f);

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        health.OnDied += () => { if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward); };
        health.OnHurt += dir =>
        {
            stunUntil = Time.time + 0.35f;          // 맞으면 잠깐 멈칫
            lunging = false;
            Vel = new Vector2(dir * 3.5f, 3f);      // 뒤로 튕겨남
            state = State.Chase;                     // 맞으면 화나서 추격
        };
        health.OnRespawned += () => { state = State.Idle; lunging = false; wanderTarget = homeX; sr.flipX = false; };
        animBase = Random.Range(0.9f, 1.1f);   // 여러 마리가 똑같이 움직이지 않게
    }

    void Start()
    {
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform;
        playerHealth = go.GetComponent<PlayerHealth>();
        playerRb = go.GetComponent<Rigidbody2D>();
        // 플레이어와 몸으로 밀치지 않게 (닿으면 데미지만)
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    bool Grounded()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.1f;
        foreach (var h in Physics2D.RaycastAll(o, Vector2.down, 0.2f))
            if (h.collider != body && !h.collider.isTrigger && h.collider.attachedRigidbody != playerRb) return true;
        return false;
    }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); lunging = false; anim.SetFloat("Speed", 0f); anim.speed = 0.3f; return; }
        if (Time.time < stunUntil) { anim.SetFloat("Speed", 0f); anim.speed = animBase; return; }

        bool grounded = Grounded();
        if (lunging && grounded && Vel.y <= 0.01f) lunging = false;

        float dx = 0f, dy = 0f, dist = float.MaxValue;
        bool playerAlive = player != null && (playerHealth == null || !playerHealth.IsDead);
        if (playerAlive)
        {
            dx = player.position.x - transform.position.x;
            dy = player.position.y - transform.position.y;
            dist = Mathf.Abs(dx);
        }
        bool canSee = playerAlive && Mathf.Abs(dy) <= heightTolerance;

        // ── 상태 전환 ──
        switch (state)
        {
            case State.Idle:
                if (canSee && dist <= detectRange) state = State.Chase;
                break;
            case State.Chase:
                if (!canSee || dist > loseRange) state = State.Return;
                break;
            case State.Return:
                if (canSee && dist <= detectRange) state = State.Chase;
                else if (Mathf.Abs(transform.position.x - homeX) < 0.1f) { state = State.Idle; wanderTarget = homeX; }
                break;
        }

        // ── 행동 ──
        float moveX = 0f, speed = 0f;
        if (!lunging)
        {
            switch (state)
            {
                case State.Idle:
                    if (wanderRadius > 0 && Time.time >= nextWanderTime)
                    {
                        wanderTarget = homeX + Random.Range(-wanderRadius, wanderRadius);
                        nextWanderTime = Time.time + Random.Range(2.5f, 5f);
                    }
                    if (Mathf.Abs(wanderTarget - transform.position.x) > 0.1f)
                    { moveX = Mathf.Sign(wanderTarget - transform.position.x); speed = wanderSpeed; }
                    break;

                case State.Chase:
                    moveX = Mathf.Sign(dx); speed = approachSpeed;
                    if (grounded && dist <= lungeRange && Time.time >= nextLungeTime)
                    {
                        lunging = true;
                        nextLungeTime = Time.time + lungeCooldown;
                        Vel = new Vector2(Mathf.Sign(dx) * lungeVelocity.x, lungeVelocity.y);
                        Face(Mathf.Sign(dx));
                    }
                    break;

                case State.Return:
                    moveX = Mathf.Sign(homeX - transform.position.x); speed = returnSpeed;
                    break;
            }
            if (!lunging) Vel = new Vector2(moveX * speed, Vel.y);
            if (moveX != 0) Face(moveX);
        }

        anim.SetFloat("Speed", lunging ? 1f : Mathf.Abs(moveX * speed));
        anim.speed = animBase * (state == State.Chase || lunging ? 1.6f : 1f);   // 추격할 땐 빠르게 꿈틀
    }

    void Face(float dir) => sr.flipX = spriteFacesLeft ? dir > 0 : dir < 0;

    // 몸에 닿으면 데미지 (Trigger 충돌체 사용)
    void OnTriggerStay2D(Collider2D other)
    {
        if (health.IsDead || health.IsStunned || Time.time < stunUntil) return;
        if (playerHealth == null || other.attachedRigidbody != playerRb) return;
        if (playerHealth.IsDead) return;
        // 방어로 막으면 → 패링: 몬스터 스턴
        var guard = player != null ? player.GetComponent<SeriaController>() : null;
        if (guard != null && guard.TryBlock(transform.position, true))
        {
            health.Stun(guard.parryStunTime);
            OnParried();
            return;
        }

        float before = playerHealth.CurrentHP;
        playerHealth.TakeDamage(attackDamage);
        if (playerHealth.CurrentHP < before && playerRb != null)   // 실제로 맞았을 때만 넉백 (무적시간 고려)
        {
            float dir = Mathf.Sign(player.position.x - transform.position.x);
            if (dir == 0) dir = 1;
            SetVel(playerRb, new Vector2(dir * knockback.x, knockback.y));
        }
    }

    void OnParried()
    {
        lunging = false;
        float d = Mathf.Sign(transform.position.x - player.position.x); if (d == 0) d = 1;
        Vel = new Vector2(d * 4f, 3f);            // 튕겨나감
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.8f, 0, 0.6f); Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1, 0.2f, 0.2f, 0.6f); Gizmos.DrawWireSphere(transform.position, lungeRange);
    }
}
