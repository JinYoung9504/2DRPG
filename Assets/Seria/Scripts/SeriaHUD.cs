// HUD: 레벨 · 체력바(%) · 경험치바 · 스킬 슬롯(Shift / A / S / D, 쿨타임·잠금 표시)
// PC: 좌측 하단 / 모바일(터치 버튼 사용 시): 좌측 상단
using UnityEngine;
using UnityEngine.UI;

public class SeriaHUD : MonoBehaviour
{
    public PlayerHealth health;
    public SeriaController player;
    public Sprite skillIcon;
    public string skillKeyLabel = "Shift";

    [Header("색상")]
    public Color hpColor = new Color(0.85f, 0.15f, 0.25f);
    public Color hpLowColor = new Color(1f, 0.35f, 0.1f);
    public Color trailColor = new Color(1f, 0.85f, 0.6f);
    public Color expColor = new Color(0.35f, 0.75f, 1f);

    class Slot { public RectTransform rt; public Image icon, cd, lockDim; public Text cdText, lockText; public float flash; public bool wasReady = true; }

    Image hpFill, hpTrail, expFill;
    Text hpText, lvText, expText;
    Slot shiftSlot; Slot[] lvSlots;
    SeriaSkills skills; PlayerLevel level;
    float trail = 1f, trailDelay, lastRatio = 1f;
    bool mobile;

    void Start()
    {
        if (player == null) { var go = GameObject.Find("Seria"); if (go) player = go.GetComponent<SeriaController>(); }
        if (player != null)
        {
            if (health == null) health = player.GetComponent<PlayerHealth>();
            skills = player.GetComponent<SeriaSkills>();
            level = player.GetComponent<PlayerLevel>();
        }
        BuildUI();
    }

    // ── UI 도우미 ──
    static Font GetFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }
    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }
    static RectTransform Stretch(string name, Transform parent, float inset = 0)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }
    static Image Img(RectTransform rt, Color c) { var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return i; }
    static Text Txt(RectTransform rt, int size, TextAnchor align)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GetFont(); t.fontSize = size; t.alignment = align; t.color = Color.white; t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }
    static Sprite white;
    static Sprite WhiteSprite()
    {
        if (white == null) { var tex = Texture2D.whiteTexture; white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); }
        return white;
    }
    static Image Filled(RectTransform rt, Color c, Image.FillMethod m)
    {
        var i = Img(rt, c); i.sprite = WhiteSprite(); i.type = Image.Type.Filled; i.fillMethod = m;
        if (m == Image.FillMethod.Radial360) { i.fillOrigin = (int)Image.Origin360.Top; i.fillClockwise = false; }
        return i;
    }

    // ── UI 생성 ──
    void BuildUI()
    {
        var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

        mobile = TouchControls.Active;
        var root = Rect("HUD Panel", canvasGo.transform, mobile ? new Vector2(0, 1) : Vector2.zero,
                        mobile ? new Vector2(30, -30) : new Vector2(30, 30), new Vector2(820, 110));
        Img(root, new Color(0, 0, 0, 0.35f));

        // 레벨
        lvText = Txt(Rect("Level", root, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(100, 60)), 30, TextAnchor.MiddleLeft);

        // 체력바
        var bar = Rect("HP Bar", root, new Vector2(0, 0.5f), new Vector2(120, 14), new Vector2(330, 32), new Vector2(0, 0.5f));
        Img(bar, new Color(0.08f, 0.05f, 0.07f, 0.9f));
        var frame = bar.gameObject.AddComponent<Outline>(); frame.effectColor = new Color(0.85f, 0.7f, 0.45f); frame.effectDistance = new Vector2(2, -2);
        hpTrail = Filled(Stretch("Trail", bar, 3), trailColor, Image.FillMethod.Horizontal);
        hpFill = Filled(Stretch("Fill", bar, 3), hpColor, Image.FillMethod.Horizontal);
        hpText = Txt(Stretch("Percent", bar), 19, TextAnchor.MiddleCenter);

        // 경험치바
        var eb = Rect("EXP Bar", root, new Vector2(0, 0.5f), new Vector2(120, -22), new Vector2(330, 14), new Vector2(0, 0.5f));
        Img(eb, new Color(0.05f, 0.06f, 0.1f, 0.9f));
        expFill = Filled(Stretch("Fill", eb, 2), expColor, Image.FillMethod.Horizontal);
        expText = Txt(Rect("EXP Text", root, new Vector2(0, 0.5f), new Vector2(120, -42), new Vector2(330, 20), new Vector2(0, 0.5f)), 15, TextAnchor.MiddleLeft);

        // 스킬 슬롯: Shift, A, S, D, F
        float x = 480, step = 84;
        shiftSlot = MakeSlot(root, x, skillIcon, mobile ? "" : skillKeyLabel);
        if (skills != null)
        {
            var all = skills.All;
            string[] icons = { "Skills/Icon_SwordWave", "Skills/Icon_Lightning", "Skills/Icon_Meteor", "Skills/Icon_SwordRain" };
            lvSlots = new Slot[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                lvSlots[i] = MakeSlot(root, x + step * (i + 1), Resources.Load<Sprite>(icons[i]), mobile ? "" : all[i].key.ToString());
                lvSlots[i].lockText.text = "Lv" + all[i].unlockLevel;
            }
        }
    }

    Slot MakeSlot(RectTransform root, float x, Sprite icon, string key)
    {
        var s = new Slot();
        s.rt = Rect("Skill Slot", root, new Vector2(0, 0.5f), new Vector2(x + 36, 0), new Vector2(72, 72), new Vector2(0.5f, 0.5f));
        Img(s.rt, new Color(0, 0, 0, 0.6f));
        s.icon = Img(Stretch("Icon", s.rt, 2), Color.white); s.icon.sprite = icon; s.icon.preserveAspect = true;
        s.cd = Filled(Stretch("Cooldown", s.rt, 2), new Color(0, 0, 0, 0.7f), Image.FillMethod.Radial360); s.cd.fillAmount = 0;
        s.lockDim = Img(Stretch("Lock", s.rt, 2), new Color(0, 0, 0, 0.72f)); s.lockDim.enabled = false;
        s.lockText = Txt(Stretch("LockText", s.rt), 20, TextAnchor.MiddleCenter); s.lockText.color = new Color(1f, 0.8f, 0.5f); s.lockText.enabled = false;
        s.cdText = Txt(Stretch("CD Text", s.rt), 26, TextAnchor.MiddleCenter);
        var k = Rect("Key", s.rt, new Vector2(1, 0), new Vector2(-3, 2), new Vector2(66, 20), new Vector2(1, 0));
        Txt(k, 15, TextAnchor.LowerRight).text = key;
        return s;
    }

    void UpdateSlot(Slot s, float remain, float total, bool locked)
    {
        s.lockDim.enabled = locked; s.lockText.enabled = locked;
        s.cd.fillAmount = locked ? 0 : remain / Mathf.Max(0.01f, total);
        s.cdText.text = locked || remain <= 0 ? "" : remain < 1f ? remain.ToString("0.0") : Mathf.CeilToInt(remain).ToString();
        s.icon.color = remain > 0 || locked ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
        bool ready = !locked && remain <= 0;
        if (ready && !s.wasReady) s.flash = 0.25f;               // 쿨타임 끝나면 톡 튀는 효과
        s.wasReady = ready;
        if (s.flash > 0) s.flash -= Time.deltaTime;
        s.rt.localScale = Vector3.one * (1f + Mathf.Max(0f, s.flash) * 0.6f);
    }

    // ── 매 프레임 갱신 ──
    void Update()
    {
        if (hpFill == null) return;

        float ratio = health != null ? Mathf.Clamp01(health.CurrentHP / health.maxHP) : 1f;
        if (ratio < lastRatio) trailDelay = 0.4f;
        if (ratio > trail) trail = ratio;
        lastRatio = ratio;
        if (trailDelay > 0) trailDelay -= Time.deltaTime; else trail = Mathf.MoveTowards(trail, ratio, Time.deltaTime * 0.8f);
        hpFill.fillAmount = ratio; hpTrail.fillAmount = trail;
        hpFill.color = ratio <= 0.3f ? Color.Lerp(hpLowColor, hpColor, Mathf.PingPong(Time.time * 3f, 1f)) : hpColor;
        int cur = health != null ? Mathf.CeilToInt(health.CurrentHP) : 0, max = health != null ? Mathf.RoundToInt(health.maxHP) : 0;
        hpText.text = cur + "/" + max + "  " + Mathf.CeilToInt(ratio * 100f) + "%";

        if (level != null)
        {
            lvText.text = "Lv." + level.level;
            expFill.fillAmount = Mathf.Clamp01((float)level.exp / level.Required);
            expText.text = "EXP " + level.exp + " / " + level.Required;
        }
        else { lvText.text = ""; expText.text = ""; }

        if (player != null) UpdateSlot(shiftSlot, player.SkillCooldownRemaining, player.skillCooldown, false);
        if (skills != null && lvSlots != null)
        {
            var all = skills.All;
            for (int i = 0; i < all.Length; i++) UpdateSlot(lvSlots[i], all[i].Remaining, all[i].cooldown, !skills.Unlocked(all[i]));
        }
    }
}
