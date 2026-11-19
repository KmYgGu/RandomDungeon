using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>최종 지형의 실제 걸음 거리로 진행 가능 여부와 시작점·계단을 정한다.</summary>
public static class DungeonRoutes
{
    private static readonly Vector3Int[] Steps =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down
    };

    public static Dictionary<Vector3Int, int> Distances(
        int width, int height, Func<Vector3Int, bool> isOpen, Vector3Int start)
    {
        var distances = new Dictionary<Vector3Int, int>();
        bool Inside(Vector3Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
        if (!Inside(start) || !isOpen(start)) return distances;
        var queue = new Queue<Vector3Int>();
        distances.Add(start, 0);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (!Inside(next) || distances.ContainsKey(next) || !isOpen(next)) continue;
                distances.Add(next, distances[cell] + 1);
                queue.Enqueue(next);
            }
        }
        return distances;
    }

    public static bool TryPlan(DungeonLayout layout, int width, int height,
        Func<Vector3Int, bool> isOpen, int seed,
        out Vector3Int start, out Vector3Int stairs)
    {
        start = stairs = default;
        var starts = new List<RoomInfo>();
        foreach (var room in layout.Rooms)
        {
            if (!isOpen(room.Center)) continue;
            int exits = 0;
            foreach (var step in Steps)
                if (isOpen(room.Center + step)) exits++;
            if (exits >= 2) starts.Add(room);
        }
        if (starts.Count == 0) return false;
        var random = new System.Random(seed);
        var startRoom = starts[random.Next(starts.Count)];
        start = startRoom.Center;
        var distances = Distances(width, height, isOpen, start);
        var candidates = new List<Vector3Int>();
        int farthest = 0;
        foreach (var room in layout.Rooms)
        {
            // 중심만 검사하면 방 구석의 계단·아이템이 고립되어도 통과한다.
            for (int x = room.InteriorMinX; x <= room.InteriorMaxX; x++)
            for (int y = room.InteriorMinY; y <= room.InteriorMaxY; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!distances.TryGetValue(cell, out int distance)) return false;
                if (room.Kind == RoomKind.Shop || (layout.Rooms.Count > 1 && room == startRoom)) continue;
                if (distance == 0) continue;
                candidates.Add(cell);
                farthest = Math.Max(farthest, distance);
            }
        }
        if (candidates.Count == 0) return false;
        // 최장 거리 하나로 고정하지 않아 같은 구조에서도 목적지가 달라진다.
        int threshold = Math.Max(1, (farthest * 3 + 3) / 4);
        candidates.RemoveAll(cell => distances[cell] < threshold);
        stairs = candidates[random.Next(candidates.Count)];
        return true;
    }
}
