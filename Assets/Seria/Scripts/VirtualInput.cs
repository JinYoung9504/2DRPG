// 화면 터치 버튼 입력을 키보드 키처럼 다루기 위한 저장소
// TouchButton 이 여기에 "눌림/뗌"을 기록하고, SeriaController 가 키보드와 함께 읽습니다.
using System.Collections.Generic;
using UnityEngine;

public static class VirtualInput
{
    class State { public int holders; public int downFrame = -10, upFrame = -10; public bool downRead, upRead; public int downReadFrame = -10, upReadFrame = -10; }
    static readonly Dictionary<KeyCode, State> states = new Dictionary<KeyCode, State>();

    static State Get(KeyCode k)
    {
        if (!states.TryGetValue(k, out var s)) { s = new State(); states[k] = s; }
        return s;
    }

    public static void Press(KeyCode k)
    {
        var s = Get(k);
        if (s.holders++ == 0) { s.downFrame = Time.frameCount; s.downRead = false; }
    }

    public static void Release(KeyCode k)
    {
        var s = Get(k);
        if (s.holders == 0) return;
        if (--s.holders == 0) { s.upFrame = Time.frameCount; s.upRead = false; }
    }

    public static bool Held(KeyCode k) => k != KeyCode.None && states.TryGetValue(k, out var s) && s.holders > 0;

    // 버튼 이벤트와 스크립트 실행 순서가 달라도 놓치지 않도록 1프레임 여유를 두고, 한 번 읽으면 소모
    public static bool Down(KeyCode k)
    {
        if (k == KeyCode.None || !states.TryGetValue(k, out var s)) return false;
        if (s.downRead && s.downReadFrame == Time.frameCount) return true;              // 같은 프레임 안에선 여러 번 읽어도 됨
        if (!s.downRead && Time.frameCount - s.downFrame <= 1) { s.downRead = true; s.downReadFrame = Time.frameCount; return true; }
        return false;
    }

    public static bool Up(KeyCode k)
    {
        if (k == KeyCode.None || !states.TryGetValue(k, out var s)) return false;
        if (s.upRead && s.upReadFrame == Time.frameCount) return true;
        if (!s.upRead && Time.frameCount - s.upFrame <= 1) { s.upRead = true; s.upReadFrame = Time.frameCount; return true; }
        return false;
    }

    public static void ReleaseAll() { states.Clear(); }
}
