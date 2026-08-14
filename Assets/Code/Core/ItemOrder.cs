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

    /// <summary>
    /// 정리했을 때 앞에 서는 차례. 작을수록 앞이다.
    ///
    /// <b>지금 차고 있는 것</b>이 맨 앞에 무기 → 방어구 → 장신구 차례로 서고,
    /// 나머지는 전부 그 뒤에서 이름순으로 선다. <b>차고 있지 않은 장비는
    /// 나머지에 섞인다</b> — 몸에 걸친 것이 무엇인지가 가장 자주 확인하는
    /// 것이고, 가방 안의 여벌 장비는 이름으로 찾는 편이 빠르다.
    ///
    /// 차례를 EquipmentSlot 의 값(1·2·3)에 기대지 않고 여기서 다시 적는다.
    /// 열거형에 새 자리를 끼워 넣거나 값을 바꾸는 순간 정리 차례가 조용히
    /// 따라 바뀌면, 왜 바뀌었는지 알아낼 방법이 없다.
    /// </summary>
    /// <param name="equippedSlot">차고 있는 자리. 차고 있지 않으면 None.</param>
    public static int RankOf(EquipmentSlot equippedSlot)
    {
        switch (equippedSlot)
        {
            case EquipmentSlot.Weapon: return 0;
            case EquipmentSlot.Armor: return 1;
            case EquipmentSlot.Accessory: return 2;
            default: return 3;   // 차고 있지 않은 것 전부
        }
    }

    /// <summary>
    /// 가방을 정리할 때 둘을 견준다. 차고 있는 것이 먼저고, 그다음이 이름이다.
    /// </summary>
    public static int Compare(
        EquipmentSlot equippedA, string nameA, EquipmentSlot equippedB, string nameB)
    {
        int rankA = RankOf(equippedA);
        int rankB = RankOf(equippedB);

        if (rankA != rankB) return rankA - rankB;

        return CompareByName(nameA, nameB);
    }
}
