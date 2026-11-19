using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이 적이 아이템을 얼마나 잘 떨어뜨리는가. 코드가 아는 것만 시트에서 고를 수 있다.
///
/// 시트의 rate 열이 이것을 고른다. 실제 확률(몇 %)은 코드가 정하는데,
/// 층마다 사정이 달라서 숫자 자체를 시트에 박아 두면 층 구성을 바꿀 때마다
/// 표의 모든 줄을 다시 만져야 하기 때문이다. 시트는 "잘 떨군다/잘 안 떨군다" 만 정한다.
///
/// ── 한때 출현 빈도였다 ────────────────────────────────────────────
/// 이 열은 오래 뜻이 정해지지 않은 채였고(ExcelData.MonsterEntity.rate 의 옛 주석),
/// 코드는 그동안 출현 빈도로 읽고 있었다. 다만 <b>실제로 출현에 쓰인 적은 없다</b> —
/// 값을 담아 두고 인스펙터에 보여 주기만 했다. 그래서 뜻을 드롭 확률로 옮겨도
/// 게임 동작이 달라지는 곳이 없다.
///
/// EnemyDropPlan이 상/중/하를 30%/15%/5%로 변환하고 모든 처치 경로가 함께 쓴다.
/// 실제 아이템 종류 선택은 item 시트의 droprate 가중치를 따른다.
/// </summary>
public enum ItemDropRate
{
    /// <summary>잘 떨어뜨린다.</summary>
    High,
    /// <summary>보통.</summary>
    Medium,
    /// <summary>잘 떨어뜨리지 않는다.</summary>
    Low,
}

/// <summary>시트 한 줄. 적 하나의 정의.</summary>
public class MonsterDefinition
{
    /// <summary>표 번호. 코드와 프리팹이 이 번호로 서로를 찾는다.</summary>
    public int Id;

    /// <summary>플레이어에게 보이는 이름("걸어다니는 시체").</summary>
    public string Name;

    public int Hp;
    public int Attack;
    public int Defense;

    /// <summary>쓰러뜨렸을 때 주는 경험치.</summary>
    public int Exp;

    /// <summary>
    /// 이 적이 치명타를 내는가.
    ///
    /// <b>확률은 여기 없다.</b> 얼마나 자주 나는지는 코드가 한 곳에서 정한다
    /// (Player.IncomingCriticalPercent). 표는 "내는 적인가" 만 정한다.
    ///
    /// 모든 적이 낸다면 맞는 쪽에서는 피해 폭이 넓은 것으로만 보인다.
    /// 내는 적이 정해져 있어야 "저 녀석은 조심해야 한다" 가 성립한다.
    /// </summary>
    public bool DealsCritical;

    /// <summary>아이템 드롭 확률.</summary>
    public ItemDropRate Rate;

    /// <summary>갈래("몬스터"). 도감이 종족으로 보여 준다.</summary>
    public string Category;

    /// <summary>도감에 띄울 글.</summary>
    public string Description;

    /// <summary>사람이 읽는 설정 메모. 게임에 뜨지 않는다.</summary>
    public string Note;
}

/// <summary>
/// 엑셀에서 읽어 온 적 줄을 게임이 쓰는 모습으로 옮겨 담는다.
///
/// ConditionSheet · TrapSheet 과 같은 규칙을 따른다. 알아볼 수 없는 값이 있는
/// 줄은 그것만 기본값으로 두고 무엇이 이상한지 problems 에 적어 둔다.
///
/// ── 적만 id 가 번호인 이유 ────────────────────────────────────────
/// 상태이상과 함정은 코드가 이름으로 가리킨다(`hungry` 를 굶주림 판정이 직접
/// 찾는다). 적은 코드가 특정 마리를 가리킬 일이 없고 — 어느 적이든 같은 규칙으로
/// 움직인다 — 프리팹이 자기 번호만 알면 된다. 그래서 시트에도 id2 가 없다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class MonsterSheet
{
    public static List<MonsterDefinition> FromEntities(
        IEnumerable<MonsterEntity> rows, List<string> problems = null)
    {
        var result = new List<MonsterDefinition>();
        if (rows == null) return result;

        foreach (MonsterEntity row in rows)
        {
            if (row == null) continue;

            // 번호가 없는 줄은 프리팹이 찾을 길이 없으므로 버린다.
            if (row.id == 0) continue;

            string name = Clean(row.name);

            // 이름이 없으면 쓰러뜨렸을 때 "를 쓰러뜨렸다." 로만 뜬다.
            if (name.Length == 0)
                problems?.Add($"{row.id} 번의 이름(name)이 비어 있습니다.");

            result.Add(new MonsterDefinition
            {
                Id = row.id,
                Name = name,
                Hp = row.hp,
                Attack = row.atk,
                Defense = row.def,
                Exp = row.exp,
                DealsCritical = ParseCritical(row.critical, row.id, problems),
                Rate = ParseRate(row.rate, row.id, problems),
                Category = Clean(row.category),
                Description = Clean(row.explanation),
                Note = Clean(row.note),
            });
        }

        return result;
    }

    /// <summary>
    /// 치명타 칸을 읽는다. <b>1 이면 낸다, 0(또는 빈칸)이면 안 낸다.</b>
    ///
    /// 한때 이 열이 확률(%)이었다. 켜고 끄는 값으로 바꾸면서 위험한 구간이
    /// 생겼다 — 예전 표의 감각으로 <c>1</c> 이라고 적으면 "1% 확률" 이 아니라
    /// "낸다" 가 되고, <c>20</c> 이라고 적으면 20% 가 아니라 그냥 <b>모르는 값</b>이다.
    /// 둘 다 에러가 나지 않으므로, 1·0 이 아닌 값은 반드시 알려 준다.
    /// </summary>
    public static bool ParseCritical(int cell, int id, List<string> problems = null)
    {
        if (cell == 0) return false;
        if (cell == 1) return true;

        problems?.Add(
            $"{id} 번의 critical 이 {cell} 입니다. 이 열은 확률이 아니라 1(낸다)/0(안 낸다) 입니다. " +
            "'낸다' 로 봅니다.");

        return true;
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '﻿', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);

    public static ItemDropRate ParseRate(string cell, int id, List<string> problems = null)
    {
        switch (Clean(cell))
        {
            case "상": return ItemDropRate.High;
            case "중": return ItemDropRate.Medium;
            case "하": return ItemDropRate.Low;
            default:
                problems?.Add($"{id} 번의 드롭 확률 '{Clean(cell)}' 를 모릅니다. '중' 으로 봅니다.");
                return ItemDropRate.Medium;
        }
    }

    /// <summary>
    /// 화면에 보여 줄 말. 읽는 곳(ParseRate)과 나란히 두어 둘이 어긋나지 않게 한다.
    ///
    /// 시트에 적는 글자와 같은 글자를 쓴다. 도감에서 '상' 을 본 사람이 시트를 열면
    /// 같은 칸을 바로 찾을 수 있어야 하기 때문이다.
    /// </summary>
    public static string NameOf(ItemDropRate rate)
    {
        switch (rate)
        {
            case ItemDropRate.High: return "상";
            case ItemDropRate.Low: return "하";
            default: return "중";
        }
    }
}
