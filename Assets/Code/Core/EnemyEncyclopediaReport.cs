using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 적 도감 화면에 무엇을 어떻게 띄울지 계산한다.
///
/// ── 쓰러뜨린 만큼 열린다 ──────────────────────────────────────────
/// 중단의 여섯 줄은 처음부터 다 보이지 않는다. 그 적을 몇 마리나 쓰러뜨렸는지에
/// 따라 세 단계로 열린다. 아래 Rows 표가 "무엇이 몇 마리에 열리는가" 를 전부
/// 쥐고 있으므로, 항목을 늘리거나 문턱을 조절할 때 고칠 곳은 그 표 하나다.
///
/// 아직 안 열린 줄은 지우지 않고 <b>남겨 둔 채 가린다.</b> 줄이 통째로 사라지면
/// 더 볼 것이 있다는 사실 자체가 보이지 않아서, 도감을 채울 이유가 생기지 않는다.
///
/// 글자를 만드는 일만 한다. 화면도 입력도 모르므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class EnemyEncyclopediaReport
{
    /// <summary>가려져 있는 값 자리에 놓는 글자.</summary>
    public const string HiddenValue = EncyclopediaFacts.HiddenValue;

    /// <summary>
    /// 중단에 놓이는 여섯 항목과 각자의 문턱.
    ///
    /// 순서는 화면에 보이는 순서다. 싸움에 바로 쓰이는 것(체력·공격력)이 먼저 열리고,
    /// 셈이 필요한 것(방어력·경험치)이 그다음, 모으는 재미에 가까운 것
    /// (드롭 확률·종족)이 마지막이다.
    ///
    /// <b>문턱이 같은 것끼리 한 줄에 놓인다</b>(EncyclopediaFacts.BuildLines).
    /// 그래서 화면에는 여섯 줄이 아니라 세 줄로 뜬다.
    /// </summary>
    public static readonly FactRow<MonsterDefinition>[] Rows =
    {
        new FactRow<MonsterDefinition>("체력", 3, d => d.Hp.ToString()),
        new FactRow<MonsterDefinition>("공격력", 3, d => d.Attack.ToString()),
        new FactRow<MonsterDefinition>("방어력", 5, d => d.Defense.ToString()),
        new FactRow<MonsterDefinition>("경험치", 5, d => d.Exp.ToString()),
        new FactRow<MonsterDefinition>("드롭 확률", 10, d => MonsterSheet.NameOf(d.Rate)),
        new FactRow<MonsterDefinition>("종족", 10, d => EncyclopediaFacts.Or(d.Category)),
    };

    /// <summary>번호(id)가 작은 것부터 늘어놓는다. 화면의 "No." 가 이 순서를 따른다.</summary>
    public static List<MonsterDefinition> SortedEntries(IEnumerable<MonsterDefinition> all)
    {
        var sorted = new List<MonsterDefinition>(all);
        sorted.Sort((a, b) => a.Id.CompareTo(b.Id));
        return sorted;
    }

    /// <summary>
    /// 목록에 채울 글자. 아이템 도감과 같은 모습이다(ItemEncyclopediaReport.BuildText).
    ///
    /// 한 번도 못 쓰러뜨린 적은 이름 대신 물음표로 가린다.
    /// </summary>
    public static string BuildListText(
        IReadOnlyList<MonsterDefinition> sorted, EnemyEncyclopedia dex,
        int selected, int offset, int capacity)
    {
        if (sorted == null || sorted.Count == 0) return string.Empty;

        int end = capacity > 0 ? Mathf.Min(sorted.Count, offset + capacity) : sorted.Count;

        var sb = new StringBuilder();
        for (int i = offset; i < end; i++)
        {
            if (i > offset) sb.Append('\n');
            sb.Append(i == selected ? "> " : "  ");
            sb.Append(i + 1).Append(". ");
            sb.Append(dex.IsDiscovered(sorted[i].Id) ? sorted[i].Name : "?");
        }
        return sb.ToString();
    }

    /// <summary>상단에 놓는 줄. 목록에서의 번호와 이름이다.</summary>
    public static string BuildTitle(int listNumber, MonsterDefinition definition)
        => $"No.{listNumber}  {definition.Name}";

    /// <summary>그림 아래에 놓는 줄.</summary>
    public static string BuildDefeatCount(int defeats) => $"쓰러뜨린 횟수 : {defeats}";

    /// <summary>중단. 문턱이 같은 것끼리 한 줄로 묶고, 아직 안 열린 것은 가린다.</summary>
    public static string BuildStatsText(MonsterDefinition definition, int defeats)
        => EncyclopediaFacts.BuildLines(Rows, definition, defeats);
}
