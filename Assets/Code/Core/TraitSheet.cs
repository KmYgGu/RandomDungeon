using System.Collections.Generic;

/// <summary>시트 한 줄. 특성 하나의 정의.</summary>
public class TraitDefinition
{
    /// <summary>특성 번호(101~). 동료가 이 번호로 자기 특성을 가리킨다.</summary>
    public int Id;

    /// <summary>화면에 보여 줄 이름("혼신").</summary>
    public string Name;

    /// <summary>무엇을 하는 특성인지 사람에게 보여 주는 글.</summary>
    public string Description;
}

/// <summary>
/// 엑셀에서 읽어 온 특성 줄을 게임이 쓰는 모습으로 옮겨 담는다.
///
/// ── 여기에는 '무엇을 하는가' 가 없다 ──────────────────────────────
/// 표가 담는 것은 번호 · 이름 · 설명뿐이다. "치명타시 방어력을 무시한다" 같은
/// <b>동작은 코드</b>이고, 표는 그것을 글로 설명만 한다. 상태이상(ConditionEffect)이나
/// 함정(TrapAnimation)처럼 코드가 아는 목록을 시트가 고르게 하는 방식과는
/// 일부러 다르게 두었다 — 특성 열아홉 개는 저마다 손대는 자리가 다르고,
/// 그것을 하나의 목록으로 줄 세우면 그 목록이 곧 거짓말이 된다.
///
/// 그래서 특성을 실제로 구현할 때는 번호(Id)로 갈래를 나눈다. 표에서 이름이나
/// 설명을 다듬어도 게임이 바뀌지 않는 이유가 그것이다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class TraitSheet
{
    public static List<TraitDefinition> FromEntities(
        IEnumerable<TraitEntity> rows, List<string> problems = null)
    {
        var result = new List<TraitDefinition>();
        if (rows == null) return result;

        foreach (TraitEntity row in rows)
        {
            if (row == null) continue;

            // 번호가 없는 줄은 동료가 가리킬 길이 없으므로 버린다.
            if (row.id == 0) continue;

            string name = Clean(row.name);
            if (name.Length == 0)
                problems?.Add($"{row.id} 번 특성의 이름(name)이 비어 있습니다.");

            result.Add(new TraitDefinition
            {
                Id = row.id,
                Name = name,
                Description = Clean(row.explanation),
            });
        }

        return result;
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '﻿', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);
}
