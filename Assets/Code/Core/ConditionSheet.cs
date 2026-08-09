using System.Collections.Generic;

/// <summary>상태이상이 실제로 하는 일. 코드가 아는 것만 시트에서 고를 수 있다.</summary>
public enum ConditionEffect
{
    /// <summary>표시만 한다. 다른 곳에서 이미 처리하고 있거나, 아직 효과가 없는 것.</summary>
    None,
    /// <summary>턴마다 수치만큼 체력이 깎인다.</summary>
    DamagePerTurn,
}

/// <summary>이롭냐 해롭냐. 표시 순서가 이 차례다.</summary>
public enum ConditionKind
{
    Harmful,
    Beneficial,
    Neutral,
}

/// <summary>시트 한 줄. 상태이상 하나의 정의.</summary>
public class ConditionDefinition
{
    /// <summary>코드가 가리키는 이름. 시트에서 바꾸면 코드가 못 찾는다.</summary>
    public string Id;

    public string Name;
    public string Description;
    public ConditionKind Kind;
    public ConditionEffect Effect;

    /// <summary>효과가 쓰는 숫자. 턴당피해면 매 턴 깎이는 체력.</summary>
    public int Amount;

    /// <summary>0 이면 조건이 유지되는 동안 계속(굶주림처럼).</summary>
    public int DurationTurns;
}

/// <summary>
/// 엑셀에서 읽어 온 줄을 게임이 쓰는 모습으로 옮겨 담는다.
///
/// 상태이상은 앞으로 수십 가지가 될 것이라, 코드에 하나씩 적어 넣으면
/// 문구 하나 고치는 데도 빌드를 다시 해야 한다. 표(Assets/Resources/ExcelData.xlsx)를
/// 엑셀에서 편집하면 ExcelImporter 가 ScriptableObject 로 옮겨 주고,
/// 여기서는 그 줄들을 읽기 좋은 모습으로 바꾸기만 한다.
///
/// ── 시트가 담을 수 있는 것과 없는 것 ──────────────────────────────
/// 문구·분류·수치·지속턴은 데이터라서 시트가 담는다.
/// 하지만 "독은 매 턴 체력을 깎는다" 같은 동작은 코드다. 그래서 코드가 아는
/// 효과 종류(ConditionEffect)를 시트가 골라 쓰는 방식으로 나눴다.
/// 같은 종류에 숫자만 다른 상태는 시트에 한 줄 더하면 끝나고,
/// 아주 새로운 동작이 필요할 때만 코드를 건드리게 된다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ConditionSheet
{
    /// <summary>Resources 아래의 애셋 이름(확장자 없이).</summary>
    public const string ResourcePath = "ExcelData";

    /// <summary>
    /// 엑셀 줄들을 정의로 옮긴다.
    ///
    /// 알아볼 수 없는 값이 있는 줄은 그것만 기본값으로 두고, 무엇이 이상한지
    /// problems 에 적어 둔다. 한 줄이 잘못됐다고 나머지까지 버리면
    /// 표를 고치기가 더 어려워진다.
    ///
    /// 여기서 직접 로그를 찍지 않는다. 로그는 Unity 런타임이 있어야 돌아가서,
    /// 표 읽는 규칙을 Unity 없이 확인할 수 없게 되기 때문이다.
    /// 알리는 일은 부르는 쪽(ConditionCatalog)이 한다.
    /// </summary>
    public static List<ConditionDefinition> FromEntities(
        IEnumerable<ConditionEntity> rows, List<string> problems = null)
    {
        var result = new List<ConditionDefinition>();
        if (rows == null) return result;

        foreach (ConditionEntity row in rows)
        {
            if (row == null) continue;

            // 엑셀에서 붙여 넣다 보면 칸 끝에 탭·공백이 남는다.
            // 그대로 두면 id 가 미묘하게 달라져 코드가 못 찾는다.
            string id = Clean(row.id);
            if (id.Length == 0) continue;

            result.Add(new ConditionDefinition
            {
                Id = id,
                Name = Clean(row.name),
                Description = Clean(row.explanation),
                Kind = ParseKind(row.type),
                Effect = ParseEffect(row.effect, id, problems),
                Amount = row.value,
                DurationTurns = row.turn,
            });
        }

        return result;
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '\uFEFF', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);

    public static ConditionKind ParseKind(string cell)
    {
        switch (Clean(cell))
        {
            case "이로움": return ConditionKind.Beneficial;
            case "중립": return ConditionKind.Neutral;
            default: return ConditionKind.Harmful;
        }
    }

    public static ConditionEffect ParseEffect(string cell, string id, List<string> problems = null)
    {
        switch (Clean(cell))
        {
            case "턴당피해": return ConditionEffect.DamagePerTurn;
            case "없음": return ConditionEffect.None;
            default:
                problems?.Add($"'{id}' 의 효과 '{Clean(cell)}' 를 모릅니다. '없음' 으로 봅니다.");
                return ConditionEffect.None;
        }
    }
}
