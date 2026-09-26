// 네크로맨서 - 멸망한 왕국 성곽3
//  거리가 떨어진 상태에서 세리아를 발견하면: 지팡이를 들어 스켈레톤 검사 1 + 궁수 1 소환
//   · 소환한 스켈레톤이 모두 쓰러진 뒤, 다시 소환하려면 summonCooldown 초가 지나야 함
//  세리아가 가까이 오면: 지팡이로 보랏빛 마력을 휘둘러 공격 (공격력 10, 방어로 막으면 스턴 없음)
//  몸에 닿는 것만으로는 데미지 없음
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class NecromancerEnemy : MonoBehaviour
{
    [Header("수치")]
    public float meleeDamage = 10f;
    public int expReward = 100;

    [Header("감지 · 이동")]
    public float detectRange = 10f;
    public float heightTolerance = 2.5f;
    public float walkSpeed = 0.9f;
    public float approachUntil = 5f;       // 이보다 멀면 천천히 다가옴

    [Header("소환")]
    public GameObject swordsmanPrefab;     // 스켈레톤 검사
    public GameObject archerPrefab;        // 스켈레톤 궁수
    public float summonMinDistance = 3.5f; // 세리아가 이보다 멀리 있을 때만 소환
    public float summonCooldown = 12f;     // 소환수가 모두 쓰러진 뒤 다시 소환까지
    public float castFps = 10f;            // 소환 동작 7장
    public int spawnFrame = 5;             // 몇 번째 장면에서 소환수가 나타나는지
    [Tooltip("끄면 소환한 스켈레톤이 경험치를 주지 않음 (뱀파이어 보스가 소환한 네크로맨서 등)")]
    public bool summonsGiveExp = true;

    [Header("근접 공격")]
    public float meleeRange = 1.8f;
    public float attackFps = 12f;          // 5장
    public int hitFrame = 3;
    public Vector2 hitBoxSize = new Vector2(2.2f, 1.7f);
    public float hitBoxForward = 1.0f;
    public float attackCooldown = 1.5f;

    public bool spriteFacesLeft = false;   // 원본 그림은 오른쪽을 봄

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D body; EnemyHealth health;
    Transform player; PlayerHealth playerHealth; Rigidbody2D playerRb; StageBounds stage;
    readonly List<EnemyHealth> summons = new List<EnemyHealth>();
    float nextAttackTime, nextSummonTime, hurtUntil;
    bool busy, summonsWereAlive; Coroutine actCo; string curState;

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

        health = GetComponent<EnemyHealth>();
        if (health == null) health = gameObject.AddComponent<EnemyHealth>();
        if (health.deathAnimTime <= 0f) health.deathAnimTime = 1.0f;

        health.OnHurt += dir => { if (busy) return; Vel = new Vector2(dir * 2f, Vel.y); hurtUntil = Time.time + 0.25f; };
        health.OnDied += () =>
        {
            Cancel(); Vel = Vector2.zero;
            Play("Death", true);
            if (PlayerLevel.Instance != null) PlayerLevel.Instance.AddExp(expReward);
        };
        health.OnStunned += () => { Cancel(); Play("Idle", true); nextAttackTime = Time.time + 2f; };
        health.OnRespawned += () => { busy = false; Play("Idle", true); nextAttackTime = Time.time + 1f; nextSummonTime = Time.time + 1f; };
    }

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        stage = Object.FindFirstObjectByType<StageBounds>();
#else
        stage = Object.FindObjectOfType<StageBounds>();
#endif
        var go = GameObject.Find("Seria");
        if (go == null) return;
        player = go.transform; playerHealth = go.GetComponent<PlayerHealth>(); playerRb = go.GetComponent<Rigidbody2D>();
        var pc = go.GetComponent<Collider2D>();
        if (pc != null && body != null) Physics2D.IgnoreCollision(pc, body);
    }

    float Facing => sr.flipX == spriteFacesLeft ? 1f : -1f;
    void Face(float dir) { if (dir != 0) sr.flipX = spriteFacesLeft ? dir > 0 : dir < 0; }

    void Play(string state, bool restart = false)
    {
        if (!restart && curState == state) return;
        curState = state; anim.Play(state, 0, 0f);
    }

    int AliveSummons()
    {
        summons.RemoveAll(s => s == null || s.IsDead);
        return summons.Count;
    }

    void Update()
    {
        if (health.IsDead) return;
        if (health.IsStunned) { Vel = new Vector2(0, Vel.y); anim.speed = 0.3f; return; }
        anim.speed = 1f;

        // 소환수가 모두 쓰러진 순간부터 재소환 대기시간 시작
        int alive = AliveSummons();
        if (summonsWereAlive && alive == 0) nextSummonTime = Time.time + summonCooldown;
        summonsWereAlive = alive > 0;

        if (busy || Time.time < hurtUntil) return;

        bool ok = player != null && (playerHealth == null || !playerHealth.IsDead);
        float dx = ok ? player.position.x - transform.position.x : 0f;
        float dy = ok ? player.position.y - transform.position.y : 0f;
        float dist = Mathf.Abs(dx);
        bool canSee = ok && Mathf.Abs(dy) <= heightTolerance && dist <= detectRange;

        float moveX = 0f;
        if (canSee)
        {
            Face(Mathf.Sign(dx));
            if (dist <= meleeRange)
            {
                if (Time.time >= nextAttackTime) { actCo = StartCoroutine(Melee()); return; }
            }
            else if (dist >= summonMinDistance && alive == 0 && Time.time >= nextSummonTime && (swordsmanPrefab != null || archerPrefab != null))
            {
                actCo = StartCoroutine(Summon()); return;
            }
            else if (dist > approachUntil) moveX = Mathf.Sign(dx);
        }

        Vel = new Vector2(moveX * walkSpeed, Vel.y);
        Play(moveX != 0 ? "Walk" : "Idle");
    }

    IEnumerator Summon()
    {
        busy = true;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("Summon", true);
        DamagePopup.ShowText(transform.position + new Vector3(0, 2.0f, 0), "소환!", new Color(0.8f, 0.5f, 1f));

        // 소환 위치에 마법진이 먼저 열림
        Vector3 swordPos = SpawnPos(dir * 2.0f), archerPos = SpawnPos(-dir * 1.5f);
        if (swordsmanPrefab != null) NecroSummonFx.Play(swordPos);
        if (archerPrefab != null) NecroSummonFx.Play(archerPos);
        yield return new WaitForSeconds(spawnFrame / castFps);

        if (!health.IsDead)
        {
            if (swordsmanPrefab != null) Spawn(swordsmanPrefab, swordPos, dir);   // 검사는 세리아 쪽 앞에
            if (archerPrefab != null) Spawn(archerPrefab, archerPos, dir);        // 궁수는 뒤에
            summonsWereAlive = summons.Count > 0;
        }
        yield return new WaitForSeconds(Mathf.Max(0.1f, (7 - spawnFrame) / castFps + 0.2f));

        Play("Idle", true);
        nextAttackTime = Time.time + 0.5f;
        busy = false; actCo = null;
    }

    Vector3 SpawnPos(float offset)
    {
        float x = transform.position.x + offset;
        if (stage != null) x = Mathf.Clamp(x, stage.left + 1f, stage.right - 1f);
        return new Vector3(x, transform.position.y, 0);
    }

    void Spawn(GameObject prefab, Vector3 pos, float dir)
    {
        var g = Instantiate(prefab, pos, Quaternion.identity);
        g.name = prefab.name + " (소환)";
        var eh = g.GetComponent<EnemyHealth>();
        if (eh != null) { eh.respawnTime = 0f; summons.Add(eh); }          // 소환수는 다시 살아나지 않음
        if (!summonsGiveExp)
        {
            var sk = g.GetComponent<SkeletonEnemy>(); if (sk != null) sk.expReward = 0;
            var ar = g.GetComponent<SkeletonArcherEnemy>(); if (ar != null) ar.expReward = 0;
        }
        var gsr = g.GetComponent<SpriteRenderer>();
        if (gsr != null) { gsr.flipX = dir < 0; StartCoroutine(FadeIn(gsr)); }
    }

    // 소환한 스켈레톤을 모두 무너뜨림 (주인 보스가 쓰러졌을 때 등)
    public void KillAllSummons()
    {
        foreach (var s in summons) if (s != null && !s.IsDead) s.TakeDamage(99999f, transform.position.x);
        summons.Clear();
    }

    IEnumerator FadeIn(SpriteRenderer s)
    {
        for (float t = 0; t < 0.4f && s != null; t += Time.deltaTime) { s.color = new Color(0.8f, 0.6f, 1f, t / 0.4f); yield return null; }
        if (s != null) s.color = Color.white;
    }

    IEnumerator Melee()
    {
        busy = true;
        Vel = new Vector2(0, Vel.y);
        float dir = Facing;
        Play("Attack", true);
        yield return new WaitForSeconds(hitFrame / attackFps);
        HitCheck(dir);
        yield return new WaitForSeconds(Mathf.Max(0.1f, (5 - hitFrame) / attackFps + 0.1f));
        Play("Idle", true);
        nextAttackTime = Time.time + attackCooldown;
        busy = false; actCo = null;
    }

    void HitCheck(float dir)
    {
        if (playerHealth == null || playerHealth.IsDead || playerRb == null) return;
        Vector2 center = (Vector2)transform.position + new Vector2(dir * hitBoxForward, hitBoxSize.y * 0.5f + 0.1f);
        foreach (var c in Physics2D.OverlapBoxAll(center, hitBoxSize, 0f))
        {
            if (c.attachedRigidbody != playerRb) continue;
            var guard = player.GetComponent<SeriaController>();
            if (guard != null && guard.TryBlock(transform.position, false)) return;
            float before = playerHealth.CurrentHP;
            playerHealth.TakeDamage(meleeDamage);
            if (playerHealth.CurrentHP < before) SetVel(playerRb, new Vector2(dir * 5f, 3f));
            return;
        }
    }

    void Cancel()
    {
        if (actCo != null) StopCoroutine(actCo);
        actCo = null; busy = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.5f); Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f); Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}

// 소환 이펙트: 바닥에 보랏빛 마법진이 열리고, 해골 기운이 솟아오른 뒤 사라짐
public class NecroSummonFx : MonoBehaviour
{
    public static void Play(Vector3 pos) { var g = new GameObject("Necro Summon Fx"); g.transform.position = pos; g.AddComponent<NecroSummonFx>(); }

    IEnumerator Start()
    {
        var portal = new GameObject("Portal").AddComponent<SpriteRenderer>();
        portal.transform.SetParent(transform, false); portal.transform.localPosition = new Vector3(0, 0.05f, 0);
        portal.sprite = Resources.Load<Sprite>("Necro/Necro_Portal"); portal.sortingOrder = -2;
        var burst = new GameObject("Burst").AddComponent<SpriteRenderer>();
        burst.transform.SetParent(transform, false);
        burst.sprite = Resources.Load<Sprite>("Necro/Necro_SummonFx"); burst.sortingOrder = 21;

        float total = 1.1f;
        for (float t = 0; t < total; t += Time.deltaTime)
        {
            float k = t / total;
            float open = Mathf.Clamp01(t / 0.25f);
            portal.transform.localScale = new Vector3(1.4f * open, 0.45f * open, 1);                 // 납작한 마법진
            portal.transform.localRotation = Quaternion.Euler(0, 0, -t * 120f * 0.1f);
            portal.color = new Color(1, 1, 1, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
            float rise = Mathf.Clamp01((t - 0.2f) / 0.35f);                                           // 해골 기운이 솟음
            burst.transform.localScale = new Vector3(0.75f, 0.75f * rise, 1);
            burst.color = new Color(1, 1, 1, k < 0.55f ? rise : Mathf.Max(0, 1f - (k - 0.55f) / 0.45f));
            yield return null;
        }
        Destroy(gameObject);
    }
}
