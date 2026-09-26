// 배경음악: 화면(맵)에 따라 자동으로 곡을 골라 반복 재생
//  타이틀 화면(Title Screen)      → BGM_Title
//  시작 마을(Town Background)  → BGM_Town   (Hearthfire and Cobblestone)
//  정령의 숲(Forest Background) → BGM_Forest (Beyond the Sacred Canopy)
//  멸망한 왕국 성곽(Castle Background) → BGM_Castle (Crown of Fallen Stone)
//  왕좌의 간 - 최종 보스(Throne Background) → BGM_Final
//  같은 지역 안에서 맵을 이동하면 끊기지 않고 이어지고, 지역이 바뀌면 부드럽게 교체
//  볼륨은 PlayerPrefs 에 저장 (게임을 껐다 켜도 유지)
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMPlayer : MonoBehaviour
{
    public static BGMPlayer Instance { get; private set; }
    const string VolumeKey = "BGM_Volume";
    const float DefaultVolume = 0.6f;

    // (배경 오브젝트 이름, Resources/Audio 안의 곡 이름)
    static readonly (string background, string clip)[] Areas =
    {
        ("Title Screen", "BGM_Title"),          // 타이틀 화면 (시작하기 / 이어하기)
        ("Throne Background", "BGM_Final"),     // 최종 보스 맵 (성곽 맵을 복사해 만들어서 성곽보다 먼저 확인)
        ("Castle Background", "BGM_Castle"),   // 멸망한 왕국 성곽 (숲 맵을 복사해 만들어서 숲보다 먼저 확인)
        ("Town Background", "BGM_Town"),
        ("Forest Background", "BGM_Forest"),
    };

    AudioSource src; Coroutine fade;

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            if (Instance != null && Instance.fade == null && Instance.src.isPlaying) Instance.src.volume = Mathf.Clamp01(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Instance != null) return;
        var go = new GameObject("BGM Player");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<BGMPlayer>();
        SceneManager.sceneLoaded += (s, m) => { if (Instance != null) Instance.Refresh(); };
        Instance.Refresh();
    }

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.loop = true; src.playOnAwake = false; src.volume = 0f;
    }

    // 소리를 들으려면 씬에 Audio Listener 가 하나 있어야 함 (타이틀 화면 카메라엔 없었음)
    AudioListener ownListener;
    void EnsureListener()
    {
        int others = 0;
#if UNITY_2023_1_OR_NEWER
        foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
#else
        foreach (var l in Object.FindObjectsOfType<AudioListener>())
#endif
            if (l != ownListener && l.enabled) others++;
        if (others == 0)
        {
            if (ownListener == null) ownListener = gameObject.AddComponent<AudioListener>();
            ownListener.enabled = true;
        }
        else if (ownListener != null) ownListener.enabled = false;   // 씬에 이미 있으면 중복 방지
    }

    void Refresh()
    {
        EnsureListener();
        AudioClip want = null;
        foreach (var a in Areas)
            if (GameObject.Find(a.background) != null)
            {
                want = Resources.Load<AudioClip>("Audio/" + a.clip);
                if (want == null) Debug.LogWarning("[BGM] Resources/Audio/" + a.clip + " 을 찾을 수 없습니다.");
                break;
            }

        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(SwitchTo(want));
    }

    IEnumerator SwitchTo(AudioClip clip)
    {
        if (clip != null && src.clip == clip && src.isPlaying)          // 같은 지역: 그대로 이어서
        {
            yield return Fade(Volume, 0.6f);
        }
        else
        {
            if (src.isPlaying) yield return Fade(0f, 0.6f);              // 이전 곡 서서히 끄기
            src.Stop();
            if (clip != null) { src.clip = clip; src.volume = 0f; src.Play(); yield return Fade(Volume, 1.2f); }
        }
        fade = null;
    }

    IEnumerator Fade(float target, float time)
    {
        float from = src.volume;
        for (float t = 0; t < time; t += Time.unscaledDeltaTime) { src.volume = Mathf.Lerp(from, target, t / time); yield return null; }
        src.volume = target;
    }
}
