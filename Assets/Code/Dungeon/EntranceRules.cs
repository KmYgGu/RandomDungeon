using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>문과 바로 바깥 한 칸의 양옆은 벽으로 남긴다. 통로의 다양성은 그 뒤에서 시작한다.</summary>
public static class EntranceRules
{
    public static HashSet<Vector3Int> BlockedCells(DungeonLayout layout)
    {
        var cells = new HashSet<Vector3Int>();
        foreach (var room in layout.Rooms)
        {
            for (int x = room.Bounds.xMin; x <= room.Bounds.xMax; x++)
            for (int y = room.Bounds.yMin; y <= room.Bounds.yMax; y++) cells.Add(new Vector3Int(x, y, 0));
            foreach (var door in room.Doors)
            {
                var forward = door.Outside - door.Position;
                var side = new Vector3Int(-forward.y, forward.x, 0);
                cells.Add(door.Position + side); cells.Add(door.Position - side);
                cells.Add(door.Outside + side); cells.Add(door.Outside - side);
            }
        }
        return cells;
    }
    public static bool IsNarrow(Door door, Func<Vector3Int, bool> isOpen)
    {
        var forward = door.Outside - door.Position;
        var side = new Vector3Int(-forward.y, forward.x, 0);
        return !isOpen(door.Position + side) && !isOpen(door.Position - side)
            && !isOpen(door.Outside + side) && !isOpen(door.Outside - side);
    }
}
