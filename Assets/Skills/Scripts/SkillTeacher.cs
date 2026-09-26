// 마을 NPC 스킬 교관: NPC 와 대화가 끝나면 '스킬 습득' 창이 열림
//  - 레벨 조건을 채운 스킬은 [습득] 버튼으로 배움 → 바로 사용 가능 (HUD 에 나타남)
//  - 레벨이 모자라면 "Lv N 필요", 이미 배웠으면 "습득 완료"
//  - 기본 배치: 시작 마을 이장(A 검기 · S 번개) / 정령의 숲 요정(D 메테오) / 성곽 저주받은 기사(F 성검 유성우)
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkillTeacher : MonoBehaviour
{
    [Tooltip("가르치는 스킬의 단축키 (A / S / D / F)")]
    public string[] skillIds = { "A" };
    public string windowTitle = "스킬 습득";

    public static bool IsOpen { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;
    GameObject root; RectTransform list; float prevScale = 1f; Font font;
    SeriaSkills skills; PlayerLevel level;

    static readonly Color Panel = new Color(0.07f, 0.05f, 0.09f, 0.95f), Gold = new Color(1f, 0.82f, 0.52f), Soft = new Color(0.9f, 0.88f, 0.92f), Dim = new Color(0.6f, 0.58f, 0.65f);

    static string Icon(string id) => id == "A" ? "Skills/Icon_SwordWave" : id == "S" ? "Skills/Icon_Lightning" : id == "D" ? "Skills/Icon_Meteor" : "Skills/Icon_SwordRain";
    static string ToastName(string id) => id == "A" ? "Skills/Toast_Learn_SwordWave" : id == "S" ? "Skills/Toast_Learn_Lightning" : id == "D" ? "Skills/Toast_Learn_Meteor" : "Skills/Toast_Learn_SwordRain";
    static string Desc(string id) => id == "A" ? "앞으로 날아가는 검기" : id == "S" ? "앞쪽 가장 가까운 적에게 번개가 떨어짐"
        : id == "D" ? "작은 돌 2개 뒤 거대한 메테오가 떨어짐" : "하늘 마법진에서 검이 비처럼 쏟아짐 (적마다 1번)";

    public void Open()
    {
        if (IsOpen) return;
        var p = GameObject.Find("Seria");
        if (p == null) return;
        skills = p.GetComponent<SeriaSkills>(); level = p.GetComponent<PlayerLevel>();
        if (skills == null) return;
        StartCoroutine(OpenNextFrame());
    }

    IEnumerator OpenNextFrame()
    {
        yield return null;                                      // 대화창이 완전히 닫힌 뒤
        while (DialogueUI.IsOpen) yield return null;
        IsOpen = true; prevScale = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f;
        font = DialogueUI.KoreanFont();
        UIHelper.EnsureEventSystem();
        Build();
    }

    void Update()
    {
        if (IsOpen && (SeriaController.KeyDown(KeyCode.Escape) || SeriaController.KeyDown(KeyCode.Return))) Close();
    }

    void OnDestroy() { if (IsOpen) { IsOpen = false; Time.timeScale = prevScale; } }

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

    void Build()
    {
        root = new GameObject("Skill Teacher UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = root.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 170;
        var sc = root.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        var dim = UIHelper.Stretch("Dim", root.transform).gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, 0.55f);

        float h = 200 + skillIds.Length * 124;
        var frame = Rt("Frame", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1128, h + 8));
        frame.gameObject.AddComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.8f);
        var win = Rt("Window", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120, h));
        win.gameObject.AddComponent<Image>().color = Panel;
        Txt(Rt("Title", win, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1000, 56)), 40, Gold, TextAnchor.MiddleCenter, FontStyle.Bold).text = windowTitle;
        Txt(Rt("Lv", win, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(200, 40)), 24, Soft, TextAnchor.MiddleRight).text = level != null ? "현재 Lv." + level.level : "";
        list = Rt("List", win, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(1040, skillIds.Length * 124));
        Refresh();

        var ok = Rt("Btn Close", win, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(240, 64));
        ok.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.26f, 0.34f);
        Txt(Rt("L", ok, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 64)), 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).text = "닫기";
        ok.gameObject.AddComponent<UIButtonFx>().onClick = Close;
    }

    void Refresh()
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
        for (int i = 0; i < skillIds.Length; i++)
        {
            var s = skills.Find(skillIds[i]); if (s == null) continue;
            string id = skillIds[i];
            bool learned = skills.Unlocked(s), can = skills.CanLearn(s);
            var row = Rt("Row " + id, list, new Vector2(0.5f, 1), new Vector2(0, -i * 124), new Vector2(1040, 112));
            row.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.06f);
            var ic = Rt("Icon", row, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(88, 88));
            var img = ic.gameObject.AddComponent<Image>(); img.sprite = Resources.Load<Sprite>(Icon(id)); img.preserveAspect = true;
            img.color = can || learned ? Color.white : new Color(0.4f, 0.4f, 0.45f);
            var key = Rt("Key", row, new Vector2(0, 1), new Vector2(118, -12), new Vector2(64, 36));
            key.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
            Txt(Rt("K", key, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 36)), 22, Gold, TextAnchor.MiddleCenter, FontStyle.Bold).text = id;
            Txt(Rt("Name", row, new Vector2(0, 1), new Vector2(194, -12), new Vector2(560, 36)), 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold).text = s.name;
            Txt(Rt("Desc", row, new Vector2(0, 1), new Vector2(118, -56), new Vector2(640, 48)), 21, Soft, TextAnchor.MiddleLeft).text =
                $"{Desc(id)}\n데미지 {s.damage:0} · 쿨타임 {s.cooldown:0.#}초 · 필요 레벨 {s.unlockLevel}";

            var btn = Rt("Btn", row, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(210, 64));
            var bimg = btn.gameObject.AddComponent<Image>();
            string label; Color col;
            if (learned) { label = "습득 완료"; col = new Color(0.22f, 0.4f, 0.28f); }
            else if (can) { label = "습득"; col = new Color(0.66f, 0.15f, 0.25f); }
            else { label = $"Lv {s.unlockLevel} 필요"; col = new Color(0.25f, 0.23f, 0.28f); }
            bimg.color = col;
            Txt(Rt("L", btn, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 64)), 26, can || learned ? Color.white : Dim, TextAnchor.MiddleCenter, FontStyle.Bold).text = label;
            if (!learned && can)
                btn.gameObject.AddComponent<UIButtonFx>().onClick = () =>
                {
                    SkillBook.Learn(id);
                    var t = Resources.Load<Sprite>(ToastName(id)); if (t != null) Toast.Show(t, 2f);
                    Refresh();
                };
        }
    }

    void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; ClosedFrame = Time.frameCount; Time.timeScale = prevScale;
        if (root != null) Destroy(root);
    }
}
