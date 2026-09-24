// 좌측 하단 HUD: 체력바(퍼센트 표시) + 스킬 아이콘(쿨타임 표시)
// 게임 시작 시 코드로 UI를 자동 생성합니다. (Canvas 따로 만들 필요 없음)
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

    Image hpFill, hpTrail, cdOverlay, iconImg;
    RectTransform slotRt;
    Text hpText, cdText;
    float trail = 1f, trailDelay;
    float lastRatio = 1f;
    float readyFlash;

    void Start()
    {
        if (player == null) { var go = GameObject.Find("Seria"); if (go) player = go.GetComponent<SeriaController>(); }
        if (health == null && player != null) health = player.GetComponent<PlayerHealth>();
        BuildUI();
    }

    // ── UI 생성 ──
    static Font GetFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent, float inset = 0)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    static Image Img(RectTransform rt, Color c) { var i = rt.gameObject.AddComponent<Image>(); i.color = c; return i; }

    Text Txt(RectTransform rt, int size, TextAnchor align)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GetFont(); t.fontSize = size; t.alignment = align; t.color = Color.white;
        t.fontStyle = FontStyle.Bold;
        var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

        // PC: 좌측 하단 / 모바일(터치 버튼 사용 시): 좌측 상단 (이동 버튼과 겹치지 않게)
        bool mobile = TouchControls.Active;
        var root = Rect("HUD Panel", canvasGo.transform, mobile ? new Vector2(0, 1) : Vector2.zero,
                        mobile ? new Vector2(30, -30) : new Vector2(30, 30), new Vector2(520, 96));
        Img(root, new Color(0, 0, 0, 0.35f));

        // 체력바
        var hpLabel = Rect("HP Label", root, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(50, 40));
        Txt(hpLabel, 26, TextAnchor.MiddleLeft).text = "HP";

        var bar = Rect("HP Bar", root, new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(330, 34));
        Img(bar, new Color(0.08f, 0.05f, 0.07f, 0.9f));
        var frame = bar.gameObject.AddComponent<Outline>(); frame.effectColor = new Color(0.85f, 0.7f, 0.45f); frame.effectDistance = new Vector2(2, -2);

        hpTrail = Img(Stretch("Trail", bar, 3), trailColor);
        hpTrail.type = Image.Type.Filled; hpTrail.fillMethod = Image.FillMethod.Horizontal; hpTrail.sprite = WhiteSprite();
        hpFill = Img(Stretch("Fill", bar, 3), hpColor);
        hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal; hpFill.sprite = WhiteSprite();
        hpText = Txt(Stretch("Percent", bar), 22, TextAnchor.MiddleCenter);

        // 스킬 아이콘
        var slot = Rect("Skill Slot", root, new Vector2(0, 0.5f), new Vector2(420, 0), new Vector2(76, 76));
        slotRt = slot; slot.pivot = new Vector2(0.5f, 0.5f); slot.anchoredPosition = new Vector2(420 + 38, 0);
        Img(slot, new Color(0, 0, 0, 0.6f));
        iconImg = Img(Stretch("Icon", slot, 2), Color.white);
        iconImg.sprite = skillIcon; iconImg.preserveAspect = true;
        cdOverlay = Img(Stretch("Cooldown", slot, 2), new Color(0, 0, 0, 0.7f));
        cdOverlay.sprite = WhiteSprite();
        cdOverlay.type = Image.Type.Filled; cdOverlay.fillMethod = Image.FillMethod.Radial360;
        cdOverlay.fillOrigin = (int)Image.Origin360.Top; cdOverlay.fillClockwise = false; cdOverlay.fillAmount = 0;
        cdText = Txt(Stretch("CD Text", slot), 28, TextAnchor.MiddleCenter);
        var key = Rect("Key", slot, new Vector2(1, 0), new Vector2(-3, 2), new Vector2(70, 22));
        var keyText = Txt(key, 15, TextAnchor.LowerRight); keyText.text = mobile ? "" : skillKeyLabel;
    }

    static Sprite white;
    static Sprite WhiteSprite()
    {
        if (white == null)
        {
            var tex = Texture2D.whiteTexture;
            white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return white;
    }

    // ── 매 프레임 갱신 ──
    void Update()
    {
        if (hpFill == null) return;

        // 체력
        float ratio = health != null ? Mathf.Clamp01(health.CurrentHP / health.maxHP) : 1f;
        if (ratio < lastRatio) trailDelay = 0.4f;      // 맞으면 흰 잔상바가 잠시 후 따라 줄어듦
        if (ratio > trail) trail = ratio;
        lastRatio = ratio;
        if (trailDelay > 0) trailDelay -= Time.deltaTime;
        else trail = Mathf.MoveTowards(trail, ratio, Time.deltaTime * 0.8f);

        hpFill.fillAmount = ratio;
        hpTrail.fillAmount = trail;
        hpFill.color = ratio <= 0.3f ? Color.Lerp(hpLowColor, hpColor, Mathf.PingPong(Time.time * 3f, 1f)) : hpColor;
        hpText.text = Mathf.CeilToInt(ratio * 100f) + "%";

        // 스킬 쿨타임
        if (player == null) return;
        float remain = player.SkillCooldownRemaining;
        float total = Mathf.Max(0.01f, player.skillCooldown);
        cdOverlay.fillAmount = remain / total;
        cdText.text = remain <= 0 ? "" : remain < 1f ? remain.ToString("0.0") : Mathf.CeilToInt(remain).ToString();
        if (remain > 0) readyFlash = 0.25f;
        else if (readyFlash > 0) readyFlash -= Time.deltaTime;
        iconImg.color = remain > 0 ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
        slotRt.localScale = Vector3.one * (1f + Mathf.Max(0f, readyFlash) * 0.6f);   // 쿨타임 끝나면 톡 튀는 효과
    }
}
