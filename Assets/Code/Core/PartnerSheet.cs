using System.Collections.Generic;

/// <summary>시트 한 줄. 동료 하나의 정의.</summary>
public class PartnerDefinition
{
    /// <summary>표 번호. 코드와 프리팹이 이 번호로 서로를 찾는다.</summary>
    public int Id;

    /// <summary>플레이어에게 보이는 이름("모험가").</summary>
    public string Name;

    public int Hp;
    public int Attack;
    public int Defense;

    /// <summary>이 동료가 들고 다닐 수 있는 가방 칸 수.</summary>
    public int BagSize;

    /// <summary>
    /// 이 동료가 가진 특성 번호들. 빈 칸은 담기지 않으므로 <b>개수가 곧 특성 수</b>다.
    ///
    /// 시트에는 skill1~6 으로 여섯 칸이 펼쳐져 있지만 지금은 모두 셋만 차 있다.
    /// 목록으로 받아 두면 넷째 이후를 채워도 이 아래 코드는 고칠 일이 없다.
    /// </summary>
    public List<int> TraitIds = new List<int>();

    /// <summary>성별("남성" · "여성").</summary>
    public string Gender;

    /// <summary>종족("인간" · "엘프" · "언데드" · "요정").</summary>
    public string Category;

    /// <summary>도감·소개에 띄울 글.</summary>
    public string Description;

    /// <summary>사람이 읽는 설정 메모. 게임이 이 글자를 보지 않는다.</summary>
    public string Note;
}

/// <summary>
/// 엑셀에서 읽어 온 동료 줄을 게임이 쓰는 모습으로 옮겨 담는다.
///
/// MonsterSheet · ItemSheet 과 같은 규칙을 따른다. 알아볼 수 없는 값이 있는
/// 줄은 그것만 기본값으로 두고 무엇이 이상한지 problems 에 적어 둔다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class PartnerSheet
{
    public static List<PartnerDefinition> FromEntities(
        IEnumerable<PartnerEntity> rows, List<string> problems = null)
    {
        var result = new List<PartnerDefinition>();
        if (rows == null) return result;

        foreach (PartnerEntity row in rows)
        {
            if (row == null) continue;

            // 번호가 없는 줄은 프리팹이 찾을 길이 없으므로 버린다.
            if (row.id == 0) continue;

            string name = Clean(row.name);
            if (name.Length == 0)
                problems?.Add($"{row.id} 번 동료의 이름(name)이 비어 있습니다.");

            // 체력이 0 이면 따라오자마자 쓰러진다. 빈칸을 눈치채기 어려운 자리다.
            if (row.hp <= 0)
                problems?.Add($"{row.id} 번 동료의 체력(hp)이 {row.hp} 입니다.");

            result.Add(new PartnerDefinition
            {
                Id = row.id,
                Name = name,
                Hp = row.hp,
                Attack = row.atk,
                Defense = row.def,
                BagSize = row.bag,
                TraitIds = CollectTraits(row, problems),
                Gender = Clean(row.gender),
                Category = Clean(row.category),
                Description = Clean(row.explanation),
                Note = Clean(row.note),
            });
        }

        return result;
    }

    /// <summary>
    /// 여섯 칸에 흩어진 특성 번호를 목록 하나로 모은다.
    ///
    /// <b>빈 칸(0)은 담지 않는다.</b> 지금 넷째부터가 모두 비어 있는데, 그것을
    /// "0 번 특성" 으로 담아 두면 나중에 특성을 찾을 때마다 0 을 걸러 내야 한다.
    ///
    /// 같은 번호를 두 번 적은 것은 알려 준다. 특성이 두 번 걸리는지 한 번만
    /// 걸리는지는 코드가 정할 일인데, 표를 채운 사람은 두 배가 될 것으로 읽는다.
    ///
    /// <b>시트에 skill 열을 늘리면 이 목록도 함께 늘려야 한다.</b> 늘리지 않으면
    /// 그 칸은 조용히 버려진다 — skill6 이 실제로 그렇게 한 번 사라졌다.
    /// </summary>
    private static List<int> CollectTraits(PartnerEntity row, List<string> problems)
    {
        var traits = new List<int>();

        foreach (int id in new[]
                 { row.skill1, row.skill2, row.skill3, row.skill4, row.skill5, row.skill6 })
        {
            if (id == 0) continue;

            if (traits.Contains(id))
            {
                problems?.Add($"{row.id} 번 동료에게 특성 {id} 이(가) 두 번 적혀 있습니다. 한 번만 봅니다.");
                continue;
            }

            traits.Add(id);
        }

        return traits;
    }

    /// <summary>
    /// 표에 없는 특성 번호를 가리키는 동료를 찾아 알린다.
    ///
    /// <b>시트 둘을 맞춰 보는 일이라 옮겨 담기와 따로 둔다.</b> 번호를 잘못 적으면
    /// 그 특성만 조용히 없는 것이 된다 — 동료는 멀쩡히 따라오고 에러도 나지 않아,
    /// "왜 이 녀석만 약하지" 를 한참 뒤에야 의심하게 된다.
    /// </summary>
    public static void ReportUnknownTraits(
        IEnumerable<PartnerDefinition> partners,
        IEnumerable<TraitDefinition> traits,
        List<string> problems)
    {
        if (partners == null || problems == null) return;

        var known = new HashSet<int>();
        if (traits != null)
        {
            foreach (TraitDefinition trait in traits)
                if (trait != null) known.Add(trait.Id);
        }

        foreach (PartnerDefinition partner in partners)
        {
            if (partner?.TraitIds == null) continue;

            foreach (int id in partner.TraitIds)
            {
                if (known.Contains(id)) continue;

                problems.Add(
                    $"{partner.Id} 번 동료가 가리키는 특성 {id} 이(가) Trait 표에 없습니다.");
            }
        }
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '﻿', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);
}
