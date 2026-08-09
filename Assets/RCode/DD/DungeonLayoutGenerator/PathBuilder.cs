using System.Collections.Generic;
using UnityEngine;

// Bresenham 알고리즘을 이용하여 두 점을 연결하는 경로를 생성하는 유틸리티 클래스
public class PathBuilder : IPathBuilder
{

    public List<Vector3Int> BuildPath(
    Vector3Int start,
    Vector3Int end,
    TileType[,] grid,
    List<Room> rooms)
    {
        int maxAttempts = 50;

        Room startRoom = FindContainingRoom(start, rooms);
        Room endRoom = FindContainingRoom(end, rooms);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            List<Vector3Int> path;

            // 직선일 경우 (x 또는 y가 같음) → 직선만
            if (start.x == end.x || start.y == end.y)
            {
                path = Line(start, end);
            }
            else
            {
                // L자 경로 (무작위로 가로 먼저 or 세로 먼저)
                bool horizontalFirst = Random.value > 0.5f;
                var mid = horizontalFirst
                    ? new Vector3Int(end.x, start.y, 0)
                    : new Vector3Int(start.x, end.y, 0);

                path = new List<Vector3Int>();
                path.AddRange(Line(start, mid));
                path.AddRange(Line(mid, end));
            }

            //if (!IntersectsRoom(path, rooms, startRoom, endRoom) &&
            //    !TooCloseToRoom(path, rooms, startRoom, endRoom))
            // TooCloseToRoom 조건을 제거 (지금 단계에서는 과도함)
            if (!IntersectsRoom(path, rooms, startRoom, endRoom))
            {
                return path;
            }
        }

        Debug.LogWarning("유효한 경로를 찾지 못했습니다.");
        //return new List<Vector3Int>();
        return Line(start, end);
    }


    // 경로가 다른 방 안을 지나면 true 반환
    private static bool IntersectsRoom(List<Vector3Int> path, List<Room> rooms, Room startRoom, Room endRoom)
    {
        foreach (var room in rooms)
        {
            if (room == startRoom || room == endRoom)
                continue;

            foreach (var p in path)
            {
                if (room.area.Contains(new Vector2Int(p.x, p.y)))
                    return true;
            }
        }
        return false;
    }

    private static Room FindContainingRoom(Vector3Int pos, List<Room> rooms)
    {
        Vector2Int v2 = new Vector2Int(pos.x, pos.y);
        foreach (var room in rooms)
        {
            if (room.area.Contains(v2))
                return room;
        }
        return null;
    }

    private static List<Vector3Int> Line(Vector3Int a, Vector3Int b)
    {
        List<Vector3Int> path = new List<Vector3Int>();

        int dx = (int)Mathf.Sign(b.x - a.x);
        int dy = (int)Mathf.Sign(b.y - a.y);

        if (a.x != b.x)
        {
            for (int x = a.x; x != b.x + dx; x += dx)
                path.Add(new Vector3Int(x, a.y, 0));
        }

        if (a.y != b.y)
        {
            for (int y = a.y; y != b.y + dy; y += dy)
                path.Add(new Vector3Int(b.x, y, 0));
        }

        return path;
    }

}
