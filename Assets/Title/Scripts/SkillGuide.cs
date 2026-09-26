// 마을(시작 마을 · 정령의 숲 · 멸망한 왕국 성곽)에 들어가면 조작 · 스킬 설명 팝업
//  - 게임을 켠 뒤 마을마다 처음 들어갈 때 한 번 자동으로 열림 (게임 일시정지)
//  - 마을에서는 오른쪽 위 톱니바퀴 왼쪽의 [스킬] 버튼으로 언제든 다시 열기
//  - 닫기: [확인] 버튼 · Enter · Esc
//  - 스킬 수치(데미지 · 쿨타임 · 습득 레벨)는 세리아의 실제 설정값을 그대로 읽어서 표시
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SkillGuide : MonoBehaviour
{
    // 팝업이 나오는 마을 (씬 이름)
    public static readonly string[] Villages = { "Map0_Town", "Map4_SpiritForest", "Map7_RuinedCastle" };
    static readonly HashSet<string> shownThisRun = new HashSet<string>();
    public static bool IsOpen { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;

    GameObject popup; float prevScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (s, m) => Attach(s.name);
        Attach(SceneManager.GetActiveScene().name);
    }

    static void Attach(string scene)
    {
        if (System.Array.IndexOf(Villages, scene) < 0) return;
        var g = new GameObject("Skill Guide");
        var sg = g.AddComponent<SkillGuide>();
        sg.autoOpen = shownThisRun.Add(scene);
    }

    bool autoOpen;
    Font font;

    IEnumerator Start()
    {
        font = DialogueUI.KoreanFont();
        UIHelper.EnsureEventSystem();
        BuildOpenButton();
        if (!autoOpen) yield break;
        yield return new WaitForSecondsRealtime(1.0f);          // 맵 이동 연출이 끝난 뒤
        while (SceneTransition.Busy || DialogueUI.IsOpen) yield return null;
        Open();
    }

    void OnDestroy() { if (IsOpen) { Time.timeScale = prevScale; IsOpen = false; } }

    void Update()
    {
        if (!IsOpen) return;
        if (SeriaController.KeyDown(KeyCode.Return) || SeriaController.KeyDown(KeyCode.Escape)) Close();
    }

    // ───────── UI ─────────
    Canvas canvas;
    Canvas GetCanvas()
    {
        if (canvas != null) return canvas;
        var cgo = new GameObject("Skill Guide Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 160;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        return canvas;
    }

    RectTransform Rt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    Text Txt(RectTransform rt, int size, Color c, TextAnchor a, FontStyle st = FontStyle.Normal)
    {
        var t = rt.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = st;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        return t;
    }

    static readonly Color Panel = new Color(0.07f, 0.05f, 0.09f, 0.94f), Gold = new Color(1f, 0.82f, 0.52f), Soft = new Color(0.9f, 0.88f, 0.92f), Dim = new Color(0.6f, 0.58f, 0.65f);

    // 톱니바퀴 왼쪽의 [스킬] 버튼
    void BuildOpenButton()
    {
        var c = GetCanvas();
        var b = Rt("Btn Skill Guide", c.transform, new Vector2(1, 1), new Vector2(-24 - 64 - 14, -24), new Vector2(64, 64));
        b.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.1f, 0.8f);
        var t = Txt(Rt("Label", b, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64)), 22, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.text = "스킬";
        var fx = b.gameObject.AddComponent<UIButtonFx>(); fx.onClick = () => { if (!IsOpen && !SaveButton.IsOpen) Open(); };
    }

    void Open()
    {
        if (IsOpen) return;
        IsOpen = true; prevScale = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f;
        var c = GetCanvas();
        popup = new GameObject("Popup", typeof(RectTransform));
        var root = (RectTransform)popup.transform; root.SetParent(c.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        var dim = popup.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.55f);

        var win = Rt("Window", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 900));
        var frame = Rt("Frame", win, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1568, 908));
        frame.gameObject.AddComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.8f);   // 금색 테두리
        frame.SetAsFirstSibling();
        var inner = Rt("Inner", win, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 900));
        inner.gameObject.AddComponent<Image>().color = Panel; inner.SetSiblingIndex(1);

        Txt(Rt("Title", win, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(1400, 60)), 44, Gold, TextAnchor.MiddleCenter, FontStyle.Bold).text = "조작 · 스킬 안내";

        // 왼쪽: 기본 조작
        var left = Rt("Controls", win, new Vector2(0, 1), new Vector2(50, -110), new Vector2(620, 680));
        Txt(Rt("Head", left, new Vector2(0, 1), Vector2.zero, new Vector2(620, 44)), 32, Gold, TextAnchor.MiddleLeft, FontStyle.Bold).text = "기본 조작";
        var ctrl = new (string key, string desc)[]
        {
            ("← →", "이동"),
            ("← ← / → →", "대쉬 (누르고 있으면 계속 달림)"),
            ("Alt", "점프 (공중에서 한 번 더: 2단 점프)"),
            ("Ctrl", "공격 — 연타 시 1타 · 2타 · 3타 강공격"),
            ("Space", Blink()),
            ("X", "방어 — 몸통 공격을 막으면 패링 (적 기절)"),
            ("↓", "숙이기 — 높은 공격 피하기"),
            ("Esc", "설정 (저장 · 소리 · 시작 화면)"),
        };
        for (int i = 0; i < ctrl.Length; i++)
        {
            float y = -62 - i * 76;
            var badge = Rt("Key", left, new Vector2(0, 1), new Vector2(0, y), new Vector2(190, 56));
            badge.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.08f);
            Txt(Rt("K", badge, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 56)), 24, Gold, TextAnchor.MiddleCenter, FontStyle.Bold).text = ctrl[i].key;
            Txt(Rt("D", left, new Vector2(0, 1), new Vector2(206, y), new Vector2(440, 56)), 21, Soft, TextAnchor.MiddleLeft).text = ctrl[i].desc;
        }

        // 오른쪽: 스킬
        var right = Rt("Skills", win, new Vector2(0, 1), new Vector2(720, -110), new Vector2(790, 680));
        Txt(Rt("Head", right, new Vector2(0, 1), Vector2.zero, new Vector2(790, 44)), 32, Gold, TextAnchor.MiddleLeft, FontStyle.Bold).text = "스킬";
        int row = 0;
        foreach (var s in SkillRows()) AddSkillRow(right, row++, s);

        // 확인 버튼
        var ok = Rt("Btn OK", win, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(260, 70));
        ok.gameObject.AddComponent<Image>().color = new Color(0.65f, 0.14f, 0.24f, 1f);
        Txt(Rt("L", ok, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 70)), 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).text = "확인";
        var fx = ok.gameObject.AddComponent<UIButtonFx>(); fx.onClick = Close;

        StartCoroutine(Pop(win));
    }

    string Blink()
    {
        var p = FindSeria();
        return p != null ? $"섬광보 — 바라보는 쪽으로 {p.blinkDistance:0.#}칸 순간이동 (쿨타임 {p.blinkCooldown:0.#}초)" : "섬광보 — 앞으로 순간이동";
    }

    struct Row { public Sprite icon; public string key, name, desc; public int unlock; public bool learned; }

    List<Row> SkillRows()
    {
        var list = new List<Row>();
        var p = FindSeria();
        var hud = FindObj<SeriaHUD>();
        if (p != null)
            list.Add(new Row { icon = hud != null ? hud.skillIcon : null, key = "Shift", name = "스킬 베기", unlock = 1, learned = true,
                desc = $"앞을 크게 베는 기술 · 데미지 {p.skillDamage:0} · 쿨타임 {p.skillCooldown:0.#}초" });
        var sk = p != null ? p.GetComponent<SeriaSkills>() : null;
        if (sk != null)
        {
            var info = new (SeriaSkills.Skill s, string icon, string desc)[]
            {
                (sk.swordWave, "Skills/Icon_SwordWave", "앞으로 날아가는 검기"),
                (sk.lightning, "Skills/Icon_Lightning", "앞쪽 가장 가까운 적에게 번개"),
                (sk.meteor, "Skills/Icon_Meteor", $"작은 돌 2개(각 {sk.meteorSmallDamage:0}) 뒤 거대한 메테오"),
                (sk.swordRain, "Skills/Icon_SwordRain", "하늘 마법진에서 검이 비처럼 쏟아짐 (적마다 1번)"),
            };
            foreach (var i in info)
                list.Add(new Row { icon = Resources.Load<Sprite>(i.icon), key = i.s.key.ToString(), name = i.s.name, unlock = i.s.unlockLevel,
                    learned = sk.Unlocked(i.s), desc = $"{i.desc} · 데미지 {i.s.damage:0} · 쿨타임 {i.s.cooldown:0.#}초" });
        }
        return list;
    }

    void AddSkillRow(RectTransform parent, int i, Row r)
    {
        float y = -62 - i * 112;
        var box = Rt("Row", parent, new Vector2(0, 1), new Vector2(0, y), new Vector2(790, 100));
        box.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, r.learned ? 0.07f : 0.03f);
        var ic = Rt("Icon", box, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(84, 84));
        var img = ic.gameObject.AddComponent<Image>(); img.sprite = r.icon; img.preserveAspect = true;
        img.color = r.icon == null ? new Color(1, 1, 1, 0.1f) : (r.learned ? Color.white : new Color(0.35f, 0.35f, 0.4f));
        var key = Rt("Key", box, new Vector2(0, 1), new Vector2(110, -10), new Vector2(90, 38));
        key.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
        Txt(Rt("K", key, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 38)), 22, Gold, TextAnchor.MiddleCenter, FontStyle.Bold).text = r.key;
        Txt(Rt("Name", box, new Vector2(0, 1), new Vector2(214, -10), new Vector2(560, 38)), 28, r.learned ? Color.white : Dim, TextAnchor.MiddleLeft, FontStyle.Bold).text =
            r.name + (r.learned ? "" : $"   <size=22><color=#E8A060>Lv {r.unlock} 에 습득</color></size>");
        Txt(Rt("Desc", box, new Vector2(0, 1), new Vector2(110, -54), new Vector2(670, 44)), 21, r.learned ? Soft : Dim, TextAnchor.MiddleLeft).text = r.desc;
    }

    void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; ClosedFrame = Time.frameCount; Time.timeScale = prevScale;
        if (popup != null) Destroy(popup);
    }

    IEnumerator Pop(RectTransform rt)
    {
        for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime) { rt.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t / 0.18f); yield return null; }
        rt.localScale = Vector3.one;
    }

    static SeriaController FindSeria()
    {
        var g = GameObject.Find("Seria");
        return g != null ? g.GetComponent<SeriaController>() : FindObj<SeriaController>();
    }
    static T FindObj<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }
}
