using UnityEngine;

/// <summary>
/// 폭이 2칸이 되어 버린 통로를 다시 1칸으로 깎아 낸다.
///
/// 통로는 서로 다른 두 방 쌍을 잇느라 각각 따로 파인다. 그래서 두 통로가
/// 우연히 바로 옆에 나란히 놓이면, 화면에서는 폭 2칸짜리 통로 하나로 보인다.
/// 경로를 고를 때 나란히 달리는 것을 피하도록 점수를 매기고 있지만,
/// 세 후보 경로가 전부 나쁠 때는 어쩔 수 없이 붙는 경우가 남는다.
///
/// 여기서는 그렇게 남은 것을 마지막에 정리한다. 판정 기준은 단순하다.
/// 폭 1칸짜리 통로는 절대로 통로 칸만으로 이루어진 2x2 블록을 만들 수 없다.
/// (ㄱ자 모서리조차 2x2 중 세 칸만 채운다.)
/// 그런 블록이 보이면 한 칸을 지우되, 지운 뒤에도 모든 방이 서로 오갈 수 있을
/// 때만 실제로 지운다. 그래서 던전이 끊어질 위험이 없다.
/// </summary>
public class CorridorThinner
{
    /// <summary>안전장치. 한 층에서 이보다 많이 깎지는 않는다.</summary>
    private const int MaxRemovals = 64;

    private readonly DungeonTilePainter painter;
    private readonly DungeonLayout layout;
    private readonly ChaseMap reachability = new ChaseMap();

    public CorridorThinner(DungeonTilePainter painter, DungeonLayout layout)
    {
        this.painter = painter;
        this.layout = layout;
    }

    /// <summary>깎아 낸 칸 수를 돌려준다.</summary>
    public int Thin(IWalkableGrid grid, int mapWidth, int mapHeight)
    {
        if (layout.Rooms.Count < 2) return 0;

        int removed = 0;
        while (removed < MaxRemovals && TryRemoveOneWideningCell(grid, mapWidth, mapHeight))
            removed++;

        return removed;
    }

    /// <summary>2x2 통로 블록을 하나 찾아 그중 지울 수 있는 칸을 지운다.</summary>
    private bool TryRemoveOneWideningCell(IWalkableGrid grid, int mapWidth, int mapHeight)
    {
        for (int x = 0; x < mapWidth - 1; x++)
        {
            for (int y = 0; y < mapHeight - 1; y++)
            {
                if (!IsCorridorBlock(x, y)) continue;
                if (TryRemoveFromBlock(grid, x, y)) return true;
            }
        }
        return false;
    }

    private bool IsCorridorBlock(int x, int y)
    {
        return painter.IsCorridor(new Vector3Int(x, y, 0))
            && painter.IsCorridor(new Vector3Int(x + 1, y, 0))
            && painter.IsCorridor(new Vector3Int(x, y + 1, 0))
            && painter.IsCorridor(new Vector3Int(x + 1, y + 1, 0));
    }

    private bool TryRemoveFromBlock(IWalkableGrid grid, int x, int y)
    {
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                var cell = new Vector3Int(x + dx, y + dy, 0);

                // 문 바로 앞은 건드리지 않는다. 방이 여전히 이어져 있더라도
                // 어디로도 통하지 않는 문이 남아 보기 이상해진다.
                if (IsNextToDoor(cell)) continue;

                painter.Erase(cell);

                if (AllRoomsStillConnected(grid)) return true;

                // 끊어졌으니 되돌린다.
                painter.PaintCorridorFloor(cell);
            }
        }
        return false;
    }

    private bool IsNextToDoor(Vector3Int cell)
    {
        return painter.IsDoor(cell + new Vector3Int(1, 0, 0))
            || painter.IsDoor(cell + new Vector3Int(-1, 0, 0))
            || painter.IsDoor(cell + new Vector3Int(0, 1, 0))
            || painter.IsDoor(cell + new Vector3Int(0, -1, 0));
    }

    /// <summary>첫 번째 방에서 출발해 나머지 방에 모두 닿을 수 있는가.</summary>
    private bool AllRoomsStillConnected(IWalkableGrid grid)
    {
        reachability.Rebuild(grid, GridUtil.CellCenter(layout.Rooms[0].Center));
        if (!reachability.IsReady) return false;

        foreach (RoomInfo room in layout.Rooms)
        {
            if (!reachability.TryGetDistance(GridUtil.CellCenter(room.Center), out _))
                return false;
        }
        return true;
    }
}
