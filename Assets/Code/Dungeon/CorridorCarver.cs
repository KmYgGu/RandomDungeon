using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방과 방을 통로로 잇는다.
///
/// 예전에는 방 *중심* 에서 통로를 파기 시작해서, 통로가 방 안을 가로지르며
/// 바닥 타일을 덮어썼다(방 바닥의 14%). 또 기존 통로를 만나면 그 자리에서
/// 중단해 버려서, 의도한 A→B 연결이 실제로는 대부분 완성되지 않았다
/// (지하 8층 기준 통로 구간의 71%가 중단).
///
/// 지금은 방 벽에 문을 뚫고 문과 문 사이만 잇는다. 방 안은 건드리지 않는다.
/// </summary>
public class CorridorCarver
{
    private readonly DungeonTilePainter painter;
    private readonly DungeonLayout layout;

    public CorridorCarver(DungeonTilePainter painter, DungeonLayout layout)
    {
        this.painter = painter;
        this.layout = layout;
    }

    /// <summary>모든 방을 잇는다. 연결성은 RoomGraph 의 신장 트리가 보장한다.</summary>
    public void ConnectRooms(int stage)
    {
        List<RoomGraph.Edge> edges = RoomGraph.Plan(layout, RoomGraph.ExtraLoopsForStage(stage));

        foreach (RoomGraph.Edge edge in edges)
        {
            RoomInfo a = layout.Rooms[edge.A];
            RoomInfo b = layout.Rooms[edge.B];

            Door doorA = OpenDoorToward(a, b);
            Door doorB = OpenDoorToward(b, a);

            bool doorAFacesHorizontally = doorA.Outside.x != doorA.Position.x;
            CarveBetween(doorA.Outside, doorB.Outside, doorAFacesHorizontally, a, b);

            painter.PaintDoor(doorA.Position);
            painter.PaintDoor(doorB.Position);
        }
    }

    /// <summary>
    /// 상대 방을 바라보는 벽에 문을 뚫는다.
    /// 같은 자리에 이미 문이 있으면 그것을 다시 쓴다.
    /// </summary>
    private static Door OpenDoorToward(RoomInfo room, RoomInfo target)
    {
        Vector3Int outward = FacingDirection(room, target);
        Vector3Int position = DoorPositionOnSide(room, target, outward);

        foreach (Door existing in room.Doors)
        {
            if (existing.Position == position) return existing;
        }

        var door = new Door(position, position + outward);
        room.Doors.Add(door);
        return door;
    }

    /// <summary>차이가 큰 축을 골라, 그쪽 벽을 쓴다.</summary>
    private static Vector3Int FacingDirection(RoomInfo room, RoomInfo target)
    {
        Vector3Int from = room.Center;
        Vector3Int to = target.Center;

        int dx = to.x - from.x;
        int dy = to.y - from.y;

        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            return new Vector3Int(dx >= 0 ? 1 : -1, 0, 0);

        return new Vector3Int(0, dy >= 0 ? 1 : -1, 0);
    }

    /// <summary>
    /// 문의 정확한 위치. 상대 방의 중심 좌표에 최대한 맞춰서
    /// 통로가 꺾이지 않고 곧게 뻗도록 한다.
    /// </summary>
    private static Vector3Int DoorPositionOnSide(RoomInfo room, RoomInfo target, Vector3Int outward)
    {
        if (outward.x != 0)
        {
            int wallX = outward.x > 0 ? room.Bounds.xMax : room.Bounds.xMin;
            int y = Mathf.Clamp(target.Center.y, room.InteriorMinY, room.InteriorMaxY);
            return new Vector3Int(wallX, y, 0);
        }

        int wallY = outward.y > 0 ? room.Bounds.yMax : room.Bounds.yMin;
        int x = Mathf.Clamp(target.Center.x, room.InteriorMinX, room.InteriorMaxX);
        return new Vector3Int(x, wallY, 0);
    }

    /// <summary>
    /// 두 문 바깥을 잇는다.
    ///
    /// 후보는 세 가지: 중간에서 꺾는 ㄹ자와, 한쪽 끝에서 꺾는 ㄱ자 둘.
    /// 다른 방을 가장 적게 지나가는 것을 고르고, 같으면 ㄹ자를 쓴다.
    ///
    /// ㄹ자를 우선하는 이유: ㄱ자는 꺾이는 지점이 반드시 두 문 중 하나에 딱 붙어서,
    /// 그 문으로 나오자마자 벽을 만난다. 달리기가 한 칸 만에 멈춰 버린다.
    /// ㄹ자는 양쪽 문 모두에서 절반쯤 곧게 뻗는다.
    /// </summary>
    private void CarveBetween(
        Vector3Int from, Vector3Int to, bool primaryHorizontal, RoomInfo roomA, RoomInfo roomB)
    {
        List<Vector3Int> zigzag = BuildZPath(from, to, primaryHorizontal);
        List<Vector3Int> horizontalFirst = BuildLPath(from, to, true);
        List<Vector3Int> verticalFirst = BuildLPath(from, to, false);

        List<Vector3Int> path = zigzag;
        int best = ScorePath(zigzag, roomA, roomB);

        int score = ScorePath(horizontalFirst, roomA, roomB);
        if (score < best) { path = horizontalFirst; best = score; }

        if (ScorePath(verticalFirst, roomA, roomB) < best)
            path = verticalFirst;

        foreach (Vector3Int cell in path)
        {
            // 이미 바닥이나 다른 통로가 있으면 그대로 둔다(자연스럽게 합류한다).
            if (painter.IsEmpty(cell))
                painter.PaintCorridorFloor(cell);
        }
    }

    /// <summary>
    /// 주 축을 따라 절반쯤 간 뒤 옆으로 건너가고, 다시 주 축으로 마저 가는 ㄹ자 경로.
    /// 두 문 모두에서 통로가 곧게 뻗어 나온다.
    /// </summary>
    private static List<Vector3Int> BuildZPath(Vector3Int from, Vector3Int to, bool primaryHorizontal)
    {
        var path = new List<Vector3Int>();

        if (primaryHorizontal)
        {
            int midX = (from.x + to.x) / 2;
            var firstCorner = new Vector3Int(midX, from.y, 0);
            var secondCorner = new Vector3Int(midX, to.y, 0);

            AppendLine(path, from, firstCorner);
            AppendLine(path, firstCorner, secondCorner);
            AppendLine(path, secondCorner, to);
        }
        else
        {
            int midY = (from.y + to.y) / 2;
            var firstCorner = new Vector3Int(from.x, midY, 0);
            var secondCorner = new Vector3Int(to.x, midY, 0);

            AppendLine(path, from, firstCorner);
            AppendLine(path, firstCorner, secondCorner);
            AppendLine(path, secondCorner, to);
        }

        return path;
    }

    private static List<Vector3Int> BuildLPath(Vector3Int from, Vector3Int to, bool horizontalFirst)
    {
        var path = new List<Vector3Int>();

        Vector3Int corner = horizontalFirst
            ? new Vector3Int(to.x, from.y, 0)
            : new Vector3Int(from.x, to.y, 0);

        AppendLine(path, from, corner);
        AppendLine(path, corner, to);
        return path;
    }

    /// <summary>축이 하나만 다른 두 점 사이를 한 칸씩 채운다.</summary>
    private static void AppendLine(List<Vector3Int> path, Vector3Int from, Vector3Int to)
    {
        int stepX = from.x == to.x ? 0 : (to.x > from.x ? 1 : -1);
        int stepY = from.y == to.y ? 0 : (to.y > from.y ? 1 : -1);

        Vector3Int cursor = from;
        path.Add(cursor);

        while (cursor.x != to.x || cursor.y != to.y)
        {
            cursor = new Vector3Int(cursor.x + stepX, cursor.y + stepY, 0);
            path.Add(cursor);
        }
    }

    /// <summary>
    /// 경로의 나쁜 정도. 낮을수록 좋다.
    ///
    /// 다른 방을 뚫고 지나가는 것이 가장 나쁘고, 그다음이 이미 있는 통로 옆에
    /// 딱 붙어 나란히 달리는 것이다. 나란히 달리면 두 통로가 붙어 폭 2칸짜리
    /// 통로처럼 보이기 때문이다.
    /// </summary>
    private int ScorePath(List<Vector3Int> path, RoomInfo roomA, RoomInfo roomB)
    {
        return CountRoomCrossings(path, roomA, roomB) * RoomCrossingPenalty
             + CountCellsRunningAlongsidePassage(path);
    }

    /// <summary>다른 방을 지나가는 것은 나란히 달리는 것보다 훨씬 나쁘다.</summary>
    private const int RoomCrossingPenalty = 100;

    private static readonly Vector3Int[] Cardinals =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
    };

    /// <summary>새로 팔 칸 중, 이미 있는 길 바로 옆에 붙는 칸이 몇 개인가.</summary>
    private int CountCellsRunningAlongsidePassage(List<Vector3Int> path)
    {
        var onPath = new HashSet<Vector3Int>(path);
        int count = 0;

        foreach (Vector3Int cell in path)
        {
            // 이미 길이 있는 칸은 새로 파는 것이 아니므로 세지 않는다(합류는 정상이다).
            if (!painter.IsEmpty(cell)) continue;

            foreach (Vector3Int direction in Cardinals)
            {
                Vector3Int neighbour = cell + direction;
                if (onPath.Contains(neighbour)) continue;

                if (painter.IsPassage(neighbour))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    /// <summary>경로가 양 끝 방이 아닌 다른 방을 몇 칸이나 지나가는가.</summary>
    private int CountRoomCrossings(List<Vector3Int> path, RoomInfo roomA, RoomInfo roomB)
    {
        int crossings = 0;

        foreach (Vector3Int cell in path)
        {
            foreach (RoomInfo room in layout.Rooms)
            {
                if (room == roomA || room == roomB) continue;

                if (room.ContainsIncludingWall(cell.x, cell.y))
                {
                    crossings++;
                    break;
                }
            }
        }

        return crossings;
    }
}
