// 모바일 터치 조작 UI (좌측 중간: 이동 / 우측 중간: 공격·강공격·스킬·점프)
// 모바일 기기에서만 보입니다. PC에서 테스트하려면 Inspector 에서 Always Show 를 켜세요.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchControls : MonoBehaviour
{
    public SeriaController player;
    public bool alwaysShow = false;          // PC(에디터)에서도 버튼 표시

    [Header("버튼 그림")]
    public Sprite left, right, jump, attack, heavy, skill, down, guard;

    [Header("투명도 (0~1, 낮을수록 흐릿)")]
    [Range(0, 1)] public float idleAlpha = 0.45f;
    [Range(0, 1)] public float pressedAlpha = 0.7f;

    public static bool Active { get; private set; }

    Image skillCd;

    void Awake()
    {
        Active = Application.isMobilePlatform || alwaysShow;
        if (Application.isMobilePlatform) Application.targetFrameRate = 60;   // 안드로이드 기본 30fps → 60fps
    }

    void Start()
    {
        if (!Active) return;
        if (player == null) { var go = GameObject.Find("Seria"); if (go) player = go.GetComponent<SeriaController>(); }
        if (player == null) { Debug.LogError("[TouchControls] Seria 를 찾을 수 없습니다."); return; }
        EnsureEventSystem();
        Build();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    void Build()
    {
        var cgo = new GameObject("Touch Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;   // 화면 높이 기준

        var L = new Vector2(0, 0.5f); var R = new Vector2(1, 0.5f);
        // 좌측 중간: 이동
        Btn(cgo.transform, "Left", left, player.leftKey, L, new Vector2(170, -60), 190);
        Btn(cgo.transform, "Right", right, player.rightKey, L, new Vector2(390, -60), 190);
        // 우측 중간: 공격 / 강공격 / 스킬 / 점프
        Btn(cgo.transform, "Attack", attack, player.attackKey, R, new Vector2(-210, -60), 210);
        Btn(cgo.transform, "Jump", jump, player.jumpKey, R, new Vector2(-440, -140), 150);
        Btn(cgo.transform, "Heavy", heavy, player.heavyKey, R, new Vector2(-430, 90), 150);
        var sk = Btn(cgo.transform, "Skill", skill, player.skillKey, R, new Vector2(-210, 190), 150);

        // 숙이기 (이동 버튼 아래) / 방어 (공격 버튼 왼쪽)
        Btn(cgo.transform, "Down", Pick(down, "SeriaUI/Btn_Down"), player.crouchKey, L, new Vector2(280, -250), 150);
        Btn(cgo.transform, "Guard", Pick(guard, "SeriaUI/Btn_Guard"), player.guardKey, R, new Vector2(-640, -60), 150);

        // 레벨 스킬 (검기 A / 번개 S / 메테오 D) — 우측 위쪽 줄
        var lvSkills = player.GetComponent<SeriaSkills>();
        if (lvSkills != null)
        {
            var all = lvSkills.All;
            string[] icons = { "Skills/Icon_SwordWave", "Skills/Icon_Lightning", "Skills/Icon_Meteor" };
            for (int i = 0; i < all.Length; i++)
                Btn(cgo.transform, all[i].name, Resources.Load<Sprite>(icons[i]), all[i].key, R, new Vector2(-510 + 150 * i, 360), 120);
        }

        // 스킬 버튼 위 쿨타임 표시
        var cd = new GameObject("Cooldown", typeof(RectTransform)).GetComponent<RectTransform>();
        cd.SetParent(sk, false); cd.anchorMin = Vector2.zero; cd.anchorMax = Vector2.one;
        cd.offsetMin = cd.offsetMax = Vector2.zero;
        skillCd = cd.gameObject.AddComponent<Image>();
        skillCd.sprite = CircleSprite(); skillCd.color = new Color(0, 0, 0, 0.6f);
        skillCd.type = Image.Type.Filled; skillCd.fillMethod = Image.FillMethod.Radial360;
        skillCd.fillOrigin = (int)Image.Origin360.Top; skillCd.fillClockwise = false;
        skillCd.raycastTarget = false; skillCd.fillAmount = 0;
    }

    RectTransform Btn(Transform parent, string name, Sprite sprite, KeyCode key, Vector2 anchor, Vector2 pos, float size)
    {
        var go = new GameObject("Btn " + name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = sprite != null ? sprite : CircleSprite();
        img.alphaHitTestMinimumThreshold = 0f;
        var b = go.AddComponent<TouchButton>();
        b.key = key; b.idleAlpha = idleAlpha; b.pressedAlpha = pressedAlpha;
        return rt;
    }

    static Sprite Pick(Sprite s, string res) { if (s != null) return s; var r = Resources.Load<Sprite>(res); return r; }

    static Sprite circle;
    static Sprite CircleSprite()
    {
        if (circle != null) return circle;
        const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - 4f - d)));
        }
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return circle;
    }

    void Update()
    {
        if (skillCd == null || player == null) return;
        skillCd.fillAmount = player.SkillCooldownRemaining / Mathf.Max(0.01f, player.skillCooldown);
    }

    void OnApplicationPause(bool paused) { if (paused) VirtualInput.ReleaseAll(); }
}
