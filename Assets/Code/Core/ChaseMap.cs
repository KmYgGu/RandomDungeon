using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어까지 몇 걸음인지를 칸마다 적어 둔 지도.
///
/// 예전 추격은 "플레이어가 있는 쪽으로 한 칸" 이라는 탐욕적 방식이었다.
/// 벽이 가로막히면 대체 방향을 몇 개 시도해 보는 것이 전부라, 우회가 필요한
/// 지형에서는 벽 앞에 붙어 제자리걸음만 했다. (게다가 축 방향으로 막혔을 때
/// 시도하는 대체 방향 계산이 잘못돼 있어서 수직 방향은 아예 시도하지 않았다.)
///
/// 여기서는 플레이어 자리에서 너비 우선 탐색을 한 번 돌려 모든 칸의 걸음 수를
/// 구해 둔다. 그러면 각 적은 "이웃 칸 중 숫자가 작은 쪽" 으로 한 칸 가기만 하면
/// 언제나 최단 경로를 따라간다. 벽을 크게 돌아가는 길도 저절로 나온다.
///
/// 적이 몇 마리든 턴마다 탐색은 한 번뿐이라, 적마다 길찾기를 돌리는 것보다 싸다.
/// </summary>
public class ChaseMap
{
    /// <summary>닿을 수 없는 칸.</summary>
    public const int Unreachable = int.MaxValue;

    /// <summary>대각선을 포함한 여덟 방향.</summary>
    private static readonly Vector2Int[] Neighbours =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    private IWalkableGrid grid;
    private BoundsInt bounds;
    private int[,] steps;
    private bool ready;

    private readonly Queue<Vector3Int> frontier = new Queue<Vector3Int>();

    /// <summary>이번 턴의 지도가 준비돼 있는가.</summary>
    public bool IsReady => ready;

    /// <summary>
    /// 플레이어 위치를 기준으로 지도를 다시 만든다. 턴마다 한 번만 부르면 된다.
    /// </summary>
    public void Rebuild(IWalkableGrid walkableGrid, Vector2 targetWorldPosition)
    {
        ready = false;
        grid = walkableGrid;
        if (grid == null) return;

        bounds = grid.Bounds;
        if (bounds.size.x <= 0 || bounds.size.y <= 0) return;

        if (steps == null || steps.GetLength(0) != bounds.size.x || steps.GetLength(1) != bounds.size.y)
            steps = new int[bounds.size.x, bounds.size.y];

        for (int x = 0; x < bounds.size.x; x++)
            for (int y = 0; y < bounds.size.y; y++)
                steps[x, y] = Unreachable;

        Vector3Int start = grid.WorldToCell(targetWorldPosition);
        if (!Contains(start)) return;

        // 플레이어가 선 칸 자체는 벽일 수 없지만, 만약을 위해 확인하지 않고 0으로 둔다.
        SetSteps(start, 0);
        frontier.Clear();
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int nextDistance = GetSteps(current) + 1;

            foreach (Vector2Int offset in Neighbours)
            {
                var next = new Vector3Int(current.x + offset.x, current.y + offset.y, 0);

                if (!Contains(next)) continue;
                if (GetSteps(next) != Unreachable) continue;
                if (!grid.IsWalkable(next)) continue;
                if (!CanCrossDiagonally(current, offset)) continue;

                SetSteps(next, nextDistance);
                frontier.Enqueue(next);
            }
        }

        ready = true;
    }

    /// <summary>해당 위치에서 플레이어까지 몇 걸음인가. 닿을 수 없으면 false.</summary>
    public bool TryGetDistance(Vector2 worldPosition, out int distance)
    {
        distance = Unreachable;
        if (!ready) return false;

        Vector3Int cell = grid.WorldToCell(worldPosition);
        if (!Contains(cell)) return false;

        distance = GetSteps(cell);
        return distance != Unreachable;
    }

    /// <summary>
    /// 플레이어에게 가까워지는 이동 방향들을 가까운 순서대로 돌려준다.
    ///
    /// 첫 번째가 최선이지만, 다른 적이 그 칸을 먼저 찜했을 수 있으므로
    /// 부르는 쪽이 순서대로 시도해 보게 한다.
    /// </summary>
    public IEnumerable<Vector2> StepsToward(Vector2 worldPosition)
    {
        if (!ready) yield break;

        Vector3Int from = grid.WorldToCell(worldPosition);
        if (!Contains(from)) yield break;

        int here = GetSteps(from);
        if (here == Unreachable) yield break;

        // 지금보다 가깝거나 같은 칸만 후보로 삼는다. 뒤로 물러나지 않게 하기 위해서다.
        var candidates = new List<(int distance, Vector2 direction)>();

        foreach (Vector2Int offset in Neighbours)
        {
            var next = new Vector3Int(from.x + offset.x, from.y + offset.y, 0);

            if (!Contains(next)) continue;
            if (!grid.IsWalkable(next)) continue;
            if (!CanCrossDiagonally(from, offset)) continue;

            int distance = GetSteps(next);
            if (distance == Unreachable || distance > here) continue;

            candidates.Add((distance, new Vector2(offset.x, offset.y)));
        }

        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

        foreach ((int _, Vector2 direction) in candidates)
            yield return direction;
    }

    /// <summary>
    /// 대각선으로 지나갈 수 있는가. 벽 두 개 사이의 틈으로 비스듬히 빠져나가는 것을 막는다.
    /// 실제 이동 규칙(TileRules.CanMoveDiagonally)과 같은 기준이어야
    /// 지도가 알려 준 길을 실제로 걸어갈 수 있다.
    /// </summary>
    private bool CanCrossDiagonally(Vector3Int from, Vector2Int offset)
    {
        if (offset.x == 0 || offset.y == 0) return true;

        var sideA = new Vector3Int(from.x + offset.x, from.y, 0);
        var sideB = new Vector3Int(from.x, from.y + offset.y, 0);

        return !grid.IsWall(sideA) && !grid.IsWall(sideB);
    }

    private bool Contains(Vector3Int cell)
    {
        return cell.x >= bounds.xMin && cell.x < bounds.xMax
            && cell.y >= bounds.yMin && cell.y < bounds.yMax;
    }

    private int GetSteps(Vector3Int cell) => steps[cell.x - bounds.xMin, cell.y - bounds.yMin];

    private void SetSteps(Vector3Int cell, int value) => steps[cell.x - bounds.xMin, cell.y - bounds.yMin] = value;
}
