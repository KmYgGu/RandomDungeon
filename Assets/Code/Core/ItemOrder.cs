using System.Collections.Generic;

/// <summary>
/// 가방을 정리할 때의 차례.
///
/// ── 왜 문화권 비교를 쓰지 않는가 ──────────────────────────────────
/// string.Compare 의 기본값은 실행하는 기기의 문화권을 따른다. 그러면 같은
/// 가방이 기기마다 다른 차례로 정리되고, 검사도 돌리는 곳에 따라 답이 달라진다.
/// 여기서는 코드 값 그대로(Ordinal) 비교한다.
///
/// 한글은 그래도 가나다 순이 된다. 완성형 한글(U+AC00~)이 초성 → 중성 → 종성
/// 차례로 배열돼 있어서, 코드 값 순서가 곧 사전 순서이기 때문이다.
/// 숫자·영문이 섞이면 그것들이 앞에 오는데, 그 편이 "왜 이번엔 차례가 다르지" 보다 낫다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ItemOrder
{
    /// <summary>
    /// 이름으로 견준다. a 가 앞이면 음수, 뒤면 양수.
    ///
    /// 이름이 없는 것은 뒤로 보낸다. 정의가 연결되지 않아 이름을 못 얻은
    /// 아이템인데, 그런 것이 앞에 서면 정리한 결과가 더 어지러워 보인다.
    /// </summary>
    public static int CompareByName(string a, string b)
    {
        bool emptyA = string.IsNullOrEmpty(a);
        bool emptyB = string.IsNullOrEmpty(b);

        if (emptyA && emptyB) return 0;
        if (emptyA) return 1;
        if (emptyB) return -1;

        return string.CompareOrdinal(a, b);
    }

    /// <summary>
    /// 이름 목록을 차례대로 늘어놓는다. 무엇을 정렬하든 규칙은 하나여야 하므로
    /// 검사도 이 길로 들어온다.
    /// </summary>
    public static void SortNames(List<string> names)
    {
        names?.Sort(CompareByName);
    }
}
