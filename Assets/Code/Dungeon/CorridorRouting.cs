using System;
using System.Collections.Generic;
using UnityEngine;

public enum CorridorStyle { Corner, Dogleg, Stair, Detour, Routed }

/// <summary>방과 경계를 피하는 후보 중 모양부터 고른다. 실제 선택 과정을 Unity 없이 검증한다.</summary>
public static class CorridorRouting
{
    private static readonly Vector3Int[] Steps = { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down };

    public sealed class Route
    {
        public CorridorStyle Style { get; }
        public List<Vector3Int> Cells { get; }
        public Route(CorridorStyle style, List<Vector3Int> cells) { Style = style; Cells = cells; }
    }

    public static Route Plan(Vector3Int from, Vector3Int to, bool horizontal, int width, int height,
        Func<Vector3Int, bool> blocked, Func<List<Vector3Int>, int> penalty, System.Random random,
        CorridorStyle? previous = null)
    {
        bool Allowed(Vector3Int p) => p.x > 0 && p.y > 0 && p.x < width - 1 && p.y < height - 1 && !blocked(p);
        if (!Allowed(from) || !Allowed(to)) return null;
        var families = new Dictionary<CorridorStyle, List<Route>>();
        void Add(CorridorStyle style, List<Vector3Int> raw)
        {
            var cells = new List<Vector3Int>();
            var seen = new HashSet<Vector3Int>();
            foreach (var cell in raw)
            {
                if (cells.Count > 0 && cells[cells.Count - 1] == cell) continue;
                if (!Allowed(cell) || !seen.Add(cell)) return;
                cells.Add(cell);
            }
            int direct = Math.Abs(to.x - from.x) + Math.Abs(to.y - from.y);
            if (cells.Count > direct + 25) return;
            // 한 후보가 스스로 2×2를 채우면 통로 정리에서 모양이 무너지므로 처음부터 빼 둔다.
            foreach (var cell in cells)
                if (seen.Contains(cell + Vector3Int.right) && seen.Contains(cell + Vector3Int.up)
                    && seen.Contains(cell + Vector3Int.right + Vector3Int.up)) return;
            // 모양 이름만 달라지는 것을 막는다. 두 번 이하로 꺾이는 계단·우회 후보는 제외한다.
            if ((style == CorridorStyle.Stair || style == CorridorStyle.Detour) && Turns(cells) < 3) return;
            if (!families.ContainsKey(style)) families.Add(style, new List<Route>());
            families[style].Add(new Route(style, cells));
        }
        Add(CorridorStyle.Corner, CorridorShapes.L(from, to, true));
        Add(CorridorStyle.Corner, CorridorShapes.L(from, to, false));
        for (int i = 0; i < 4; i++)
        {
            bool axis = i % 2 == 0 ? horizontal : !horizontal;
            int a = axis ? from.x : from.y, b = axis ? to.x : to.y;
            Add(CorridorStyle.Dogleg, CorridorShapes.Z(from, to, axis, random.Next(Math.Min(a, b), Math.Max(a, b) + 1)));
            Add(CorridorStyle.Stair, CorridorShapes.Stair(from, to, axis, 2 + i % 3));
            Add(CorridorStyle.Detour, CorridorShapes.Detour(from, to, axis, (i < 2 ? 1 : -1) * random.Next(3, 7)));
        }
        if (families.Count > 0)
        {
            // 후보 개수가 많은 ㄹ자만 뽑히지 않도록 종류를 먼저 고르고, 같은 종류의 연속도 피한다.
            var styles = new List<CorridorStyle>(families.Keys);
            if (previous.HasValue && styles.Count > 1) styles.Remove(previous.Value);
            var options = families[styles[random.Next(styles.Count)]];
            int best = int.MaxValue;
            var winners = new List<Route>();
            foreach (var option in options)
            {
                int cost = penalty(option.Cells);
                if (cost < best) { best = cost; winners.Clear(); }
                if (cost == best) winners.Add(option);
            }
            return winners[random.Next(winners.Count)];
        }
        // 모든 모양이 방에 부딪히면 방 벽을 뚫지 않고 빈 공간을 탐색한다.
        var parents = new Dictionary<Vector3Int, Vector3Int> { [from] = from };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell == to)
            {
                var path = new List<Vector3Int> { to };
                while (cell != from) { cell = parents[cell]; path.Add(cell); }
                path.Reverse();
                return new Route(CorridorStyle.Routed, path);
            }
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (!Allowed(next) || parents.ContainsKey(next)) continue;
                parents.Add(next, cell);
                queue.Enqueue(next);
            }
        }
        return null;
    }

    public static int Turns(List<Vector3Int> cells)
    {
        int turns = 0;
        var direction = Vector3Int.zero;
        for (int i = 1; i < cells.Count; i++)
        {
            var next = cells[i] - cells[i - 1];
            if (next == Vector3Int.zero) continue;
            if (direction != Vector3Int.zero && direction != next) turns++;
            direction = next;
        }
        return turns;
    }
}
