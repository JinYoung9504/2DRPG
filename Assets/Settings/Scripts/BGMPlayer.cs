// 배경음악: 마을 배경(Town Background)이 있는 맵에서 BGM_Town 을 반복 재생
//  씬에 따로 넣을 필요 없이 게임 시작 시 자동 생성되고, 맵을 이동해도 끊기지 않고 이어짐
//  볼륨은 PlayerPrefs 에 저장 (게임을 껐다 켜도 유지)
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMPlayer : MonoBehaviour
{
    public static BGMPlayer Instance { get; private set; }
    const string VolumeKey = "BGM_Volume";
    const float DefaultVolume = 0.6f;

    AudioSource src; AudioClip townClip; Coroutine fade;

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
        townClip = Resources.Load<AudioClip>("Audio/BGM_Town");
        if (townClip == null) Debug.LogWarning("[BGM] Resources/Audio/BGM_Town 을 찾을 수 없습니다.");
    }

    void Refresh()
    {
        bool town = GameObject.Find("Town Background") != null;
        if (town && townClip != null)
        {
            if (src.clip != townClip || !src.isPlaying) { src.clip = townClip; src.volume = 0f; src.Play(); }
            FadeTo(Volume, 1.2f);
        }
        else if (src.isPlaying) FadeTo(0f, 0.8f, stopAfter: true);
    }

    void FadeTo(float target, float time, bool stopAfter = false)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(target, time, stopAfter));
    }

    IEnumerator Fade(float target, float time, bool stopAfter)
    {
        float from = src.volume;
        for (float t = 0; t < time; t += Time.unscaledDeltaTime) { src.volume = Mathf.Lerp(from, target, t / time); yield return null; }
        src.volume = target;
        if (stopAfter) src.Stop();
        fade = null;
    }
}
