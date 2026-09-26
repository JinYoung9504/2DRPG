// 타이틀 화면: 그림을 화면에 꽉 채우고, 그림 속 버튼 위치에 투명 버튼을 겹쳐 놓음
//  시작하기 → 검은 화면 오프닝 대사 → 첫 번째 맵으로 / 게임 종료
//  오른쪽 위 톱니바퀴 → 소리 조절 / 이어하기 → 마지막 저장 위치에서 계속
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleScreen : MonoBehaviour
{
    public string firstScene = "Map0_Town";

    [Header("그림")]
    public Sprite background;
    public Sprite backgroundBlur;          // 비워두면 Resources/Title_BG_Blur 사용
    public Sprite startMask, continueMask;
    public Sprite toastNoSave;

    // 원본 그림(1536x1024) 기준 버튼 위치 (픽셀, 좌상단 기준)
    static readonly Rect StartRect = new Rect(750, 518, 477, 108);
    static readonly Rect ContinueRect = new Rect(802, 656, 374, 76);
    static readonly Rect QuitRect = new Rect(802, 754, 374, 76);      // 이어하기 아래 '게임 종료'
    const float ArtW = 1536f, ArtH = 1024f;

    Image fade; bool leaving;

    void Start()
    {
        UIHelper.EnsureEventSystem();
        Build();
        StartCoroutine(FadeIn());
    }

    void Build()
    {
        var cgo = new GameObject("Title Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // 검은 바탕
        var bg = UIHelper.Stretch("Black", cgo.transform).gameObject.AddComponent<Image>(); bg.color = Color.black;

        // 뒤쪽: 흐리고 어두운 그림으로 화면 전체를 채움 (남는 여백용)
        if (backgroundBlur == null) backgroundBlur = Resources.Load<Sprite>("Title_BG_Blur");
        if (backgroundBlur != null)
        {
            var blur = UIHelper.Stretch("Blur Fill", cgo.transform);
            var bfit = blur.gameObject.AddComponent<AspectRatioFitter>();
            bfit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; bfit.aspectRatio = ArtW / ArtH;
            var bimg = blur.gameObject.AddComponent<Image>(); bimg.sprite = backgroundBlur; bimg.raycastTarget = false;
        }

        // 앞쪽: 원본 그림을 잘리지 않게 화면 안에 전부 표시
        var art = UIHelper.Stretch("Art", cgo.transform);
        var fit = art.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = ArtW / ArtH;
        var artImg = art.gameObject.AddComponent<Image>(); artImg.sprite = background; artImg.raycastTarget = false;

        var start = MakeButton(art, "Start", StartRect, startMask);
        start.idlePulse = 0.08f;
        start.onClick = OnStart;

        var cont = MakeButton(art, "Continue", ContinueRect, continueMask);
        cont.disabledLook = !SaveSystem.HasSave;
        cont.onClick = OnContinue;

        // 게임 종료 (그림에 없는 버튼이라 이어하기 모양의 그림을 겹쳐 그림)
        var quitArt = new GameObject("Quit Art", typeof(RectTransform)).GetComponent<RectTransform>();
        quitArt.SetParent(art, false);
        quitArt.anchorMin = new Vector2(QuitRect.xMin / ArtW, 1f - QuitRect.yMax / ArtH);
        quitArt.anchorMax = new Vector2(QuitRect.xMax / ArtW, 1f - QuitRect.yMin / ArtH);
        quitArt.offsetMin = quitArt.offsetMax = Vector2.zero;
        var qimg = quitArt.gameObject.AddComponent<Image>(); qimg.sprite = Resources.Load<Sprite>("Btn_Quit"); qimg.raycastTarget = false;
        var quit = MakeButton(art, "Quit", QuitRect, continueMask);
        quit.onClick = OnQuit;

        // 오른쪽 위 톱니바퀴 (소리 조절)
        var gear = new GameObject("Title Settings").AddComponent<SaveButton>();
        gear.titleMode = true; gear.transform.SetParent(transform, false);

        fade = UIHelper.Stretch("Fade", cgo.transform).gameObject.AddComponent<Image>();
        fade.color = Color.black; fade.raycastTarget = false;
    }

    UIButtonFx MakeButton(RectTransform art, string name, Rect px, Sprite mask)
    {
        var rt = new GameObject("Btn " + name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(art, false);
        // 그림 크기에 비례해 따라가도록 앵커를 그림 비율 좌표로 지정
        rt.anchorMin = new Vector2(px.xMin / ArtW, 1f - px.yMax / ArtH);
        rt.anchorMax = new Vector2(px.xMax / ArtW, 1f - px.yMin / ArtH);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = mask; img.color = new Color(1, 1, 1, 0);
        return rt.gameObject.AddComponent<UIButtonFx>();
    }

    IEnumerator FadeIn()
    {
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.8f) { fade.color = new Color(0, 0, 0, t); yield return null; }
        fade.color = new Color(0, 0, 0, 0);
    }

    void OnStart()
    {
        if (leaving) return;
        leaving = true;
        PlayerHealth.CarryHP = -1f;                 // 새 게임: 체력 가득
        PlayerLevel.CarryLevel = 1; PlayerLevel.CarryExp = 0;   // 새 게임: 1레벨
        GameProgress.Reset(); MonsterTalk.ResetShown();
        // 검은 화면 오프닝 대사 → 끝나면 첫 맵으로
        PrologueScreen.Play(() => SceneTransition.Go(firstScene, null));
    }

    void OnQuit()
    {
        if (leaving) return;
        leaving = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터에서는 플레이 모드 종료
#else
        Application.Quit();
#endif
    }

    void OnContinue()
    {
        if (leaving) return;
        var data = SaveSystem.Load();
        if (data == null || string.IsNullOrEmpty(data.scene)) { Toast.Show(toastNoSave); return; }
        leaving = true;
        PlayerHealth.CarryHP = data.hp;
        PlayerLevel.CarryLevel = Mathf.Max(1, data.level); PlayerLevel.CarryExp = data.exp;
        GameProgress.Load(data.defeatedBosses); MonsterTalk.ResetShown();
        SceneTransition.GoToPosition(data.scene, new Vector3(data.x, data.y, 0), data.facingLeft);
    }
}
