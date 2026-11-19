using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>층별 미니맵 탐색 기록. 방은 입장할 때, 통로는 현재 칸과 주변 8칸을 밝힌다.</summary>
public sealed class MinimapExploration
{
    private readonly HashSet<Vector2Int> terrain;
    private readonly List<RectInt> rooms;
    private readonly HashSet<int> enteredRooms = new HashSet<int>();
    private readonly HashSet<Vector2Int> known = new HashSet<Vector2Int>();
    public IReadOnlyCollection<Vector2Int> KnownCells => known;

    public MinimapExploration(IEnumerable<Vector2Int> openCells, IEnumerable<RectInt> roomInteriors)
    {
        terrain = new HashSet<Vector2Int>(openCells);
        rooms = new List<RectInt>(roomInteriors);
    }

    public bool IsKnown(Vector2Int cell) => known.Contains(cell);
    public bool IsTerrainOpen(Vector2Int cell) => terrain.Contains(cell);

    /// <summary>적의 현재 시야 판정은 탐색 기록과 별도로 계산한다.</summary>
    public bool CanSeeEnemy(Vector2Int player, Vector2Int enemy)
    {
        if (!terrain.Contains(enemy)) return false;
        int roomIndex = FindRoom(player);
        if (roomIndex >= 0 && rooms[roomIndex].Contains(enemy)) return true;
        return CanSeeNeighbor(player, enemy);
    }

    private bool CanSeeNeighbor(Vector2Int player, Vector2Int neighbor)
    {
        Vector2Int direction = neighbor - player;
        if (direction == Vector2Int.zero || Math.Abs(direction.x) > 1 || Math.Abs(direction.y) > 1 ||
            !terrain.Contains(neighbor)) return false;
        return direction.x == 0 || direction.y == 0 ||
            (terrain.Contains(player + new Vector2Int(direction.x, 0)) &&
             terrain.Contains(player + new Vector2Int(0, direction.y)));
    }

    public bool Reveal(Vector2Int player)
    {
        bool changed = Mark(player);
        int roomIndex = FindRoom(player);
        if (roomIndex >= 0)
        {
            if (!enteredRooms.Add(roomIndex)) return changed;
            RectInt room = rooms[roomIndex];
            // 방 바닥과 벽 위의 출입구까지만 포함한다. 바깥 통로는 포함하지 않는다.
            for (int y = room.yMin - 1; y <= room.yMax; y++)
            for (int x = room.xMin - 1; x <= room.xMax; x++)
                changed |= Mark(new Vector2Int(x, y));
            return changed;
        }

        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            var neighbor = player + new Vector2Int(x, y);
            // 방 내부는 실제로 들어간 뒤 전체를 공개한다.
            if (FindRoom(neighbor) >= 0) continue;
            if (CanSeeNeighbor(player, neighbor)) changed |= Mark(neighbor);
        }
        return changed;
    }

    private bool Mark(Vector2Int cell) => terrain.Contains(cell) && known.Add(cell);

    private int FindRoom(Vector2Int cell)
    {
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i].Contains(cell)) return i;
        return -1;
    }
}
