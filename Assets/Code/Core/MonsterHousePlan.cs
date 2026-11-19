using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>방 안쪽에 실제 진입할 때 한 번만 발동한다. 숨겨진 적의 착지 칸도 결정한다.</summary>
public sealed class MonsterHousePlan
{
    public RectInt Interior { get; }
    public bool Triggered { get; private set; }
    public MonsterHousePlan(RectInt interior) => Interior = interior;

    public bool TryEnter(Vector3Int player)
    {
        if (Triggered || !Interior.Contains(new Vector2Int(player.x, player.y))) return false;
        Triggered = true;
        return true;
    }

    public bool TryLanding(Vector3Int preferred, ISet<Vector3Int> occupied,
        Func<Vector3Int, bool> walkable, out Vector3Int result)
    {
        bool Free(Vector3Int cell) => Interior.Contains(new Vector2Int(cell.x, cell.y)) &&
            !occupied.Contains(cell) && walkable(cell);
        if (Free(preferred)) { result = preferred; return true; }
        for (int y = Interior.yMin; y < Interior.yMax; y++)
        for (int x = Interior.xMin; x < Interior.xMax; x++)
        {
            var cell = new Vector3Int(x, y, 0);
            if (Free(cell)) { result = cell; return true; }
        }
        result = default;
        return false;
    }
}
