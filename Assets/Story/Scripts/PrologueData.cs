// 오프닝 대사 (새 게임 시작 시 검은 화면에 나오는 이야기)
//  Project 창에서 Assets/Story/Resources/Story/Prologue 선택 → Inspector 에서 대사 추가·수정·순서 변경
//  한 줄 = 한 화면. 말하는 사람을 비워두면 해설(가운데 흰 글씨), 이름을 넣으면 이름과 함께 대사로 표시
using UnityEngine;

[CreateAssetMenu(fileName = "Prologue", menuName = "Game/오프닝 대사")]
public class PrologueData : ScriptableObject
{
    [System.Serializable]
    public class Line
    {
        [Tooltip("비워두면 해설")] public string speaker = "";
        [TextArea(2, 5)] public string text = "";
    }

    [Tooltip("글자가 나오는 속도 (초당 글자 수)")] public float charsPerSecond = 22f;
    public Line[] lines = Default();

    public static Line[] Default() => new[]
    {
        new Line { text = "오래전, 이 땅에는 왕국의 이름으로 세상을 지키던 기사들이 있었다." },
        new Line { text = "그러나 붉은 달이 뜬 어느 밤,\n한 기사가 어둠과 계약을 맺었다." },
        new Line { text = "그의 이름은 아타칸.\n왕국은 하룻밤 사이에 무너졌고, 성은 죽은 자들의 것이 되었다." },
        new Line { text = "그로부터 십 년.\n변방의 작은 마을에서, 한 소녀가 검을 쥐었다." },
        new Line { speaker = "세리아", text = "…또 그 꿈이야.\n붉은 달, 무너지는 성, 그리고… 나를 부르던 목소리." },
        new Line { speaker = "세리아", text = "요즘 마을 근처까지 마물이 내려오고 있어.\n더 이상 가만히 있을 순 없어." },
        new Line { speaker = "세리아", text = "먼저 이장님께 가 보자." },
        new Line { text = "그렇게, 세리아의 여정이 시작되었다." },
    };
}
