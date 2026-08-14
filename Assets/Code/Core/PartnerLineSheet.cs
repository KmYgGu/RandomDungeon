using System.Collections.Generic;

/// <summary>동료 하나의 대사 묶음.</summary>
public class PartnerLineSet
{
    /// <summary>동료 번호(partner 시트의 id 와 같다).</summary>
    public int Id;

    /// <summary>처음 만났을 때.</summary>
    public string Meet;

    /// <summary>
    /// 체력 구간별 대사. <see cref="PartnerLineSheet.Thresholds"/> 와 같은 차례다.
    /// 채우지 않은 칸은 빈 문자열이고, 그 구간에서는 아무 말도 하지 않는다.
    /// </summary>
    public string[] HpLines = new string[PartnerLineSheet.Thresholds.Length];

    /// <summary>고유 행동을 시작할 때.</summary>
    public string Action;

    /// <summary>고유 행동의 결과별 대사. 셋 다 채우지 않아도 된다.</summary>
    public string[] ActionCases = new string[3];
}

/// <summary>
/// 엑셀에서 읽어 온 동료 대사를 게임이 쓰는 모습으로 옮겨 담는다.
///
/// ── 빈 칸은 잘못이 아니다 ─────────────────────────────────────────
/// 지금 열두 명 중 셋만 끝까지 차 있고 일곱은 만났을 때 한 줄뿐이다. 대사는
/// 채워 가는 것이므로 빈 칸을 <b>알리지 않는다.</b> 다른 표와 다른 점이라
/// 여기 적어 둔다 — 이름이나 수치가 비면 게임이 이상해지지만, 대사가 비면
/// 그저 말을 하지 않을 뿐이다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class PartnerLineSheet
{
    /// <summary>
    /// 체력 구간의 <b>아래 끝</b>(%). 큰 것부터 늘어놓는다.
    ///
    /// 열 이름 hp80 은 "체력이 80% 일 때" 가 아니라 <b>"80% 이상일 때"</b> 다.
    /// 그렇게 읽지 않으면 79% 처럼 어느 칸에도 안 걸리는 구간이 생긴다.
    /// 맨 끝 1 은 1% 이상, 즉 살아 있는 동안 전부를 받는다.
    /// </summary>
    public static readonly int[] Thresholds = { 100, 80, 60, 30, 10, 1 };

    public static List<PartnerLineSet> FromEntities(
        IEnumerable<PartnerLineEntity> rows, List<string> problems = null)
    {
        var result = new List<PartnerLineSet>();
        if (rows == null) return result;

        foreach (PartnerLineEntity row in rows)
        {
            if (row == null) continue;

            // 번호가 없는 줄은 어느 동료의 대사인지 알 수 없다.
            if (row.id == 0) continue;

            var set = new PartnerLineSet
            {
                Id = row.id,
                Meet = Clean(row.meet),
                Action = Clean(row.action),
            };

            set.HpLines[0] = Clean(row.hp100);
            set.HpLines[1] = Clean(row.hp80);
            set.HpLines[2] = Clean(row.hp60);
            set.HpLines[3] = Clean(row.hp30);
            set.HpLines[4] = Clean(row.hp10);
            set.HpLines[5] = Clean(row.hp1);

            set.ActionCases[0] = Clean(row.actioncase1);
            set.ActionCases[1] = Clean(row.actioncase2);
            set.ActionCases[2] = Clean(row.actioncase3);

            result.Add(set);
        }

        return result;
    }

    /// <summary>
    /// 지금 체력(%)에 맞는 대사. 그 칸이 비어 있으면 빈 문자열이다.
    ///
    /// <b>비었을 때 위 칸으로 올라가 대신 읽지 않는다.</b> 그렇게 하면 채우다 만
    /// 동료가 엉뚱한 상황의 대사를 하게 된다 — 빈사인데 "여유롭습니다" 를 말한다.
    /// 말하지 않는 편이 낫다.
    /// </summary>
    /// <param name="percent">남은 체력의 백분율(0~100).</param>
    public static string LineForPercent(PartnerLineSet lines, int percent)
    {
        if (lines?.HpLines == null) return string.Empty;

        for (int i = 0; i < Thresholds.Length; i++)
        {
            if (percent < Thresholds[i]) continue;

            return i < lines.HpLines.Length ? lines.HpLines[i] ?? string.Empty : string.Empty;
        }

        // 1% 에도 못 미친다 = 쓰러진 것이다. 할 말이 따로 있다면 그건 대사가
        // 아니라 쓰러질 때의 연출이므로 여기서 내지 않는다.
        return string.Empty;
    }

    /// <summary>남은 체력을 백분율로 바꾼다. 최대가 0 이면 0 으로 본다.</summary>
    public static int PercentOf(int hp, int maxHp)
    {
        if (maxHp <= 0) return 0;
        if (hp <= 0) return 0;

        // 내림으로 센다. 99.9% 를 100% 로 올리면 한 대 맞고도 멀쩡한 대사를 한다.
        return (int)(hp * 100L / maxHp);
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '﻿', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);
}
