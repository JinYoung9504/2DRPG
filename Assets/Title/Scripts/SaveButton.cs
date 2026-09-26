// 인게임 우측 상단 설정(톱니바퀴) 메뉴
//  톱니바퀴를 누르면 아래로 펼쳐짐 (게임 일시정지):  저장(플로피) / 소리(스피커, 볼륨 조절) / 시작 화면(집)
//  ESC 키로도 열고 닫을 수 있음
//  타이틀 화면(titleMode)에서는 소리 조절만 표시
//  ※ 이전 '저장 버튼' 스크립트를 대체합니다 (클래스 이름 유지 → 기존 맵에 자동 적용)
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SaveButton : MonoBehaviour
{
    public Sprite icon;          // 저장(플로피) 아이콘
    public Sprite toastSaved;
    public float size = 64f;
    public string titleScene = "Title";
    [Tooltip("타이틀 화면용: 소리 조절만 표시, 게임 일시정지 안 함")] public bool titleMode;

    Sprite gearIcon, soundIcon, muteIcon, homeIcon, toastHome;
    RectTransform gearBtn, panel, volumePopup, saveBtn, soundBtn;
    Image soundImg, dim; Slider slider; Text volText;
    bool open; float homeArmedUntil;

    public static bool IsOpen { get; private set; }

    void Start()
    {
        UIHelper.EnsureEventSystem();
        gearIcon = Resources.Load<Sprite>("Settings/Icon_Gear");
        soundIcon = Resources.Load<Sprite>("Settings/Icon_Sound");
        muteIcon = Resources.Load<Sprite>("Settings/Icon_SoundMute");
        homeIcon = Resources.Load<Sprite>("Settings/Icon_Home");
        toastHome = Resources.Load<Sprite>("Settings/Toast_Home");
        Build();
        SetOpen(false);
    }

    void OnDestroy() { if (IsOpen) { if (!titleMode) Time.timeScale = 1f; IsOpen = false; } }

    // ── UI 만들기 ──
    RectTransform Rt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 sz)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = sz;
        return rt;
    }

    RectTransform IconButton(string name, Transform parent, Vector2 pos, Sprite sprite, System.Action onClick, out Image img)
    {
        var rt = Rt(name, parent, new Vector2(1, 1), pos, new Vector2(size, size));
        img = rt.gameObject.AddComponent<Image>(); img.sprite = sprite;
        var over = UIHelper.Stretch("Press", rt).gameObject.AddComponent<Image>();
        over.sprite = sprite; over.color = new Color(1, 1, 1, 0);
        var fx = over.gameObject.AddComponent<UIButtonFx>();
        fx.hoverColor = new Color(1, 1, 1, 0.25f); fx.pressColor = new Color(0, 0, 0, 0.3f);
        fx.onClick = () => { StartCoroutine(Pop(rt)); onClick(); };
        return rt;
    }

    static Font GetFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    void Build()
    {
        var cgo = new GameObject("Settings Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var c = cgo.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 150;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;

        // 열려 있을 때 화면을 살짝 어둡게 (바깥을 누르면 닫힘)
        dim = UIHelper.Stretch("Dim", cgo.transform).gameObject.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.35f);
        var dimFx = dim.gameObject.AddComponent<UIButtonFx>();
        dimFx.hoverColor = dimFx.pressColor = new Color(0, 0, 0, 0.35f);
        dimFx.onClick = () => SetOpen(false);

        float step = size + 14f;
        gearBtn = IconButton("Btn Settings", cgo.transform, new Vector2(-24, -24), gearIcon, () => SetOpen(!open), out _);

        // 펼쳐지는 패널
        int rows = titleMode ? 1 : 3;
        float soundY = titleMode ? -8 : -8 - step;
        panel = Rt("Panel", cgo.transform, new Vector2(1, 1), new Vector2(-16, -24 - step + 8), new Vector2(size + 16, step * rows + 8));
        var pimg = panel.gameObject.AddComponent<Image>(); pimg.color = new Color(0.08f, 0.06f, 0.1f, 0.75f);
        if (!titleMode) saveBtn = IconButton("Btn Save", panel, new Vector2(-8, -8), icon, DoSave, out _);
        soundBtn = IconButton("Btn Sound", panel, new Vector2(-8, soundY), soundIcon, ToggleVolume, out soundImg);
        if (!titleMode) IconButton("Btn Home", panel, new Vector2(-8, -8 - step * 2), homeIcon, GoHome, out _);

        // 볼륨 조절 (스피커 왼쪽에 펼쳐짐)
        volumePopup = Rt("Volume", panel, new Vector2(1, 1), new Vector2(-size - 22, soundY), new Vector2(360, size));
        var vimg = volumePopup.gameObject.AddComponent<Image>(); vimg.color = new Color(0.08f, 0.06f, 0.1f, 0.85f);
        slider = BuildSlider(volumePopup);
        slider.value = BGMPlayer.Volume;
        slider.onValueChanged.AddListener(v => { BGMPlayer.Volume = v; UpdateVolumeLook(); });
        var vt = Rt("Value", volumePopup, new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(70, 40));
        volText = vt.gameObject.AddComponent<Text>(); volText.font = GetFont(); volText.fontSize = 24; volText.fontStyle = FontStyle.Bold;
        volText.alignment = TextAnchor.MiddleRight; volText.color = Color.white;
        volumePopup.gameObject.SetActive(false);
        UpdateVolumeLook();
    }

    Slider BuildSlider(RectTransform parent)
    {
        var root = Rt("Slider", parent, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(250, 30));
        var bg = UIHelper.Stretch("Background", root); bg.offsetMin = new Vector2(0, 9); bg.offsetMax = new Vector2(0, -9);
        bg.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.2f);
        var fillArea = UIHelper.Stretch("Fill Area", root); fillArea.offsetMin = new Vector2(0, 9); fillArea.offsetMax = new Vector2(0, -9);
        var fill = UIHelper.Stretch("Fill", fillArea); fill.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.7f, 0.43f);
        var handleArea = UIHelper.Stretch("Handle Area", root);
        var handle = new GameObject("Handle", typeof(RectTransform)).GetComponent<RectTransform>();
        handle.SetParent(handleArea, false); handle.sizeDelta = new Vector2(24, 0);
        var himg = handle.gameObject.AddComponent<Image>(); himg.color = new Color(0.96f, 0.92f, 0.84f);
        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill; s.handleRect = handle; s.targetGraphic = himg;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0; s.maxValue = 1;
        return s;
    }

    // ── 동작 ──
    void SetOpen(bool o)
    {
        open = o; IsOpen = o;
        panel.gameObject.SetActive(o);
        dim.gameObject.SetActive(o);
        if (!o) volumePopup.gameObject.SetActive(false);
        if (!titleMode) Time.timeScale = o ? 0f : 1f;   // 설정 중엔 게임 일시정지 (타이틀에선 안 함)
        if (titleMode && o) volumePopup.gameObject.SetActive(true);   // 타이틀: 바로 소리 조절 표시
        gearBtn.localScale = Vector3.one * (o ? 0.9f : 1f);   // 열려 있으면 살짝 눌린 모양
    }

    void DoSave()
    {
        if (SceneTransition.Busy) return;
        if (SaveSystem.Save()) Toast.Show(toastSaved);
    }

    void ToggleVolume() => volumePopup.gameObject.SetActive(!volumePopup.gameObject.activeSelf);

    void UpdateVolumeLook()
    {
        volText.text = Mathf.RoundToInt(slider.value * 100) + "%";
        soundImg.sprite = slider.value <= 0.001f ? muteIcon : soundIcon;
    }

    void GoHome()
    {
        // 실수 방지: 2초 안에 한 번 더 눌러야 이동
        if (Time.unscaledTime > homeArmedUntil)
        {
            homeArmedUntil = Time.unscaledTime + 2f;
            Toast.Show(toastHome, 1.6f);
            return;
        }
        SetOpen(false);
        SceneTransition.Go(titleScene, null);
    }

    IEnumerator Pop(RectTransform rt)
    {
        for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime)
        { rt.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(t / 0.2f * Mathf.PI)); yield return null; }
        rt.localScale = Vector3.one;
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        bool esc = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (esc && !titleMode && !SceneTransition.Busy && !SkillGuide.IsOpen && SkillGuide.ClosedFrame != Time.frameCount && !SkillTeacher.IsOpen && SkillTeacher.ClosedFrame != Time.frameCount) SetOpen(!open);
    }
}
