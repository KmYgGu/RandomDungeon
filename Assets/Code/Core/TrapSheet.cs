using System.Collections.Generic;

/// <summary>
/// 함정이 작동할 때 캐릭터가 취하는 몸짓. 코드가 아는 것만 시트에서 고를 수 있다.
///
/// 스프라이트와 회전은 코드가 하는 일이라 시트가 담을 수 없다. 그래서 상태이상의
/// ConditionEffect 와 같은 방식으로, 코드가 아는 목록을 시트가 골라 쓰게 했다.
/// </summary>
public enum TrapAnimation
{
    /// <summary>아무 몸짓도 없다.</summary>
    None,

    /// <summary>공격을 받는 모습. 스프라이트가 아직 없다.</summary>
    Hit,

    /// <summary>
    /// 지금 애니메이션은 그대로 두고 8방향으로 자연스럽게 한 바퀴 돈다.
    /// 워프처럼 "방향을 잃었다" 를 보여 줄 때 쓴다.
    /// </summary>
    Spin,

    /// <summary>강제로 돌려진 것처럼 빠르게 두 바퀴 돈다. 혼란에 쓴다.</summary>
    SpinFast,

    /// <summary>잠든 모습. 스프라이트가 아직 없다.</summary>
    Sleep,
}

/// <summary>
/// 코드가 직접 가리키는 함정 이름. 시트의 id2 열과 같아야 한다.
///
/// 여기 없는 함정은 문구만 뜨고 아무 일도 하지 않는다.
/// 효과를 만들면 이 목록에 이름을 더하고 GameManager.ApplyTrapEffect 에 잇는다.
/// </summary>
public static class TrapIds
{
    /// <summary>강제로 다음 층으로. 방어력 무시 피해가 따른다.</summary>
    public const string Hole = "hole";

    /// <summary>무작위 방으로 옮긴다.</summary>
    public const string Warp = "warp";

    /// <summary>보고 있는 반대 방향으로 여덟 칸 날려 보낸다.</summary>
    public const string Blow = "blow";

    /// <summary>독침. 힘이 깎인다. 상태이상이 아니라 그 자리에서 끝나는 피해다.</summary>
    public const string Dart = "dart";

    /// <summary>가스. 잠든다.</summary>
    public const string Gas = "gas";

    /// <summary>회전. 혼란에 빠진다.</summary>
    public const string Spin = "spin";

    /// <summary>봉인. 특수 능력이 나가지 않는다.</summary>
    public const string Restraint = "restraint";

    /// <summary>끈적끈적. 가방의 아이템 하나가 손에서 떨어지지 않게 된다.</summary>
    public const string Sticky = "sticky";

    // 아직 만들지 않은 것 — soil(오물) 하나뿐이다.
}

/// <summary>시트 한 줄. 함정 하나의 정의.</summary>
public class TrapDefinition
{
    /// <summary>코드가 가리키는 이름(시트의 id2 열).</summary>
    public string Id;

    /// <summary>표 번호(시트의 id 열).</summary>
    public int Number;

    /// <summary>플레이어에게 보이는 이름("독침"). 밟았다는 알림에 쓰인다.</summary>
    public string Name;

    /// <summary>
    /// 작동했을 때 메시지 창에 그대로 띄우는 문구("어디선가 독침이 날라왔다!").
    ///
    /// 함정 하나가 뜨는 글은 셋이다 —
    ///   1. "…을(를) 밟았다!"        MessageCatalog 가 Name 으로 만든다
    ///   2. 이 문구                   시트가 정한다
    ///   3. "힘이 감소했다."          효과를 실제로 실행한 쪽이 만든다
    /// 2번만 시트에 있는 이유는, 1번과 3번은 무슨 일이 일어났는지에 따라
    /// 글이 달라지는데 2번은 함정마다 고정이기 때문이다.
    /// </summary>
    public string Message;

    /// <summary>작동할 때의 몸짓.</summary>
    public TrapAnimation Animation;

    /// <summary>
    /// 무슨 일이 일어나야 하는지 적어 둔 설정("힘이 감소").
    ///
    /// 사람이 읽는 메모다. 코드가 이 글자를 보고 갈래를 나누지 않는다 —
    /// 그렇게 하면 메모의 말투를 다듬는 순간 게임이 조용히 바뀐다.
    /// 효과를 만들 때는 Id 로 갈래를 나누고, 이 글을 명세로 삼는다.
    /// </summary>
    public string Note;
}

/// <summary>
/// 엑셀에서 읽어 온 함정 줄을 게임이 쓰는 모습으로 옮겨 담는다.
///
/// ConditionSheet 와 같은 규칙을 따른다. 알아볼 수 없는 값이 있는 줄은 그것만
/// 기본값으로 두고, 무엇이 이상한지 problems 에 적어 둔다. 한 줄이 잘못됐다고
/// 나머지까지 버리면 표를 고치기가 더 어려워진다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class TrapSheet
{
    public static List<TrapDefinition> FromEntities(
        IEnumerable<TrapEntity> rows, List<string> problems = null)
    {
        var result = new List<TrapDefinition>();
        if (rows == null) return result;

        foreach (TrapEntity row in rows)
        {
            if (row == null) continue;

            string id = Clean(row.id2);
            if (id.Length == 0) continue;

            string message = Clean(row.explanation);

            // 문구가 비어 있으면 함정이 작동해도 아무 말이 없다. 밟은 사람은
            // 무슨 일이 일어났는지 모른 채 결과만 맞게 되므로 반드시 알린다.
            if (message.Length == 0)
                problems?.Add($"'{id}' 에 띄울 문구(explanation)가 비어 있습니다.");

            result.Add(new TrapDefinition
            {
                Id = id,
                Number = row.id,
                Name = Clean(row.name),
                Message = message,
                Animation = ParseAnimation(row.charani, id, problems),
                Note = Clean(row.note),
            });
        }

        return result;
    }

    /// <summary>칸 끝에 남은 탭·공백·줄바꿈을 털어 낸다.</summary>
    private static readonly char[] Junk =
        { '﻿', ' ', '\t', '\r', '\n' };

    private static string Clean(string cell)
        => cell == null ? string.Empty : cell.Trim(Junk);

    public static TrapAnimation ParseAnimation(
        string cell, string id, List<string> problems = null)
    {
        switch (Clean(cell))
        {
            case "피격": return TrapAnimation.Hit;
            case "빙글": return TrapAnimation.Spin;
            case "빙글빙글": return TrapAnimation.SpinFast;
            case "수면": return TrapAnimation.Sleep;
            case "없음": return TrapAnimation.None;
            default:
                problems?.Add($"'{id}' 의 몸짓 '{Clean(cell)}' 를 모릅니다. '없음' 으로 봅니다.");
                return TrapAnimation.None;
        }
    }
}
