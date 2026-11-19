using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어느 방이 상점이 되고, 그 안의 어디에 물건을 늘어놓는가.
///
/// 방과 칸만 다루므로 Unity 없이 그대로 확인할 수 있다. 실제로 상인과 물건을
/// 만드는 일은 DungeonPopulator.PopulateShop 이 이 답을 보고 한다.
/// </summary>
public static class ShopPlan
{
    /// <summary>
    /// 상점이 될 수 있는 방인가.
    ///
    ///   평범한 방 — 몬스터 하우스 같은 특수 방을 덮어쓰지 않는다
    ///   정사각형   — 가운데에 진열대를 반듯하게 놓을 수 있다
    ///   안쪽 4칸↑  — 진열 2×2 와 그 둘레의 통행로가 들어간다
    ///   문이 하나  — 상인이 그 하나를 막아서면 값을 안 치르고는 못 나간다
    ///
    /// 문은 <b>자리</b>로 센다. 같은 자리에 통로가 둘 이어져 있어도 문은 하나다.
    /// </summary>
    public static bool Eligible(RoomInfo room)
    {
        if (room.Kind != RoomKind.Normal) return false;
        if (room.Bounds.width != room.Bounds.height) return false;
        if (room.InteriorCellRect.width < 4) return false;

        var doors = new HashSet<Vector3Int>();
        foreach (Door door in room.Doors) doors.Add(door.Position);

        return doors.Count == 1;
    }

    /// <summary>
    /// 조건에 맞는 방 하나를 상점으로 정한다. 정했으면 true.
    ///
    /// 한 층에 하나뿐이다 — 이미 상점이 있으면 아무것도 하지 않는다.
    /// 굴림(roll · candidate)은 밖에서 받는다. 안에서 굴리면 Unity 없이 확인할 수 없다.
    /// </summary>
    /// <param name="roll">0~1. chance 보다 작으면 상점이 선다.</param>
    /// <param name="candidate">후보 방들 중 몇 번째를 고를지. 범위를 넘으면 끝의 것.</param>
    public static bool Assign(DungeonLayout layout, float roll, int candidate, float chance)
    {
        chance = Mathf.Clamp01(chance);

        // 1 이면 굴림과 상관없이 반드시 선다(roll 이 정확히 1 이어도).
        if (chance <= 0f || (chance < 1f && roll >= chance)) return false;

        var eligible = new List<RoomInfo>();

        foreach (RoomInfo room in layout.Rooms)
        {
            if (room.Kind == RoomKind.Shop) return false;
            if (Eligible(room)) eligible.Add(room);
        }

        if (eligible.Count == 0) return false;

        eligible[Mathf.Clamp(candidate, 0, eligible.Count - 1)].Kind = RoomKind.Shop;
        return true;
    }

    /// <summary>
    /// 물건을 늘어놓을 칸들. <b>가운데에서 가까운 차례</b>로 준다.
    ///
    /// 방 한가운데에 짝수 변의 정사각형을 잡는다. 벽 쪽 한 줄은 언제나 비워
    /// 두므로 진열대가 통행로를 막지 않는다. 방이 클수록 진열대도 커진다.
    ///
    /// 차례가 가운데부터인 것은, 앞의 넷이 언제나 한가운데 2×2 가 되게 하려는 것이다.
    /// 물건이 모자라 일부만 놓더라도 가운데부터 차서 반듯해 보인다.
    /// </summary>
    public static List<Vector3Int> DisplayCells(RectInt room)
    {
        int side = Math.Max(2, ((Math.Min(room.width, room.height) - 2) / 2) * 2);

        var cells = new List<Vector3Int>();
        if (side > room.width || side > room.height) return cells;

        int x0 = room.xMin + (room.width - side) / 2;
        int y0 = room.yMin + (room.height - side) / 2;

        for (int y = y0; y < y0 + side; y++)
        for (int x = x0; x < x0 + side; x++)
            cells.Add(new Vector3Int(x, y, 0));

        // 가운데에서 얼마나 먼가(가로·세로 중 먼 쪽). 변이 짝수라 가운데가 칸과
        // 칸 사이에 놓이므로, 반 칸을 피하려고 두 배로 늘려 잰다.
        int Rank(Vector3Int p) => Math.Max(
            Math.Abs(2 * p.x - (2 * x0 + side - 1)),
            Math.Abs(2 * p.y - (2 * y0 + side - 1)));

        // 거리가 같으면 아래에서 위로, 왼쪽에서 오른쪽으로. 차례가 굴림을 타지
        // 않아야 같은 시드에서 같은 진열이 나온다.
        cells.Sort((a, b) =>
        {
            int rank = Rank(a).CompareTo(Rank(b));
            if (rank != 0) return rank;

            return a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
        });

        return cells;
    }
}
