using UnityEngine;

/// <summary>
/// 한 층에 적을 몇 마리나 놓을지 정한다.
///
/// ── 왜 상한이 필요한가 ────────────────────────────────────────────
/// 예전에는 방당 적이 <b>네 층마다 한 마리씩 끝없이</b> 늘었다. 방 수는 18에서
/// 멈추는데(RoomLayoutBuilder.MaxRooms) 적은 멈추지 않아서, 50층이면 한 층에
/// 200마리가 넘고 100층이면 450마리에 이르렀다. 몬스터 하우스는 거기에 네 배라
/// 한 방에 백 마리가 몰렸다.
///
/// 깊은 층의 난이도는 <b>수가 아니라 어떤 적이 나오는가</b>로 올린다
/// (DungeonPopulator.SetMonsterStats 주석에 적어 둔 방침이다). 몬스터 하우스가
/// 나올 확률도 층이 깊을수록 오르므로(RoomLayoutBuilder), 수까지 함께 불리면
/// 셋이 겹쳐 클리어가 불가능해진다.
///
/// ── 상한 셋 ───────────────────────────────────────────────────────
///   방당      기본 + 열 층마다 하나, <b>둘까지만</b> 더 붙는다
///   층 전체   일반 방에 놓는 적은 다 합쳐 스물까지
///   몬하      한 방에 열넷까지. 층 상한과 따로 센다
///
/// 몬스터 하우스를 층 상한에서 빼 둔 것은, 방을 채우는 차례에 따라 하우스가
/// 남은 몫을 못 받고 텅 비는 일이 없게 하려는 것이다. 하우스는 층에 하나뿐이라
/// (RoomLayoutBuilder.AssignSpecialRooms) 따로 세도 총합이 흔들리지 않는다.
///
/// 숫자만 다루므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class EnemyBudget
{
    /// <summary>몇 층마다 방당 적이 한 마리씩 늘어나는가.</summary>
    public const int StagesPerExtra = 10;

    /// <summary>층이 아무리 깊어도 방당 이만큼까지만 더 붙는다.</summary>
    public const int MaxExtraPerRoom = 2;

    /// <summary>일반 방에 놓는 적의 층 전체 상한.</summary>
    public const int MaxPerFloor = 20;

    /// <summary>몬스터 하우스의 적 배수.</summary>
    public const int MonsterHouseMultiplier = 4;

    /// <summary>몬스터 하우스 한 방의 상한.</summary>
    public const int MaxInMonsterHouse = 14;

    /// <summary>층이 깊어져 더 붙는 수. 상한에 걸린다.</summary>
    public static int ExtraForStage(int stage)
    {
        if (stage <= 0) return 0;
        return Mathf.Min(stage / StagesPerExtra, MaxExtraPerRoom);
    }

    /// <summary>일반 방 하나에 놓고 싶은 수.</summary>
    public static int ForRoom(int baseCount, int stage)
        => Mathf.Max(0, baseCount) + ExtraForStage(stage);

    /// <summary>
    /// 몬스터 하우스 한 방에 놓을 수.
    ///
    /// 기본이 0 이어도 하우스는 비어 있으면 안 되므로 배수만큼은 놓는다 —
    /// 인스펙터의 방당 최소가 0 이라(실제로 0 이다) 그대로 두면 텅 빈 하우스가 나온다.
    /// </summary>
    public static int ForMonsterHouse(int baseCount, int stage)
    {
        int wanted = ForRoom(baseCount, stage) * MonsterHouseMultiplier;
        if (wanted == 0) wanted = MonsterHouseMultiplier;

        return Mathf.Min(wanted, MaxInMonsterHouse);
    }

    /// <summary>
    /// 층 상한에 걸러 실제로 놓을 수. 이미 놓은 만큼을 빼고 남은 몫을 준다.
    /// </summary>
    public static int AllowedThisFloor(int wanted, int spawnedSoFar)
    {
        int room = MaxPerFloor - Mathf.Max(0, spawnedSoFar);
        if (room <= 0) return 0;

        return Mathf.Clamp(wanted, 0, room);
    }
}
