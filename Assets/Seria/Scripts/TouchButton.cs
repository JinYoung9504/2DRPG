// 화면 터치 버튼 하나. 누르고 있는 동안 지정한 키를 누른 것으로 처리합니다.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public KeyCode key;
    public float idleAlpha = 0.45f;     // 평소 투명도 (낮을수록 흐릿)
    public float pressedAlpha = 0.7f;   // 누를 때 투명도

    Image img; bool pressed; Vector3 baseScale;

    void Awake() { img = GetComponent<Image>(); baseScale = transform.localScale; SetLook(false); }

    public void OnPointerDown(PointerEventData e)
    {
        if (pressed) return;
        pressed = true; VirtualInput.Press(key); SetLook(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!pressed) return;
        pressed = false; VirtualInput.Release(key); SetLook(false);
    }

    void OnDisable() { if (pressed) { pressed = false; VirtualInput.Release(key); SetLook(false); } }

    void SetLook(bool down)
    {
        if (img == null) return;
        var c = img.color; c.a = down ? pressedAlpha : idleAlpha; img.color = c;
        transform.localScale = baseScale * (down ? 0.92f : 1f);
    }
}
