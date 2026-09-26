// 세리아 조작 스크립트
// ─────────────────────────────────────────────
//  ← / →  : 이동 (같은 방향 두 번 빠르게 → 대쉬, 키를 누르고 있는 동안 계속)
//  Alt    : 점프 (짧게 누르면 낮게, 길게 누르면 높게), 공중에서 한 번 더 → 2단 점프
//  Ctrl   : 기본 공격 (공격 중에 한 번 더 누르면 강공격으로 이어짐)
//  Space  : 강공격
//  Shift  : 스킬 (쿨타임 3초)
//  A / S / D : 검기(5레벨) / 번개(10레벨) / 메테오(20레벨)  → SeriaSkills
//  ↓      : 숙이기 (누르고 있는 동안 엎드림 → 날아오는 돌이 머리 위로 지나감)
//  X      : 방어 (앞에서 오는 공격을 막음, 몸통 박치기를 막으면 패링 → 몬스터 2초 스턴)
//  H : 피격 테스트(-10)   K : 사망 테스트 (쓰러지면 GAME OVER → 시작 화면)
//  ※ 모든 키는 Inspector 창에서 바꿀 수 있습니다.
//  ※ 모바일에서는 화면 터치 버튼(TouchControls)으로 같은 동작을 합니다.
// ─────────────────────────────────────────────
using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class SeriaController : MonoBehaviour
{
    [Header("키 설정")]
    public KeyCode leftKey = KeyCode.LeftArrow;
    public KeyCode rightKey = KeyCode.RightArrow;
    public KeyCode jumpKey = KeyCode.LeftAlt;
    public KeyCode jumpKey2 = KeyCode.RightAlt;
    public KeyCode attackKey = KeyCode.LeftControl;
    public KeyCode attackKey2 = KeyCode.RightControl;
    public KeyCode heavyKey = KeyCode.Space;
    public KeyCode skillKey = KeyCode.LeftShift;
    public KeyCode hitTestKey = KeyCode.H;
    public KeyCode dieTestKey = KeyCode.K;
    public KeyCode crouchKey = KeyCode.DownArrow;
    public KeyCode guardKey = KeyCode.X;
    public KeyCode reviveTestKey = KeyCode.R;

    [Header("이동")]
    public float moveSpeed = 4f;          // 초당 이동 거리
    public float acceleration = 30f;      // 출발할 때 가속 (클수록 즉시 최고속도)
    public float deceleration = 45f;      // 멈출 때 감속
    public float turnAcceleration = 60f;  // 반대 방향으로 꺾을 때
    public float airControl = 0.7f;       // 공중에서 방향 조절 정도 (0~1)

    [Header("부드러움 연출")]
    public bool squashStretch = true;     // 점프 시 살짝 늘어나고 착지 시 살짝 눌림
    public float squashAmount = 0.12f;

    [Header("점프")]
    public float jumpForce = 11f;
    public float jumpCutMultiplier = 0.5f; // 점프 키를 일찍 떼면 상승 속도에 곱해짐
    public float coyoteTime = 0.1f;        // 발판에서 떨어진 직후에도 점프 허용하는 시간
    public float jumpBufferTime = 0.1f;    // 착지 직전에 누른 점프를 기억하는 시간
    public int maxAirJumps = 1;            // 공중 추가 점프 횟수 (1 = 2단 점프)
    public float airJumpForce = 10f;       // 2단 점프 힘
    public LayerMask groundLayer = ~0;

    [Header("공격력")]
    public float attackDamage = 5f;      // 기본 공격 (Ctrl)
    public float heavyDamage = 5f;       // 강공격 (Space) — 따로 정하지 않아 기본 공격과 동일
    public float skillDamage = 10f;      // 스킬 (Shift)

    // 공격 판정 범위 (캐릭터 발밑 기준, 바라보는 방향 앞쪽)
    [System.Serializable] public struct HitBox { public float delay; public Vector2 offset; public Vector2 size; }
    public HitBox attackBox = new HitBox { delay = 0.15f, offset = new Vector2(0.8f, 0.6f), size = new Vector2(1.4f, 1.2f) };
    public HitBox heavyBox  = new HitBox { delay = 0.28f, offset = new Vector2(0.9f, 0.7f), size = new Vector2(1.8f, 1.4f) };
    public HitBox skillBox  = new HitBox { delay = 0.5f,  offset = new Vector2(1.1f, 0.8f), size = new Vector2(2.8f, 2.0f) };

    [Header("스킬")]
    public float skillCooldown = 3f;
    public float SkillCooldownRemaining => Mathf.Max(0f, skillReadyTime - Time.time);
    float skillReadyTime;

    [Header("방어 · 숙이기")]
    public float parryStunTime = 2f;       // 패링 성공 시 몬스터 스턴 시간
    public float crouchHeight = 0.55f;     // 숙였을 때 몸 높이 (원래 약 1.3)

    [Header("대쉬 (방향키 두 번 연타)")]
    public float doubleTapTime = 0.25f;   // 두 번 누르는 간격 허용 시간
    public float dashSpeed = 13f;
    public float dashDuration = 0.18f;     // 최소 대쉬 시간 (이후엔 키를 놓을 때까지 계속)
    public float sustainDashSpeedRatio = 0.5f;   // 키를 누르고 있는 동안의 대쉬 속도 비율
    public float dashCooldown = 0.4f;
    public bool allowAirDash = true;      // 공중 대쉬 허용 (점프 1회당 1번)
    public Color afterImageColor = new Color(1f, 0.55f, 0.65f, 0.6f);

    [Header("이펙트 (비워두면 표시 안 함)")]
    public Sprite slashFx;
    public Sprite skillFx;

    Rigidbody2D rb; Animator anim; SpriteRenderer sr; Collider2D col;
    PlayerHealth health;
    Vector3 baseScale; float squashT = 1f, squashSign; bool wasGrounded = true;
    bool dead;
    float coyoteCounter, jumpBufferCounter;
    int airJumpsUsed;
    // 대쉬 상태
    float lastTapTime = -1f; int lastTapDir;
    bool dashing, airDashUsed; float dashEndTime, nextDashTime, nextGhostTime; int dashDir;

#if UNITY_6000_0_OR_NEWER
    Vector2 Vel { get => rb.linearVelocity; set => rb.linearVelocity = value; }
#else
    Vector2 Vel { get => rb.velocity; set => rb.velocity = value; }
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        // 예전 설정(X = 사망 테스트)이 남아 있으면 방어 키와 겹치지 않게 변경
        if (dieTestKey == guardKey) dieTestKey = KeyCode.K;
        capsule = col as CapsuleCollider2D;
        if (capsule != null) { standSize = capsule.size; standOffset = capsule.offset; }

        // 물리(50회/초)와 화면(60~144회/초) 사이를 보간해 떨림 제거
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        baseScale = transform.localScale;

        health = GetComponent<PlayerHealth>();
        if (health == null) health = gameObject.AddComponent<PlayerHealth>();
        health.OnDamaged += () => { if (dashing) EndDash(); EndPose(); anim.SetTrigger("Hit"); };
        health.OnDied += Die;
        health.OnRevived += Revive;

        // 레벨 / 레벨 스킬 (없으면 자동 추가)
        if (GetComponent<SeriaSkills>() == null) gameObject.AddComponent<SeriaSkills>();
    }

    void Die()
    {
        EndPose();
        if (dashing) EndDash();
        dead = true; anim.speed = 1f; Vel = Vector2.zero;
        GameOverScreen.Show();                              // GAME OVER → 시작 화면
        anim.SetTrigger("Die");
    }

    void Revive()
    {
        dead = false; anim.speed = 1f;
        anim.ResetTrigger("Die"); anim.ResetTrigger("Hit");
        anim.Play("Idle", 0, 0f);
    }

    // 발밑에 바닥이 있는지 확인
    bool IsGrounded()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        foreach (var h in Physics2D.RaycastAll(origin, Vector2.down, 0.2f, groundLayer))
            if (h.collider != col && !h.collider.isTrigger) return true;   // 몬스터 판정 영역은 바닥 아님
        return false;
    }

    // 공격·스킬·피격 동작 중인지 (캔슬 판단용)
    bool Busy()
    {
        var s = anim.GetCurrentAnimatorStateInfo(0);
        return s.IsName("Attack1") || s.IsName("Attack2") || s.IsName("Skill") || s.IsName("Hit");
    }

    // ── 입력 처리: 새 Input System / 예전 Input Manager 둘 다 지원 ──
#if ENABLE_INPUT_SYSTEM
    static Key ToKey(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.LeftControl: return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.Return: return Key.Enter;
        }
        string n = k.ToString();
        if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
        return Enum.TryParse(n, out Key key) ? key : Key.None;
    }
    static UnityEngine.InputSystem.Controls.KeyControl K(KeyCode k)
    {
        var kb = Keyboard.current; var key = ToKey(k);
        return (kb == null || key == Key.None) ? null : kb[key];
    }
    static bool Down(KeyCode k) { var c = K(k); return c != null && c.wasPressedThisFrame; }
    static bool Hold(KeyCode k) { var c = K(k); return c != null && c.isPressed; }
    static bool Up(KeyCode k)   { var c = K(k); return c != null && c.wasReleasedThisFrame; }
#else
    static bool Down(KeyCode k) => Input.GetKeyDown(k);
    static bool Hold(KeyCode k) => Input.GetKey(k);
    static bool Up(KeyCode k)   => Input.GetKeyUp(k);
#endif
    // 키보드 + 화면 터치 버튼(VirtualInput) 둘 다 확인
    static bool AnyDown(KeyCode k) => k != KeyCode.None && (Down(k) | VirtualInput.Down(k));
    public static bool KeyDown(KeyCode k) => AnyDown(k);   // 다른 스크립트(스킬)에서 사용
    static bool AnyHold(KeyCode k) => k != KeyCode.None && (Hold(k) || VirtualInput.Held(k));
    static bool AnyUp(KeyCode k)   => k != KeyCode.None && (Up(k) | VirtualInput.Up(k));
    bool Pressed(KeyCode a, KeyCode b = KeyCode.None) => AnyDown(a) | AnyDown(b);
    bool Held(KeyCode a, KeyCode b = KeyCode.None) => AnyHold(a) || AnyHold(b);
    bool Released(KeyCode a, KeyCode b = KeyCode.None) => AnyUp(a) | AnyUp(b);

    void Start()
    {
        // 흔한 설정 문제를 Console 에 알려줌
        if (rb.bodyType != RigidbodyType2D.Dynamic)
            Debug.LogWarning("[Seria] Rigidbody 2D 의 Body Type 이 Dynamic 이 아닙니다. Dynamic 으로 바꾸세요.");
        if (anim.runtimeAnimatorController == null)
            Debug.LogWarning("[Seria] Animator 에 Controller 가 없습니다. Tools > Seria > 애니메이션 만들기 를 다시 실행하세요.");
        if (rb.constraints.HasFlag(RigidbodyConstraints2D.FreezePositionX))
            Debug.LogWarning("[Seria] Rigidbody 2D 의 Freeze Position X 가 켜져 있습니다. 꺼주세요.");
    }

    [Header("디버그")]
    public bool showDebug = true;   // 화면 왼쪽 위에 상태 표시
    float dbgX; bool dbgGround, dbgBusy;
    void OnGUI()
    {
        if (!showDebug) return;
        GUI.Label(new Rect(10, 10, 600, 120),
            $"Input X: {dbgX}   Grounded: {dbgGround}   Busy: {dbgBusy}\n" +
            $"Velocity: {Vel}   State: {(anim.runtimeAnimatorController ? CurrentState() : "NO CONTROLLER")}");
    }
    string CurrentState()
    {
        foreach (var n in new[] { "Idle", "Walk", "Jump", "Fall", "Attack1", "Attack2", "Skill", "Hit", "Die" })
            if (anim.GetCurrentAnimatorStateInfo(0).IsName(n)) return n;
        return "?";
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;                 // 설정 메뉴 등으로 일시정지 중
        if (dead) return;
        if (DialogueUI.IsOpen)                             // 대화 중엔 멈춤
        {
            if (dashing) EndDash();
            Vel = new Vector2(0f, Vel.y); anim.SetFloat("Speed", 0f); anim.SetBool("Grounded", IsGrounded());
            return;
        }

        CheckAttackStart();

        bool grounded = IsGrounded();
        bool busy = Busy();

        if (grounded) { airDashUsed = false; airJumpsUsed = 0; }   // 땅에 닿으면 2단 점프·공중 대쉬 다시 가능 (대쉬 중 착지 포함)

        // ── 대쉬 중 ──
        if (dashing)
        {
            // 방향키를 누르고 있는 동안 계속 대쉬 (최소 dashDuration 은 유지)
            bool dashHeld = dashDir < 0 ? Held(leftKey) : Held(rightKey);
            // 대쉬 중 점프: 대쉬 속도를 유지한 채 뛰어오름 (공중이면 2단 점프로 사용)
            if (Pressed(jumpKey, jumpKey2) && (grounded || airJumpsUsed < maxAirJumps))
            {
                if (!grounded) airJumpsUsed++;
                dashEndTime = Mathf.Min(dashEndTime, Time.time);   // 이제부터 중력 적용
                Vel = new Vector2(Vel.x, grounded ? jumpForce : airJumpForce);
                Squash(-1f);
            }
            float vy = Time.time < dashEndTime ? 0f : Vel.y;   // 처음 순간만 중력 무시, 이후 공중이면 떨어짐
            float spd = Time.time < dashEndTime ? dashSpeed : dashSpeed * sustainDashSpeedRatio;   // 지속 대쉬는 절반 속도
            Vel = new Vector2(dashDir * spd, vy);
            if (Time.time >= nextGhostTime) { SpawnAfterImage(); nextGhostTime = Time.time + 0.05f; }
            anim.SetFloat("Speed", 1f); anim.SetFloat("VelY", vy); anim.SetBool("Grounded", grounded);
            if (Time.time >= dashEndTime && !dashHeld) EndDash();
            return;
        }

        // ── 방어 / 숙이기 ──
        if (UpdatePose(grounded, busy)) return;

        // ── 방향키 두 번 연타 → 대쉬 ──
        int tap = Pressed(leftKey) ? -1 : Pressed(rightKey) ? 1 : 0;
        if (tap != 0)
        {
            bool doubleTap = tap == lastTapDir && Time.time - lastTapTime <= doubleTapTime;
            lastTapDir = tap; lastTapTime = Time.time;
            if (doubleTap && CanDash(grounded, busy)) { StartDash(tap, grounded); return; }
        }

        // ── 좌우 입력 ──
        float x = 0;
        if (Held(leftKey)) x -= 1;
        if (Held(rightKey)) x += 1;
        if (x != 0) sr.flipX = x < 0;                // 원본 그림은 오른쪽을 봄
        // 공격 중 새로 방향키를 누르면 공격 동작을 끊고 바로 이동 (누른 채로 공격하면 공격 유지)
        if (busy && (Pressed(leftKey) || Pressed(rightKey))) { CancelAttack(); anim.Play(grounded ? "Walk" : "Jump", 0, 0.5f); busy = false; }

        // ── 점프 타이밍 계산 ──
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - Time.deltaTime;
        bool jumpPressed = Pressed(jumpKey, jumpKey2);
        jumpBufferCounter = jumpPressed ? jumpBufferTime : jumpBufferCounter - Time.deltaTime;
        if (grounded) airJumpsUsed = 0;

        Vector2 v = Vel;

        // ── 이동 (출발·정지·방향전환 가속도를 따로 적용) ──
        float control = grounded ? 1f : airControl;
        float target = x * moveSpeed;
        float rate = x == 0 ? deceleration : (Mathf.Sign(target) != Mathf.Sign(v.x) && Mathf.Abs(v.x) > 0.1f ? turnAcceleration : acceleration);
        v.x = Mathf.MoveTowards(v.x, target, rate * control * Time.deltaTime);

        // ── 점프 ──
        if (jumpBufferCounter > 0 && coyoteCounter > 0)
        {
            if (busy) CancelAttack();                     // 공격 중에도 바로 점프
            anim.Play("Jump", 0, 0f);
            v.y = jumpForce;
            Squash(-1f);                                  // 점프: 위로 늘어남
            jumpBufferCounter = 0;
            coyoteCounter = 0;
        }
        // 2단 점프: 공중에서 점프 키를 한 번 더
        else if (jumpPressed && !grounded && coyoteCounter <= 0 && airJumpsUsed < maxAirJumps)
        {
            if (busy) CancelAttack();
            v.y = airJumpForce;
            airJumpsUsed++;
            jumpBufferCounter = 0;
            anim.Play("Jump", 0, 0f);                     // 점프 동작 처음부터 다시
            Squash(-1f);
            SpawnAfterImage();                            // 발밑에 잔상 한 번
        }
        // 점프 키를 일찍 떼면 낮게 점프
        if (Released(jumpKey, jumpKey2) && v.y > 0)
            v.y *= jumpCutMultiplier;

        Vel = v;

        // ── 공격 ──
        // 어떤 동작 중이든 즉시 새 공격으로 전환 (기본 공격을 이어 누르면 콤보 → 강공격)
        if (Pressed(attackKey, attackKey2))
        {
            var cur = anim.GetCurrentAnimatorStateInfo(0);
            bool combo = cur.IsName("Attack1") && cur.normalizedTime > 0.35f;
            if (combo) StartAttack("Attack2", heavyBox, heavyDamage); else StartAttack("Attack1", attackBox, attackDamage);
        }
        if (Pressed(heavyKey)) { StartAttack("Attack2", heavyBox, heavyDamage); StartCoroutine(Fx(slashFx, 0.25f, new Vector2(0.6f, 0.7f), 0.25f)); }
        if (Pressed(skillKey) && SkillCooldownRemaining <= 0f)   // 쿨타임이 다 차야 사용 가능
        {
            skillReadyTime = Time.time + skillCooldown;
            StartAttack("Skill", skillBox, skillDamage);
            StartCoroutine(Fx(skillFx, 0.5f, new Vector2(1.0f, 0.7f), 0.4f));
        }

        // ── 테스트용 ──
        if (Pressed(hitTestKey)) health.TakeDamage(10f);
        if (Pressed(dieTestKey)) health.TakeDamage(health.maxHP);

        dbgX = x; dbgGround = grounded; dbgBusy = busy;

        // ── 착지 감지 → 살짝 눌림 ──
        if (grounded && !wasGrounded) Squash(1f);
        wasGrounded = grounded;

        // ── 애니메이터에 상태 전달 ──
        anim.SetFloat("Speed", Mathf.Abs(x));
        anim.SetFloat("VelY", Vel.y);
        anim.SetBool("Grounded", grounded);

        // 걷기 동작 재생 속도를 실제 이동 속도에 맞춤 (출발·정지 때 발 미끄러짐 방지)
        if (anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
            anim.speed = Mathf.Clamp(Mathf.Abs(Vel.x) / moveSpeed, 0.5f, 1.2f);
        else if (anim.speed != 1f) anim.speed = 1f;
    }

    // ── 스쿼시 & 스트레치 (sign: 1 = 눌림, -1 = 늘어남) ──
    void Squash(float sign) { if (squashStretch) { squashT = 0f; squashSign = sign; } }

    void LateUpdate()
    {
        if (!squashStretch || squashT >= 1f) return;
        squashT = Mathf.Min(1f, squashT + Time.deltaTime / 0.18f);
        float k = Mathf.Sin(squashT * Mathf.PI) * squashAmount * squashSign;   // 0 → 최대 → 0
        transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), baseScale.z);
        if (squashT >= 1f) transform.localScale = baseScale;
    }

    // ══════════════ 방어 · 숙이기 ══════════════
    // 애니메이터를 잠시 끄고 Resources/SeriaMotion 의 프레임을 직접 재생
    enum Pose { None, Guard, Crouch }
    enum Phase { Enter, Hold, Exit }
    Pose pose = Pose.None; Phase phase; float frameT; int frameI;
    Sprite[] guardFrames, proneFrames, shieldFx;
    CapsuleCollider2D capsule; Vector2 standSize, standOffset;
    float lastBlockTime = -9f;
    SpriteRenderer shieldSr;

    // 프레임 구간: 방어 0-2 준비 / 3-5 유지 / 6-7 해제, 엎드리기 0-4 진입 / 5-9 유지 / 10-12 일어나기
    static readonly int[] GuardEnter = { 0, 1, 2 }, GuardHold = { 3, 4, 5, 4 }, GuardExit = { 6, 7 };
    static readonly int[] ProneEnter = { 0, 1, 2, 3, 4 }, ProneHold = { 5, 6, 7, 8, 9, 8, 7, 6 }, ProneExit = { 10, 11, 12 };

    static Sprite[] LoadSheet(string path)
    {
        var a = Resources.LoadAll<Sprite>(path);
        System.Array.Sort(a, (x, y) => Num(x.name).CompareTo(Num(y.name)));
        return a;
    }
    static int Num(string n) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out var v) ? v : 0; }

    bool UpdatePose(bool grounded, bool busy)
    {
        if (guardFrames == null)
        {
            guardFrames = LoadSheet("SeriaMotion/Seria_Guard");
            proneFrames = LoadSheet("SeriaMotion/Seria_Prone");
            shieldFx = LoadSheet("SeriaFX/FX_Shield");
        }
        bool guardHeld = Held(guardKey), crouchHeld = Held(crouchKey);
        if (pose != Pose.None && (Pressed(attackKey, attackKey2) || Pressed(heavyKey) || Pressed(skillKey) || Pressed(jumpKey, jumpKey2)))
        { EndPose(); return false; }                                  // 자세를 끊고 이번 프레임에 바로 행동

        if (pose == Pose.None)
        {
            if (!grounded) return false;
            if (busy && (guardHeld || crouchHeld)) CancelAttack();          // 공격 중에도 바로 방어·숙이기
            if (guardHeld && guardFrames.Length >= 8) StartPose(Pose.Guard);
            else if (crouchHeld && proneFrames.Length >= 13) StartPose(Pose.Crouch);
            else return false;
        }

        // 자세 중엔 제자리 (방향만 바꿀 수 있음)
        Vel = new Vector2(Mathf.MoveTowards(Vel.x, 0, deceleration * Time.deltaTime), Vel.y);
        if (pose == Pose.Guard && phase != Phase.Exit)
        {
            if (Pressed(leftKey)) sr.flipX = true;
            if (Pressed(rightKey)) sr.flipX = false;
        }

        bool held = pose == Pose.Guard ? guardHeld : crouchHeld;
        if (!held && phase != Phase.Exit) { phase = Phase.Exit; frameI = 0; frameT = 0; if (pose == Pose.Crouch) SetCrouchCollider(false); }

        var frames = pose == Pose.Guard ? guardFrames : proneFrames;
        int[] seq = pose == Pose.Guard
            ? (phase == Phase.Enter ? GuardEnter : phase == Phase.Hold ? GuardHold : GuardExit)
            : (phase == Phase.Enter ? ProneEnter : phase == Phase.Hold ? ProneHold : ProneExit);
        float fdur = phase == Phase.Hold ? (pose == Pose.Guard ? 0.1f : 0.16f) : (pose == Pose.Guard ? 0.05f : 0.05f);

        frameT += Time.deltaTime;
        while (frameT >= fdur)
        {
            frameT -= fdur; frameI++;
            if (frameI >= seq.Length)
            {
                if (phase == Phase.Enter) { phase = Phase.Hold; frameI = 0; seq = pose == Pose.Guard ? GuardHold : ProneHold; }
                else if (phase == Phase.Hold) frameI = 0;
                else { EndPose(); return true; }
            }
        }
        sr.sprite = frames[seq[Mathf.Min(frameI, seq.Length - 1)]];
        anim.SetFloat("Speed", 0f);
        return true;
    }

    void StartPose(Pose p)
    {
        pose = p; phase = Phase.Enter; frameI = 0; frameT = 0;
        anim.enabled = false;
        if (p == Pose.Crouch) SetCrouchCollider(true);
    }

    void EndPose()
    {
        if (pose == Pose.None) return;
        if (pose == Pose.Crouch) SetCrouchCollider(false);
        pose = Pose.None;
        anim.enabled = true;
        anim.Play("Idle", 0, 0f);
    }

    void SetCrouchCollider(bool crouch)
    {
        if (capsule == null) return;
        if (crouch)
        {
            capsule.size = new Vector2(standSize.x, crouchHeight);
            capsule.offset = new Vector2(standOffset.x, standOffset.y - (standSize.y - crouchHeight) / 2f);
        }
        else { capsule.size = standSize; capsule.offset = standOffset; }
    }

    public bool IsGuarding => pose == Pose.Guard && phase != Phase.Exit;
    public bool IsCrouching => pose == Pose.Crouch;

    // 몬스터·투사체가 공격 직전에 호출: 막으면 true (bodyAttack = 몸통 박치기 → 패링)
    public bool TryBlock(Vector3 from, bool bodyAttack)
    {
        if (!IsGuarding) return false;
        if (phase == Phase.Enter && frameI == 0) return false;          // 방어 들어가는 첫 순간은 아직 못 막음
        float face = sr.flipX ? -1f : 1f, dx = from.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.2f && Mathf.Sign(dx) != face) return false;   // 등 뒤 공격은 못 막음
        if (Time.time < lastBlockTime + 0.25f) return true;              // 같은 공격이 연달아 닿는 경우
        lastBlockTime = Time.time;

        StartCoroutine(ShieldFlash(bodyAttack));
        Vel = new Vector2(-face * (bodyAttack ? 2.5f : 1.5f), Vel.y);   // 살짝 밀림
        if (bodyAttack)
        {
            DamagePopup.ShowText(transform.position + new Vector3(face * 0.4f, 1.9f, 0), "PARRY!", new Color(0.55f, 0.85f, 1f));
            StartCoroutine(HitStop(0.08f));
            CameraShake.Shake(0.12f, 0.12f);
        }
        else DamagePopup.ShowText(transform.position + new Vector3(face * 0.4f, 1.7f, 0), "BLOCK", new Color(0.8f, 0.9f, 1f));
        return true;
    }

    IEnumerator ShieldFlash(bool parry)
    {
        if (shieldFx == null || shieldFx.Length < 6) yield break;
        if (shieldSr == null)
        {
            var g = new GameObject("Shield FX"); g.transform.SetParent(transform, false);
            shieldSr = g.AddComponent<SpriteRenderer>(); shieldSr.sortingOrder = sr.sortingOrder + 2;
        }
        float face = sr.flipX ? -1f : 1f;
        shieldSr.transform.localPosition = new Vector3(face * 0.55f, 0.7f, 0);
        shieldSr.flipX = face < 0;
        int[] seq = parry ? new[] { 3, 4, 5, 5 } : new[] { 2, 3 };
        float scale = parry ? 1.3f : 1f;
        shieldSr.transform.localScale = Vector3.one * scale;
        foreach (int i in seq) { shieldSr.sprite = shieldFx[i]; shieldSr.color = Color.white; yield return new WaitForSecondsRealtime(0.06f); }
        for (float t = 1f; t > 0; t -= Time.unscaledDeltaTime / 0.15f) { shieldSr.color = new Color(1, 1, 1, t); yield return null; }
        shieldSr.sprite = null;
    }

    IEnumerator HitStop(float seconds)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = 1f;
    }

    // ── 다른 스크립트(SeriaSkills)용 ──
    public bool CanAct => !dead && pose == Pose.None;
    float suppressHitUntil;
    // 스킬 시전 모션만 재생 (근접 공격 판정은 하지 않음). 진행 중인 동작은 즉시 캔슬
    public void PlayCastAnimation(string trigger)
    {
        if (dashing) EndDash();
        CancelAttack();
        string state = trigger == "Heavy" ? "Attack2" : trigger == "Attack" ? "Attack1" : trigger;
        anim.speed = 1f;
        anim.Play(state, 0, 0f);
        lastStateHash = Animator.StringToHash("Base Layer." + state);   // 근접 판정 없이 모션만
    }

    // ── 즉시 공격 (진행 중인 동작을 끊고 처음부터) ──
    Coroutine hitCo;
    void StartAttack(string state, HitBox box, float damage)
    {
        if (dashing) EndDash();
        CancelAttack();
        anim.speed = 1f;
        anim.Play(state, 0, 0f);
        lastStateHash = Animator.StringToHash("Base Layer." + state);   // 자동 감지와 중복 판정 방지
        hitCo = StartCoroutine(DoHit(box, damage));
    }

    // 공격 캔슬: 아직 판정이 나가기 전이면 판정도 취소
    void CancelAttack()
    {
        if (hitCo != null) { StopCoroutine(hitCo); hitCo = null; }
    }

    // ── 공격 판정 ──
    // 공격 애니메이션이 "시작되는 순간"을 감지해서, 정해진 시간 뒤 앞쪽 범위에 있는 적에게 데미지
    int lastStateHash;
    void CheckAttackStart()
    {
        if (anim.runtimeAnimatorController == null) return;
        var info = anim.GetCurrentAnimatorStateInfo(0);
        if (info.fullPathHash == lastStateHash) return;
        lastStateHash = info.fullPathHash;
        bool isAttack = info.IsName("Attack1") || info.IsName("Attack2") || info.IsName("Skill");
        if (isAttack && Time.time < suppressHitUntil) { suppressHitUntil = 0f; return; }   // 스킬 시전 모션
        if (info.IsName("Attack1")) hitCo = StartCoroutine(DoHit(attackBox, attackDamage));
        else if (info.IsName("Attack2")) hitCo = StartCoroutine(DoHit(heavyBox, heavyDamage));
        else if (info.IsName("Skill")) hitCo = StartCoroutine(DoHit(skillBox, skillDamage));
    }

    IEnumerator DoHit(HitBox box, float damage)
    {
        float dir = sr.flipX ? -1f : 1f;                 // 휘두르기 시작한 방향 기준
        yield return new WaitForSeconds(box.delay);
        if (dead) yield break;
        Vector2 center = (Vector2)transform.position + new Vector2(box.offset.x * dir, box.offset.y);
        var done = new System.Collections.Generic.HashSet<EnemyHealth>();
        foreach (var c in Physics2D.OverlapBoxAll(center, box.size, 0f))
        {
            var e = c.GetComponentInParent<EnemyHealth>();
            if (e != null && !e.IsDead && done.Add(e)) e.TakeDamage(damage, transform.position.x);   // 한 번 휘두를 때 적 1마리당 1회
        }
    }

    // Scene 창에서 캐릭터를 선택하면 공격 범위가 보임
    void OnDrawGizmosSelected()
    {
        float dir = (sr != null && sr.flipX) ? -1f : 1f;
        void Box(HitBox b, Color c) { Gizmos.color = c; Gizmos.DrawWireCube(transform.position + new Vector3(b.offset.x * dir, b.offset.y), b.size); }
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        Box(attackBox, Color.yellow); Box(heavyBox, new Color(1f, 0.5f, 0f)); Box(skillBox, Color.red);
    }

    // ── 대쉬 ──
    bool CanDash(bool grounded, bool busy)
    {
        if (Time.time < nextDashTime) return false;          // 공격 중에도 대쉬로 캔슬 가능
        if (!grounded && (!allowAirDash || airDashUsed)) return false;
        return true;
    }

    void StartDash(int dir, bool grounded)
    {
        CancelAttack(); EndPose();
        dashing = true; dashDir = dir;
        dashEndTime = Time.time + dashDuration;
        nextGhostTime = 0f;
        if (!grounded) airDashUsed = true;
        sr.flipX = dir < 0;
        lastTapTime = -1f;                 // 세 번 연타로 연속 대쉬 방지
        anim.Play("Walk", 0, 0f);          // 걷기 동작을 빠르게 재생
        anim.speed = 2.5f;
    }

    void EndDash()
    {
        dashing = false;
        nextDashTime = Time.time + dashCooldown;
        anim.speed = 1f;
        Vel = new Vector2(dashDir * moveSpeed, Vel.y);   // 부드럽게 감속 (점프 중이면 높이 유지)
    }

    // 잔상: 현재 모습을 복사해 색을 입히고 서서히 사라지게
    void SpawnAfterImage()
    {
        var g = new GameObject("AfterImage");
        g.transform.SetPositionAndRotation(transform.position, transform.rotation);
        g.transform.localScale = transform.localScale;
        var r = g.AddComponent<SpriteRenderer>();
        r.sprite = sr.sprite; r.flipX = sr.flipX;
        r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder - 1;
        r.color = afterImageColor;
        StartCoroutine(FadeOut(r, 0.25f));
    }

    IEnumerator FadeOut(SpriteRenderer r, float life)
    {
        Color c0 = r.color;
        for (float t = 0; t < life; t += Time.deltaTime)
        {
            r.color = new Color(c0.r, c0.g, c0.b, c0.a * (1 - t / life));
            yield return null;
        }
        Destroy(r.gameObject);
    }

    // 간단한 이펙트: delay 후 캐릭터 앞에 스프라이트를 띄우고 서서히 사라지게
    IEnumerator Fx(Sprite sprite, float delay, Vector2 offset, float life)
    {
        if (sprite == null) yield break;
        yield return new WaitForSeconds(delay);
        float dir = sr.flipX ? -1 : 1;
        var go = new GameObject("FX");
        go.transform.position = transform.position + new Vector3(offset.x * dir, offset.y, 0);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite; r.flipX = sr.flipX; r.sortingOrder = sr.sortingOrder + 1;
        for (float t = 0; t < life; t += Time.deltaTime)
        {
            r.color = new Color(1, 1, 1, 1 - t / life);
            yield return null;
        }
        Destroy(go);
    }
}
