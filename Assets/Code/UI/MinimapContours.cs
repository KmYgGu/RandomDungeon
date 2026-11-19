using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>열린 칸들의 합집합 경계만 추출한다. 같은 직선의 이웃 경계는 합친다.</summary>
public static class MinimapContours
{
    public struct Segment
    {
        public Vector2Int Start;
        public Vector2Int End;
        public Segment(Vector2Int start, Vector2Int end) { Start = start; End = end; }
    }

    /// <summary>실제 지형을 전달하면 미탐색 바닥과 맞닿는 경계는 막지 않는다.</summary>
    public static List<Segment> Build(IEnumerable<Vector2Int> openCells, Func<Vector2Int, bool> isTerrainOpen = null)
    {
        var cells = new HashSet<Vector2Int>(openCells);
        var horizontal = new SortedDictionary<int, List<int>>();
        var vertical = new SortedDictionary<int, List<int>>();
        foreach (var cell in cells)
        {
            if (!cells.Contains(cell + Vector2Int.down) &&
                (isTerrainOpen == null || !isTerrainOpen(cell + Vector2Int.down))) Add(horizontal, cell.y, cell.x);
            if (!cells.Contains(cell + Vector2Int.up) &&
                (isTerrainOpen == null || !isTerrainOpen(cell + Vector2Int.up))) Add(horizontal, cell.y + 1, cell.x);
            if (!cells.Contains(cell + Vector2Int.left) &&
                (isTerrainOpen == null || !isTerrainOpen(cell + Vector2Int.left))) Add(vertical, cell.x, cell.y);
            if (!cells.Contains(cell + Vector2Int.right) &&
                (isTerrainOpen == null || !isTerrainOpen(cell + Vector2Int.right))) Add(vertical, cell.x + 1, cell.y);
        }
        var result = new List<Segment>();
        Merge(horizontal, true, result);
        Merge(vertical, false, result);
        return result;
    }

    private static void Add(SortedDictionary<int, List<int>> lines, int line, int start)
    {
        if (!lines.TryGetValue(line, out var spans)) lines.Add(line, spans = new List<int>());
        spans.Add(start);
    }

    private static void Merge(SortedDictionary<int, List<int>> lines, bool horizontal, List<Segment> result)
    {
        foreach (var line in lines)
        {
            line.Value.Sort();
            int start = line.Value[0];
            int end = start + 1;
            for (int i = 1; i <= line.Value.Count; i++)
            {
                if (i < line.Value.Count && line.Value[i] == end) { end++; continue; }
                result.Add(horizontal
                    ? new Segment(new Vector2Int(start, line.Key), new Vector2Int(end, line.Key))
                    : new Segment(new Vector2Int(line.Key, start), new Vector2Int(line.Key, end)));
                if (i < line.Value.Count) { start = line.Value[i]; end = start + 1; }
            }
        }
    }
}
