using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 층의 구조를 담는 자료. "무엇이 어디에 있는가" 만 알고,
/// 그것을 어떻게 그리는지(타일)나 무엇을 놓는지(몬스터)는 모른다.
/// </summary>
public class DungeonLayout
{
    public List<RoomInfo> Rooms { get; } = new List<RoomInfo>();

    public bool IsEmpty => Rooms.Count == 0;

    public RoomInfo RandomRoom() => Rooms[Random.Range(0, Rooms.Count)];

    /// <summary>
    /// 적이 방 안에 있는지 판정할 때 쓰는 영역들. (Enemy.roomdata)
    /// </summary>
    public List<Rect> InteriorRects()
    {
        var result = new List<Rect>(Rooms.Count);
        foreach (RoomInfo room in Rooms)
            result.Add(room.InteriorWorldRect);
        return result;
    }

    /// <summary>
    /// 적이 순찰 목표로 삼는 지점들 = 모든 방의 문. (Enemy.connectionPoints)
    ///
    /// 예전에는 "통로 타일 중 방 경계에 걸친 것" 을 긁어모아 썼는데,
    /// 통로가 방 중심에서 출발하는 구조라 방 안쪽 칸도 섞여 들어왔다.
    /// 이제는 진짜 출입구만 들어간다.
    /// </summary>
    public List<Vector3> DoorWorldPositions()
    {
        var result = new List<Vector3>();
        foreach (RoomInfo room in Rooms)
        {
            foreach (Door door in room.Doors)
                result.Add(GridUtil.CellCenter(door.Position));
        }
        return result;
    }

    /// <summary>해당 칸이 어떤 방의 안쪽인가.</summary>
    public RoomInfo RoomAt(int x, int y)
    {
        foreach (RoomInfo room in Rooms)
        {
            if (room.ContainsInterior(x, y))
                return room;
        }
        return null;
    }
}
