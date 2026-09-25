// 대화창: 말하는 사람의 얼굴 + 이름표 + 대사 (한 글자씩 출력)
//  세리아는 왼쪽, NPC 는 오른쪽에 얼굴이 나옴
//  넘기기: Space / Enter / Ctrl / Z / 마우스 클릭 / 화면 터치
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DialogueUI : MonoBehaviour
{
    public struct Entry { public string name; public Sprite face; public bool right; public string text; }

    public static bool IsOpen { get; private set; }
    static DialogueUI inst;

    Text nameText, bodyText; Image portrait; RectTransform plate, body, portraitRt; GameObject root; RectTransform nextMark;
    Entry[] entries; int index; bool typing; Action onClose; Coroutine typeCo;
    public float charsPerSecond = 35f;

    public static void Show(Entry[] list, Action onClosed = null)
    {
        if (list == null || list.Length == 0) return;
        if (inst == null) { var go = new GameObject("Dialogue UI"); inst = go.AddComponent<DialogueUI>(); inst.Build(); }
        inst.Open(list, onClosed);
    }

    // 예전 방식 (NPC 혼자 말하기)
    public static void Show(string speaker, Sprite face, string[] lines, Action onClosed = null)
    {
        if (lines == null) return;
        var list = new Entry[lines.Length];
        for (int i = 0; i < lines.Length; i++) list[i] = new Entry { name = speaker, face = face, right = true, text = lines[i] };   // NPC 혼자 말할 때도 오른쪽
        Show(list, onClosed);
    }

    static Font korean;
    public static Font KoreanFont()
    {
        if (korean != null) return korean;
        try { korean = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Droid Sans Fallback" }, 32); } catch { }
#if UNITY_2022_2_OR_NEWER
        if (korean == null) korean = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        if (korean == null) korean = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        return korean;
    }

    RectTransform Rt(string n, Transform p)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(p, false); return rt;
    }
    static void Set(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax) { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax; }

    Text Txt(RectTransform rt, int size, TextAnchor align, Color c)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = KoreanFont(); t.fontSize = size; t.alignment = align; t.color = c; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 1.15f;
        var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    void Build()
    {
        DontDestroyOnLoad(gameObject);
        UIHelper.EnsureEventSystem();
        var c = gameObject.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 300;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
        var r = (RectTransform)root.transform; r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0); r.pivot = new Vector2(0.5f, 0);
        r.anchoredPosition = new Vector2(0, 40); r.sizeDelta = new Vector2(1400, 300);

        // 대화창 틀: 항상 불투명하게 보임
        var frameRt = Rt("Frame", r); Set(frameRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var frame = frameRt.gameObject.AddComponent<Image>(); frame.sprite = Resources.Load<Sprite>("NPC/Dialog_Frame"); frame.color = Color.white; frame.raycastTarget = false;

        portraitRt = Rt("Portrait", r);
        portrait = portraitRt.gameObject.AddComponent<Image>(); portrait.preserveAspect = true; portrait.raycastTarget = false;

        plate = Rt("NamePlate", r);
        var pimg = plate.gameObject.AddComponent<Image>(); pimg.sprite = Resources.Load<Sprite>("NPC/Dialog_NamePlate"); pimg.raycastTarget = false;
        var nrt = Rt("Name", plate); Set(nrt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        nameText = Txt(nrt, 34, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.78f));

        body = Rt("Body", r);
        bodyText = Txt(body, 38, TextAnchor.UpperLeft, Color.white);

        nextMark = Rt("Next", r); Set(nextMark, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 24), new Vector2(-40, 54));
        Txt(nextMark, 30, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.45f)).text = "▼";

        // 클릭·터치 받는 투명 판 (색이 바뀌지 않도록 틀과 분리)
        var hitRt = Rt("ClickArea", r); Set(hitRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var hit = hitRt.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0);
        var fx = hitRt.gameObject.AddComponent<UIButtonFx>();
        fx.hoverColor = fx.pressColor = new Color(1, 1, 1, 0); fx.onClick = Advance;

        root.SetActive(false);
    }

    void Layout(bool right)
    {
        if (!right)
        {
            Set(portraitRt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(18, 18), new Vector2(330, 60));
            Set(plate, new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -32), new Vector2(660, 38));
            Set(body, Vector2.zero, Vector2.one, new Vector2(370, 40), new Vector2(-80, -60));
            portraitRt.localScale = Vector3.one;
        }
        else
        {
            Set(portraitRt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-330, 18), new Vector2(-18, 60));
            Set(plate, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-660, -32), new Vector2(-300, 38));
            Set(body, Vector2.zero, Vector2.one, new Vector2(80, 40), new Vector2(-370, -60));
        }
    }

    void Open(Entry[] list, Action closed)
    {
        entries = list; index = 0; onClose = closed;
        root.SetActive(true); IsOpen = true;
        ShowLine();
    }

    void ShowLine()
    {
        var e = entries[index];
        Layout(e.right);
        nameText.text = e.name; portrait.sprite = e.face; portrait.enabled = e.face != null;
        if (typeCo != null) StopCoroutine(typeCo);
        typeCo = StartCoroutine(Type(e.text ?? ""));
    }

    IEnumerator Type(string s)
    {
        typing = true; bodyText.text = "";
        for (int i = 1; i <= s.Length; i++) { bodyText.text = s.Substring(0, i); yield return new WaitForSecondsRealtime(1f / charsPerSecond); }
        typing = false;
    }

    void Advance()
    {
        if (!IsOpen) return;
        if (typing) { StopCoroutine(typeCo); bodyText.text = entries[index].text; typing = false; return; }
        index++;
        if (index >= entries.Length) { Close(); return; }
        ShowLine();
    }

    void Close()
    {
        root.SetActive(false); IsOpen = false;
        var cb = onClose; onClose = null; cb?.Invoke();
    }

    int openedFrame;
    void Update()
    {
        if (!IsOpen) { openedFrame = Time.frameCount; return; }
        if (Time.frameCount - openedFrame < 2) return;
        nextMark.gameObject.SetActive(!typing && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f);
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        bool next = k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.leftCtrlKey.wasPressedThisFrame || k.rightCtrlKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame);
#else
        bool next = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Z);
#endif
        if (next) Advance();
    }

    void OnDestroy() { if (inst == this) { IsOpen = false; inst = null; } }
}
