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
    /// <summary>
    /// 안전장치. 한 층에서 이보다 많이 깎지는 않는다.
    ///
    /// 한때 64였다. 깊은 층은 고리가 방 수만큼 붙어(RoomGraph.ExtraLoopsForStage)
    /// 통로가 훨씬 많고, 그만큼 폭 2칸이 되는 곳도 많아 64로는 모자랐다.
    ///
    /// 한 칸 깎을 때마다 맵을 한 번 훑고 길찾기를 다시 돌리므로 공짜는 아니지만,
    /// 층을 만들 때 한 번 도는 일이라 이 정도는 눈에 띄지 않는다.
    /// </summary>
    public const int MaxRemovals = 256;

    /// <summary>
    /// 한도에 걸려 <b>깎다 만 채로</b> 끝났는가.
    ///
    /// 걸렸다면 폭 2칸짜리 통로가 그대로 남아 있다는 뜻이다 — 화면에서 통로가
    /// 넓게 뭉개져 보이는 원인이 된다. 에러가 아니라 조용히 남는 것이라,
    /// 부르는 쪽이 이 값을 보고 알린다(RandomDung).
    /// </summary>
    public bool StoppedAtLimit { get; private set; }

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

        // 한도까지 깎고도 아직 폭 2칸이 남아 있으면 깎다 만 것이다.
        // <b>세기만 한다</b> — TryRemoveOneWideningCell 로 확인하면 한 칸 더 지워 버린다.
        StoppedAtLimit = removed >= MaxRemovals && HasWideningBlock(mapWidth, mapHeight);

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

    /// <summary>폭 2칸짜리가 아직 남아 있는가. 아무것도 지우지 않는다.</summary>
    public bool HasWideningBlock(int mapWidth, int mapHeight)
    {
        for (int x = 0; x < mapWidth - 1; x++)
        {
            for (int y = 0; y < mapHeight - 1; y++)
            {
                if (IsCorridorBlock(x, y)) return true;
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

    /// <summary>
    /// 이 블록에서 지울 수 있는 칸을 하나 지운다.
    ///
    /// ── 토막이 생겨도 여기서는 신경 쓰지 않는다 ──────────────────────
    /// 한때 "지운 자리 둘레에 막다른 끝이 생기면 되돌린다" 를 두었다. 그랬더니
    /// 네 칸 다 못 지우는 블록이 늘어 <b>폭 2칸이 그대로 남았다</b>(실제로 한 층에서
    /// 깎은 칸이 0개인데 2칸짜리가 남았다).
    ///
    /// 지금은 층을 다 만든 뒤 CorridorPruner 가 막다른 끝을 걷어 내므로, 여기서
    /// 미리 몸을 사릴 이유가 없다. 각자 한 가지만 한다 — 여기는 폭을 줄이고,
    /// 걷어 내기는 끝을 정리한다.
    ///
    /// ── 문 옆칸도 이제 지운다 ────────────────────────────────────────
    /// "지우면 어디로도 통하지 않는 문이 남는다" 며 문 옆칸을 비켜 갔다. 그런데
    /// 깊은 층은 고리가 많아 문도 많고, 그래서 <b>문 옆이라는 이유로 못 깎는 블록이
    /// 대부분이 되어</b> 폭 2칸이 그대로 남았다(96층에서 여섯 칸 깎고 멈췄다).
    ///
    /// 통하지 않게 된 문은 걷어 내기가 치운다 — 이웃이 하나뿐인 문은 그쪽이
    /// 지우는 대상이다(CorridorPruner). 방이 떨어져 나가지 않는 것은 여기서
    /// 여전히 확인하므로(AllRoomsStillConnected), 둘을 합치면 빈틈이 없다.
    /// </summary>
    private bool TryRemoveFromBlock(IWalkableGrid grid, int x, int y)
    {
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                var cell = new Vector3Int(x + dx, y + dy, 0);

                painter.Erase(cell);

                if (AllRoomsStillConnected(grid)) return true;

                // 끊어졌으니 되돌린다.
                painter.PaintCorridorFloor(cell);
            }
        }
        return false;
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
