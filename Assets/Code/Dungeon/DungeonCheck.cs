using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다 만들어진 층을 훑어 <b>잘못된 자리를 찾아 알린다.</b> 고치지는 않는다.
///
/// ── 왜 마지막에 재나 ──────────────────────────────────────────────
/// 층을 만드는 도중에는 벽이 아직 안 칠해져 있어서(FillEmptySpace 가 마지막에 한다)
/// "걸어갈 수 있는가" 를 물으면 <b>빈 칸까지 갈 수 있다고 답한다</b>
/// (TileRules.KindOfTile 이 이름 없는 타일을 바닥으로 본다). 그래서 도중에 재면
/// 온 맵이 뚫린 것으로 보여 아무것도 잡히지 않는다. 다 칠한 뒤라야 답이 맞다.
///
/// 여기서 찾는 것은 둘이다 —
///   끊어진 길 : 방에서 <b>걸어서 닿을 수 없는</b> 통로. 화면에서 동떨어져 보인다
///   막다른 끝 : 이어져는 있지만 들어갔다 되돌아 나와야 하는 자리
///
/// 둘 다 에러가 나지 않고 조용히 남는 종류라, 눈으로 찾기 전에는 모른다.
/// </summary>
public class DungeonCheck
{
    private static readonly Vector3Int[] Cardinals =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
    };

    private readonly DungeonTilePainter painter;
    private readonly DungeonLayout layout;

    public DungeonCheck(DungeonTilePainter painter, DungeonLayout layout)
    {
        this.painter = painter;
        this.layout = layout;
    }

    public void Report(int stage, int mapWidth, int mapHeight)
    {
        if (layout.Rooms.Count == 0) return;

        HashSet<Vector3Int> reached = FloodFromFirstRoom(mapWidth, mapHeight);

        var orphans = new List<Vector3Int>();
        var deadEnds = new List<Vector3Int>();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!painter.IsPassage(cell)) continue;

                if (!reached.Contains(cell)) { orphans.Add(cell); continue; }

                if (WalkableNeighbours(cell) <= 1) deadEnds.Add(cell);
            }
        }

        // 방에 못 가는 것이 가장 나쁘다. 계단이 그 안에 있으면 진행이 막힌다.
        var unreachableRooms = new List<Vector3Int>();
        foreach (RoomInfo room in layout.Rooms)
        {
            if (!reached.Contains(room.Center)) unreachableRooms.Add(room.Center);
        }

        if (unreachableRooms.Count > 0)
        {
            Debug.LogError(
                $"{stage}층: 걸어서 갈 수 없는 방이 {unreachableRooms.Count}개 있습니다. " +
                $"예: {unreachableRooms[0]}");
        }

        if (orphans.Count > 0)
        {
            Debug.LogWarning(
                $"{stage}층: 끊어진 통로 {orphans.Count}칸. 예: {orphans[0]}");
        }

        if (deadEnds.Count > 0)
        {
            // 무엇이 남았는지까지 적는다. 통로가 남았는지 문이 남았는지에 따라
            // 걷어 내기의 어느 규칙이 놓친 것인지가 갈린다.
            Vector3Int sample = deadEnds[0];
            string kind = painter.IsDoor(sample) ? "문" : "통로";

            Debug.LogWarning(
                $"{stage}층: 막다른 끝 {deadEnds.Count}칸. 예: {sample} ({kind}, " +
                $"이웃 {WalkableNeighbours(sample)}칸)");
        }
    }

    /// <summary>첫 번째 방에서 걸어서 닿는 칸을 전부 모은다.</summary>
    private HashSet<Vector3Int> FloodFromFirstRoom(int mapWidth, int mapHeight)
    {
        var reached = new HashSet<Vector3Int>();

        Vector3Int start = layout.Rooms[0].Center;
        if (!IsOpen(start)) return reached;

        var queue = new Queue<Vector3Int>();
        queue.Enqueue(start);
        reached.Add(start);

        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();

            foreach (Vector3Int direction in Cardinals)
            {
                Vector3Int next = cell + direction;

                if (next.x < 0 || next.y < 0 || next.x >= mapWidth || next.y >= mapHeight) continue;
                if (!IsOpen(next) || !reached.Add(next)) continue;

                queue.Enqueue(next);
            }
        }

        return reached;
    }

    private int WalkableNeighbours(Vector3Int cell)
    {
        int count = 0;
        foreach (Vector3Int direction in Cardinals)
        {
            if (IsOpen(cell + direction)) count++;
        }
        return count;
    }

    /// <summary>
    /// 실제로 걸어 다닐 수 있는 칸인가.
    ///
    /// 빈 칸을 따로 걸러 내는 이유는 CorridorPruner.IsOpen 과 같다 —
    /// painter.IsWalkable 은 아무것도 없는 칸에도 '갈 수 있다' 고 답한다.
    /// 여기서는 다 칠한 뒤라 빈 칸이 없어야 정상이지만, 재는 쪽과 고치는 쪽이
    /// <b>같은 기준</b>을 써야 한쪽만 통과하는 일이 없다.
    /// </summary>
    private bool IsOpen(Vector3Int cell)
        => !painter.IsEmpty(cell) && painter.IsWalkable(cell);
}
