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
///
/// ── 통로 수를 늘리지 않는다 ───────────────────────────────────────
/// 한때 옆으로 크게 벗어났다 돌아오는 우회 통로와, 통로끼리 잇는 지름길을
/// 넣어 봤다. 층이 훨씬 어지러워지고 <b>진행이 불가능한 층까지 나왔다.</b>
///
/// 통로가 늘면 우연히 나란히 붙어 폭 2칸이 되는 곳도 함께 는다. 그것을 1칸으로
/// 깎는 CorridorThinner 는 한 층에 64칸까지만 깎으므로(MaxRemovals), 넘어서면
/// <b>깎다 만 채로 끝난다.</b> 그래서 통로가 넓게 뭉개져 보였고, 깊은 층일수록
/// (통로가 더 많아) 심했다. 우회 통로가 방 벽을 뚫어 방 모양이 무너지는 문제도 있었다.
///
/// 그래서 <b>모양만 바꾸고 수는 늘리지 않는</b> 쪽으로 되돌렸다. 꺾는 자리를
/// 무작위로 잡으면 통로 하나의 길이도 지나는 칸 수도 예전과 같으면서 모양만 달라진다.
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

    /// <summary>꺾는 자리를 몇 군데나 굴려 보는가.</summary>
    private const int RandomCandidates = 6;

    /// <summary>
    /// 두 문 바깥을 잇는다.
    ///
    /// 후보는 ㄹ자와 ㄱ자 둘, 그리고 <b>꺾는 자리를 무작위로 잡은 ㄹ자</b> 여럿이다.
    /// 다른 방을 가장 적게 지나가는 것을 고르고, 점수가 같으면 그중 아무거나 고른다.
    ///
    /// ── 왜 ㄹ자를 여럿 굴리나 ────────────────────────────────────────
    /// 예전에는 후보가 셋뿐이었고 같은 점수면 늘 ㄹ자를 골랐다. 방 배치만 정해지면
    /// 통로 모양이 그대로 따라 나와서, 층을 몇 번 보면 눈에 익었다.
    ///
    /// 꺾는 자리만 옮기면 <b>지나는 칸 수는 그대로</b>다(가로로 간 만큼 세로로도
    /// 가야 하는 것은 변하지 않는다). 통로가 길어지지도 늘어나지도 않으므로
    /// 폭 2칸이 되는 곳도 예전만큼만 생기고, 모양만 달라진다.
    ///
    /// ㄱ자는 꺾이는 지점이 두 문 중 하나에 딱 붙어서 그 문으로 나오자마자 벽을
    /// 만난다(달리기가 한 칸 만에 멈춘다). 그래서 ㄹ자 쪽이 여럿이다.
    /// </summary>
    private void CarveBetween(
        Vector3Int from, Vector3Int to, bool primaryHorizontal, RoomInfo roomA, RoomInfo roomB)
    {
        var candidates = new List<List<Vector3Int>>
        {
            CorridorShapes.Z(from, to, primaryHorizontal, Midpoint(from, to, primaryHorizontal)),
            CorridorShapes.L(from, to, true),
            CorridorShapes.L(from, to, false),
        };

        for (int i = 0; i < RandomCandidates; i++)
            candidates.Add(RandomZ(from, to, primaryHorizontal));

        List<Vector3Int> path = PickBest(candidates, roomA, roomB);

        foreach (Vector3Int cell in path)
        {
            // 이미 바닥이나 다른 통로가 있으면 그대로 둔다(자연스럽게 합류한다).
            if (!painter.IsEmpty(cell)) continue;

            // 방의 벽 테두리를 지나가는 자리면 통로가 아니라 <b>문</b>을 놓는다.
            //
            // ── 왜 이게 필요한가 ──────────────────────────────────────
            // 방의 벽은 이 시점에 아직 안 칠해져 있다(FillEmptySpace 가 나중에 한다).
            // 그래서 통로가 그 위를 지나가면 통로 바닥이 칠해지고, 방 안쪽 바닥과
            // 곧바로 붙어 <b>벽이 사라진다.</b> 화면에서는 방에 넓은 구간이 달라붙은
            // 것처럼 보이는데, 통로 타일과 방 타일이 섞여 있어서 폭 2칸 깎기
            // (CorridorThinner)에도 걸리지 않고 그대로 남는다.
            //
            // 문을 놓으면 지나갈 수 있는 것은 같고 <b>벽에 낸 출입구</b>로 보인다.
            // 통로가 방을 지나가는 일 자체는 점수로 이미 피하고 있으므로
            // (ScorePath 의 RoomCrossingPenalty) 어쩔 수 없이 지나갈 때만 이렇게 된다.
            if (IsOnRoomWall(cell)) painter.PaintDoor(cell);
            else painter.PaintCorridorFloor(cell);
        }
    }

    /// <summary>어느 방의 벽 테두리에 놓인 칸인가. 방 안쪽은 아니다.</summary>
    private bool IsOnRoomWall(Vector3Int cell)
    {
        foreach (RoomInfo room in layout.Rooms)
        {
            if (room.ContainsIncludingWall(cell.x, cell.y) && !room.ContainsInterior(cell.x, cell.y))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 가장 점수가 낮은 것. 여럿이면 그중 아무거나 고른다.
    ///
    /// 같은 점수에서 늘 앞의 것을 고르면 굴린 후보가 거의 쓰이지 않는다 —
    /// 대개 여러 모양이 나란히 0 점이기 때문이다.
    /// </summary>
    private List<Vector3Int> PickBest(
        List<List<Vector3Int>> candidates, RoomInfo roomA, RoomInfo roomB)
    {
        var best = new List<List<Vector3Int>>();
        int bestScore = int.MaxValue;

        foreach (List<Vector3Int> candidate in candidates)
        {
            int score = ScorePath(candidate, roomA, roomB);

            if (score < bestScore)
            {
                bestScore = score;
                best.Clear();
                best.Add(candidate);
            }
            else if (score == bestScore)
            {
                best.Add(candidate);
            }
        }

        return best[Random.Range(0, best.Count)];
    }

    private static int Midpoint(Vector3Int from, Vector3Int to, bool primaryHorizontal)
        => primaryHorizontal ? (from.x + to.x) / 2 : (from.y + to.y) / 2;

    /// <summary>
    /// 꺾는 자리를 두 문 사이 아무 데나 잡은 ㄹ자.
    ///
    /// 자리를 <b>두 문 사이에서만</b> 고르는 것이 중요하다. 바깥으로 벗어나면
    /// 통로가 길어지고 방 벽을 뚫을 수 있다(예전에 그렇게 층이 무너졌다).
    /// </summary>
    private static List<Vector3Int> RandomZ(Vector3Int from, Vector3Int to, bool primaryHorizontal)
    {
        int a = primaryHorizontal ? from.x : from.y;
        int b = primaryHorizontal ? to.x : to.y;

        return CorridorShapes.Z(from, to, primaryHorizontal, RandomBetween(a, b));
    }

    /// <summary>두 값 사이(양 끝 포함)에서 하나 고른다.</summary>
    private static int RandomBetween(int a, int b)
        => Random.Range(Mathf.Min(a, b), Mathf.Max(a, b) + 1);

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
