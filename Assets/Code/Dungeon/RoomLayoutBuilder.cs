using UnityEngine;

/// <summary>
/// 방을 서로 겹치지 않게 흩뿌리고, 그중 일부를 특별한 방으로 정한다.
/// 타일을 칠하거나 몬스터를 놓는 일은 하지 않는다.
/// </summary>
public class RoomLayoutBuilder
{
    /// <summary>
    /// 방 가장자리 사이에 최소한 남겨 둘 칸 수. 서로 벽을 맞대지 않게 한다.
    ///
    /// 예전에는 '중심 사이 맨해튼 거리 6' 을 썼는데, 큰 방 둘은 중심이 멀어도
    /// 거의 붙을 수 있고 작은 방 둘은 충분히 떨어져 있어도 거부됐다.
    /// </summary>
    private const int MinGapBetweenRooms = 2;

    /// <summary>
    /// 방 하나를 놓기 위해 자리를 몇 번까지 다시 뽑아 볼지.
    ///
    /// 예전에는 후보를 한 번만 뽑고 실패하면 그냥 넘어가서,
    /// 깊은 층일수록 요청한 방의 62% 밖에 놓이지 않았다.
    /// </summary>
    private const int AttemptsPerRoom = 30;

    /// <summary>50x50 맵에 현실적으로 들어가는 방의 상한.</summary>
    private const int MaxRooms = 18;

    private readonly int mapWidth;
    private readonly int mapHeight;
    private readonly int minRoomSize;
    private readonly int maxRoomSize;

    public RoomLayoutBuilder(int mapWidth, int mapHeight, int minRoomSize, int maxRoomSize)
    {
        this.mapWidth = mapWidth;
        this.mapHeight = mapHeight;
        this.minRoomSize = minRoomSize;
        this.maxRoomSize = maxRoomSize;
    }

    /// <summary>
    /// 층 번호에 맞는 목표 방 개수.
    ///
    /// 예전 식은 Clamp(stage * 2, 1, 15) 라서 지하 8층에서 상한에 닿았고,
    /// 그 뒤로는 30층이든 100층이든 완전히 같은 규모의 던전이 나왔다.
    /// 지금은 층마다 한 칸씩 늘어나되 맵에 들어갈 수 있는 만큼에서 멈춘다.
    /// (그 위의 난이도는 방 개수가 아니라 적·특별한 방의 밀도로 표현한다.)
    /// </summary>
    public static int TargetRoomCount(int stage)
    {
        return Mathf.Clamp(3 + stage, 3, MaxRooms);
    }

    /// <summary>방을 배치해 layout 에 채워 넣는다.</summary>
    public void Build(DungeonLayout layout, int stage)
    {
        int target = TargetRoomCount(stage);
        int budget = target * AttemptsPerRoom;

        while (layout.Rooms.Count < target && budget-- > 0)
        {
            RoomInfo candidate = CreateRandomRoom();
            if (!CanPlace(candidate, layout)) continue;

            layout.Rooms.Add(candidate);
        }

        AssignSpecialRooms(layout, stage);
    }

    private RoomInfo CreateRandomRoom()
    {
        int width = Random.Range(minRoomSize, maxRoomSize + 1);
        int height = Random.Range(minRoomSize, maxRoomSize + 1);

        // 맵 테두리에는 놓지 않는다. 문과 통로가 밖으로 빠져나갈 여유가 필요하다.
        return new RoomInfo(
            Random.Range(1, mapWidth - width - 1),
            Random.Range(1, mapHeight - height - 1),
            width,
            height);
    }

    private static bool CanPlace(RoomInfo candidate, DungeonLayout layout)
    {
        foreach (RoomInfo existing in layout.Rooms)
        {
            if (candidate.GapTo(existing) < MinGapBetweenRooms)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 보물방과 몬스터 하우스를 확률적으로 넣는다.
    /// 층이 깊어질수록 자주 나오되, 매 층 나오지는 않도록 상한을 둔다.
    /// 첫 번째 방은 플레이어가 시작할 수 있으므로 건드리지 않는다.
    /// </summary>
    private static void AssignSpecialRooms(DungeonLayout layout, int stage)
    {
        if (layout.Rooms.Count < 3) return;

        float treasureChance = Mathf.Min(0.25f + stage * 0.01f, 0.45f);
        float monsterHouseChance = Mathf.Min(0.08f + stage * 0.015f, 0.35f);

        if (Random.value < treasureChance)
            MarkRandomNormalRoom(layout, RoomKind.Treasure);

        if (Random.value < monsterHouseChance)
            MarkRandomNormalRoom(layout, RoomKind.MonsterHouse);
    }

    private static void MarkRandomNormalRoom(DungeonLayout layout, RoomKind kind)
    {
        // 0번 방은 후보에서 제외한다.
        int start = Random.Range(1, layout.Rooms.Count);

        for (int offset = 0; offset < layout.Rooms.Count - 1; offset++)
        {
            int index = 1 + (start - 1 + offset) % (layout.Rooms.Count - 1);
            if (layout.Rooms[index].Kind != RoomKind.Normal) continue;

            layout.Rooms[index].Kind = kind;
            return;
        }
    }
}
