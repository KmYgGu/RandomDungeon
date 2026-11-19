using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방 밖의 ㄱ자·ㄹ자·계단·우회 통로를 섞는다. 방 벽과 맵 경계는 침범하지 않는다.
/// 후보 개수나 최단 거리만으로 고르면 ㄹ자에 편중되므로, 가능한 모양부터 추첨한다.
/// 방 때문에 모든 모양이 막히면 빈 공간에서 길을 찾고, 그래도 실패하면 층을 재생성한다.
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
    public bool ConnectRooms(int stage, int? extraLoops = null, int mapWidth = 50, int mapHeight = 50)
    {
        var random = new System.Random(Random.Range(0, int.MaxValue));
        List<RoomGraph.Edge> edges = RoomGraph.Plan(layout, extraLoops ?? RoomGraph.ExtraLoopsForStage(stage), random);
        PrepareEntrances(layout, mapWidth, mapHeight, random);
        CorridorStyle? previous = null;

        foreach (RoomGraph.Edge edge in edges)
        {
            RoomInfo a = layout.Rooms[edge.A];
            RoomInfo b = layout.Rooms[edge.B];

            var route = PlanConnection(layout, a, b, mapWidth, mapHeight, random,
                cell => !painter.IsEmpty(cell) && painter.IsWalkable(cell), CountCellsRunningAlongsidePassage, previous, out var doorA, out var doorB);
            if (route == null) return false;
            previous = route.Style;
            foreach (var cell in route.Cells)
                if (painter.IsEmpty(cell)) painter.PaintCorridorFloor(cell);

            painter.PaintDoor(doorA.Position);
            painter.PaintDoor(doorB.Position);
        }
        foreach (var room in layout.Rooms)
            room.Doors.RemoveAll(door => !painter.IsPassage(door.Position));
        return true;
    }

    /// <summary>입구 보호와 재시도를 포함한 실제 연결 선택. 타일맵 없이도 회귀 검사한다.</summary>
    public static void PrepareEntrances(DungeonLayout layout, int width, int height, System.Random random)
    {
        // 먼저 모든 방의 입구 자리를 확보한다. 다른 통로가 지나간 뒤에는 입구를 낼 자리가 사라질 수 있다.
        foreach (var room in layout.Rooms)
        {
            if (room.Doors.Count > 0) continue;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var door = VariedDoor(room, room, random, width, height);
                var ahead = door.Outside + (door.Outside - door.Position);
                var blocked = EntranceRules.BlockedCells(layout);
                if (ahead.x > 0 && ahead.y > 0 && ahead.x < width - 1 && ahead.y < height - 1
                    && !blocked.Contains(ahead) && !blocked.Contains(door.Outside)) break;
                room.Doors.Clear();
            }
        }
    }

    public static CorridorRouting.Route PlanConnection(DungeonLayout layout, RoomInfo a, RoomInfo b,
        int mapWidth, int mapHeight, System.Random random, System.Func<Vector3Int, bool> isOpen,
        System.Func<List<Vector3Int>, int> penalty, CorridorStyle? previous, out Door doorA, out Door doorB)
    {
        doorA = doorB = default;
        CorridorRouting.Route route = null;
        // 새 입구 옆에 기존 통로가 있으면 그 길을 지우지 않고 다른 입구를 찾는다.
        for (int attempt = 0; attempt < 128 && route == null; attempt++)
        {
            int countA = a.Doors.Count, countB = b.Doors.Count;
            doorA = attempt >= 4 && countA > 0 && random.Next(2) == 0 ? a.Doors[random.Next(countA)] : VariedDoor(a, b, random, mapWidth, mapHeight);
            doorB = attempt >= 4 && countB > 0 && random.Next(2) == 0 ? b.Doors[random.Next(countB)] : VariedDoor(b, a, random, mapWidth, mapHeight);
            var blocked = EntranceRules.BlockedCells(layout);
            if (EntranceRules.IsNarrow(doorA, isOpen) && EntranceRules.IsNarrow(doorB, isOpen))
                route = CorridorRouting.Plan(doorA.Outside, doorB.Outside, doorA.Outside.x != doorA.Position.x,
                    mapWidth, mapHeight, blocked.Contains,
                    penalty, random, previous);
            // 혼잡하면 이미 열린 방·문을 경유한다. 새로운 벽을 뚫거나 입구를 넓히지는 않는다.
            if (route == null && attempt >= 8 && EntranceRules.IsNarrow(doorA, isOpen) && EntranceRules.IsNarrow(doorB, isOpen))
                route = CorridorRouting.Plan(doorA.Outside, doorB.Outside, doorA.Outside.x != doorA.Position.x,
                    mapWidth, mapHeight, cell => blocked.Contains(cell) && !isOpen(cell),
                    penalty, random, previous);
            if (route == null)
            {
                a.Doors.RemoveRange(countA, a.Doors.Count - countA);
                b.Doors.RemoveRange(countB, b.Doors.Count - countB);
            }
        }
        return route;
    }

    /// <summary>문을 항상 상대 중심에 맞추면 복도가 직선·두 꺾임으로 고정되므로 벽 안에서 위치도 섞는다.</summary>
    private static Door VariedDoor(RoomInfo room, RoomInfo target, System.Random random, int width, int height)
    {
        var outward = Cardinals[random.Next(Cardinals.Length)];
        var position = DoorPositionOnSide(room, target, outward);
        if (random.Next(4) != 0)
        {
            if (outward.x != 0) position.y = random.Next(room.InteriorMinY, room.InteriorMaxY + 1);
            else position.x = random.Next(room.InteriorMinX, room.InteriorMaxX + 1);
        }
        // 가장자리 방의 옆문이 맵 테두리를 향하면 통로를 낼 공간이 없다.
        bool Inside(Vector3Int p) => p.x > 0 && p.y > 0 && p.x < width - 1 && p.y < height - 1;
        if (!Inside(position + outward))
        {
            foreach (var direction in Cardinals)
            {
                var candidate = DoorPositionOnSide(room, target, direction);
                if (!Inside(candidate + direction)) continue;
                outward = direction;
                position = candidate;
                break;
            }
        }
        foreach (var door in room.Doors)
        {
            // 같은 벽에 가까운 문을 새로 뚫지 않고 기존의 한 칸 입구를 공유한다.
            if (door.Outside - door.Position == outward
                && Mathf.Abs(door.Position.x - position.x) + Mathf.Abs(door.Position.y - position.y) <= 2)
                return door;
        }
        var result = new Door(position, position + outward);
        room.Doors.Add(result);
        return result;
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

}
