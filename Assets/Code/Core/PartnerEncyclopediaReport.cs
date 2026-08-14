using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 동료 도감 화면에 무엇을 어떻게 띄울지 계산한다.
///
/// 적 도감(EnemyEncyclopediaReport)과 짜임이 같다. 다른 것은 둘뿐이다 —
/// 여는 열쇠가 쓰러뜨린 횟수가 아니라 <b>호감도</b>이고, 중단에 놓이는 것이
/// partner 시트의 열들(가방 칸·특성·성별·종족)이라는 점.
///
/// ── 문턱 숫자는 잠정이다 ──────────────────────────────────────────
/// 호감도가 무엇으로 오를지 아직 정하지 않았다(PartnerEncyclopedia 주석).
/// 그래서 지금은 아무도 호감도를 올리지 않고, 문턱을 적 도감처럼 3/5/10 으로
/// 두면 <b>중단이 통째로 물음표인 화면</b>이 된다.
///
/// 첫 단계를 0 으로 둔 것은 그 때문이다. 동행해 본 동료의 기본 수치는 지금도
/// 보이고, 호감도가 실제로 오르기 시작하면 뒤 두 단계가 순서대로 열린다.
/// 눈금이 정해지면 아래 Rows 의 숫자만 고치면 된다.
///
/// 글자를 만드는 일만 한다. 화면도 입력도 모르므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class PartnerEncyclopediaReport
{
    /// <summary>가려져 있는 값 자리에 놓는 글자.</summary>
    public const string HiddenValue = EncyclopediaFacts.HiddenValue;

    /// <summary>
    /// 중단에 놓이는 일곱 항목과 각자의 문턱.
    ///
    /// 싸움에 바로 쓰이는 것(체력·공격력·방어력)이 먼저, 데리고 다녀 봐야 아는 것
    /// (가방 칸·특성)이 그다음, 설정에 가까운 것(성별·종족)이 마지막이다.
    /// 적 도감이 세 단계로 나뉜 것과 같은 결이다.
    ///
    /// <b>문턱이 같은 것끼리 한 줄에 놓인다</b>(EncyclopediaFacts.BuildLines).
    /// 그래서 화면에는 일곱 줄이 아니라 세 줄로 뜬다.
    /// </summary>
    public static readonly FactRow<PartnerDefinition>[] Rows =
    {
        new FactRow<PartnerDefinition>("체력", 0, d => d.Hp.ToString()),
        new FactRow<PartnerDefinition>("공격력", 0, d => d.Attack.ToString()),
        new FactRow<PartnerDefinition>("방어력", 0, d => d.Defense.ToString()),
        new FactRow<PartnerDefinition>("가방 칸", 5, d => d.BagSize.ToString()),
        new FactRow<PartnerDefinition>("특성", 5, TraitNamesOf),
        new FactRow<PartnerDefinition>("성별", 10, d => EncyclopediaFacts.Or(d.Gender)),
        new FactRow<PartnerDefinition>("종족", 10, d => EncyclopediaFacts.Or(d.Category)),
    };

    /// <summary>
    /// 특성 이름들을 이어 붙인다. 번호가 아니라 이름으로 보여 준다 —
    /// 번호는 표를 채우는 사람의 것이지 노는 사람의 것이 아니다.
    ///
    /// 표에 없는 번호는 번호 그대로 남긴다. 조용히 빼 버리면 <b>특성 수가 줄어든
    /// 것조차 보이지 않아</b> 시트의 오타를 영영 못 찾는다(PartnerSheet.ReportUnknownTraits
    /// 가 로그로도 알리지만, 화면에서도 티가 나는 편이 낫다).
    /// </summary>
    private static string TraitNamesOf(PartnerDefinition definition)
    {
        if (definition?.TraitIds == null || definition.TraitIds.Count == 0)
            return EncyclopediaFacts.EmptyValue;

        var names = new List<string>();
        foreach (int id in definition.TraitIds)
        {
            TraitDefinition trait = TraitCatalog.Find(id);
            names.Add(trait != null && trait.Name.Length > 0 ? trait.Name : $"?{id}");
        }

        return string.Join(" · ", names);
    }

    /// <summary>번호(id)가 작은 것부터 늘어놓는다. 화면의 "No." 가 이 순서를 따른다.</summary>
    public static List<PartnerDefinition> SortedEntries(IEnumerable<PartnerDefinition> all)
    {
        var sorted = new List<PartnerDefinition>(all);
        sorted.Sort((a, b) => a.Id.CompareTo(b.Id));
        return sorted;
    }

    /// <summary>
    /// 목록에 채울 글자. 한 번도 동행해 보지 않은 동료는 이름을 물음표로 가린다.
    /// </summary>
    public static string BuildListText(
        IReadOnlyList<PartnerDefinition> sorted, PartnerEncyclopedia dex,
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
    public static string BuildTitle(int listNumber, PartnerDefinition definition)
        => $"No.{listNumber}  {definition.Name}";

    /// <summary>그림 아래에 놓는 줄. 적 도감의 '쓰러뜨린 횟수' 자리다.</summary>
    public static string BuildAffinity(int affinity) => $"호감도 : {affinity}";

    /// <summary>중단. 문턱이 같은 것끼리 한 줄로 묶고, 아직 안 열린 것은 가린다.</summary>
    public static string BuildStatsText(PartnerDefinition definition, int affinity)
        => EncyclopediaFacts.BuildLines(Rows, definition, affinity);
}
