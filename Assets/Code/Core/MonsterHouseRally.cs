using System.Collections.Generic;
using UnityEngine;

/// <summary>경보가 울린 방까지의 공용 길찾기. 플레이어가 떠나도 목표 방은 유지한다.</summary>
public sealed class MonsterHouseRally
{
    public RectInt Room { get; }
    private readonly ChaseMap route = new ChaseMap();

    public MonsterHouseRally(RectInt room, IWalkableGrid grid)
    {
        Room = room;
        var center = new Vector3Int(room.xMin + room.width / 2, room.yMin + room.height / 2, 0);
        if (grid.IsWalkable(center)) route.Rebuild(grid, GridUtil.CellCenter(center));
        else
        {
            for (int y = room.yMin; y < room.yMax; y++)
            for (int x = room.xMin; x < room.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!grid.IsWalkable(cell)) continue;
                route.Rebuild(grid, GridUtil.CellCenter(cell));
                return;
            }
        }
    }

    public bool HasArrived(Vector3Int cell) => Room.Contains(new Vector2Int(cell.x, cell.y));
    public bool TryGetDistance(Vector2 world, out int distance) => route.TryGetDistance(world, out distance);
    public IEnumerable<Vector2> StepsToward(Vector2 world) => route.StepsToward(world);
}
