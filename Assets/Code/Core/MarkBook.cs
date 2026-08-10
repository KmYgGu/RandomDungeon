using System.Collections.Generic;

/// <summary>
/// 아이템 <b>종류마다</b> 붙여 둔 표식을 적어 두는 수첩.
///
/// ── 왜 개체가 아니라 종류인가 ─────────────────────────────────────
/// 시렌류에서 표식(메모)은 <b>미확인 아이템을 관리하려고</b> 있는 기능이다.
/// 판마다 겉모습이 섞이므로 "파란 물약" 을 하나 마셔 보고 알아낸 다음,
/// "이 종류 = 나쁜 것" 이라고 스스로 적어 둔다. 한 개에만 붙으면 다음에 만날
/// 같은 물약을 가려낼 수 없어서 적어 둔 뜻이 없어진다.
///
/// 같은 이유로 원작은 감정(식별)도 종류 단위다. 표식은 그 규칙의 손으로 하는 버전이다.
///
/// 종류에 붙어 있으므로 던지든 바닥에 두든 표식은 그대로고, <b>바닥에 놓인 것에도
/// 같은 표식이 보인다.</b> 줍기 전에 알아보는 것이 오히려 이 기능의 요점이다.
///
/// 개체마다 다른 값(강화 수치·저주)은 게임이 알아서 따로 보여 주므로 여기 들어오지 않는다.
///
/// ── 판 단위다 ─────────────────────────────────────────────────────
/// 표식은 그 판에서 알아낸 것이라 층을 넘어가도 남고, 새 판에서는 지워진다
/// (GameManager.ResetRun). 미확인 아이템이 생기면 겉모습도 판마다 다시 섞이므로,
/// 지난 판의 표식을 물려받으면 거짓말이 된다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class MarkBook
{
    private readonly struct Entry
    {
        public readonly MarkShape Shape;
        public readonly MarkColor Color;

        public Entry(MarkShape shape, MarkColor color)
        {
            Shape = shape;
            Color = color;
        }
    }

    private readonly Dictionary<int, Entry> marks = new Dictionary<int, Entry>();

    /// <summary>적어 둔 것이 하나도 없는가.</summary>
    public bool IsEmpty => marks.Count == 0;

    /// <summary>
    /// 표식을 적는다. 모양이 None 이면 지우는 것이다.
    ///
    /// 번호가 0 인 것은 정의가 연결되지 않은 아이템이라 받지 않는다.
    /// 받아 두면 서로 다른 물건들이 0 번 하나를 나눠 갖게 된다.
    /// </summary>
    public void Set(int itemId, MarkShape shape, MarkColor color)
    {
        if (itemId == 0) return;

        if (shape == MarkShape.None)
        {
            marks.Remove(itemId);
            return;
        }

        marks[itemId] = new Entry(shape, color);
    }

    /// <summary>적어 둔 것을 꺼낸다. 없으면 false.</summary>
    public bool TryGet(int itemId, out MarkShape shape, out MarkColor color)
    {
        if (marks.TryGetValue(itemId, out Entry entry))
        {
            shape = entry.Shape;
            color = entry.Color;
            return true;
        }

        shape = MarkShape.None;
        color = MarkColor.Red;
        return false;
    }

    /// <summary>이름 앞에 붙일 글자. 적어 둔 것이 없으면 빈 문자열.</summary>
    public string PrefixOf(int itemId)
        => TryGet(itemId, out MarkShape shape, out MarkColor color)
            ? ItemMark.Prefix(shape, color)
            : string.Empty;

    /// <summary>새 판을 시작할 때.</summary>
    public void Clear() => marks.Clear();
}
