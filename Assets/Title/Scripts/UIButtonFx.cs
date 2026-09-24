// 그림 위에 겹쳐 놓는 투명 버튼: 마우스를 올리면 밝아지고, 누르면 어두워짐
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public Action onClick;
    public bool disabledLook;                       // 비활성처럼 어둡게 (이어하기에 저장 데이터가 없을 때)
    public Color hoverColor = new Color(1, 1, 1, 0.18f);
    public Color pressColor = new Color(0, 0, 0, 0.28f);
    public Color disabledColor = new Color(0, 0, 0, 0.45f);
    public float idlePulse = 0f;                    // 0보다 크면 평소에 은은하게 반짝임

    Image img; bool hover, press;

    void Awake() { img = GetComponent<Image>(); }

    void Update()
    {
        Color c;
        if (disabledLook) c = press ? new Color(0, 0, 0, 0.6f) : disabledColor;
        else if (press) c = pressColor;
        else if (hover) c = hoverColor;
        else c = new Color(1, 1, 1, idlePulse > 0 ? Mathf.PingPong(Time.unscaledTime * 0.5f, 1f) * idlePulse : 0f);
        img.color = c;
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) { hover = false; press = false; }
    public void OnPointerDown(PointerEventData e) => press = true;
    public void OnPointerUp(PointerEventData e) => press = false;
    public void OnPointerClick(PointerEventData e) => onClick?.Invoke();
}
