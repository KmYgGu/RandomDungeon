using System.Collections.Generic;
using UnityEngine;

/// <summary>방의 종류. 층마다 확률적으로 특별한 방이 섞인다.</summary>
public enum RoomKind
{
    /// <summary>평범한 방.</summary>
    Normal,
    /// <summary>보물방. 아이템이 많고 적은 없다.</summary>
    Treasure,
    /// <summary>몬스터 하우스. 적이 몰려 있고 보상도 크다.</summary>
    MonsterHouse,
}

/// <summary>
/// 방의 출입구. 벽 위의 한 칸과, 통로가 시작되는 바깥 한 칸을 함께 갖는다.
///
/// 예전에는 문이라는 개념이 없어서 통로가 방 중심에서 시작했고,
/// 그 결과 방 바닥의 14%가 통로 타일로 덮여 방을 가로지르는 줄무늬가 생겼다.
/// </summary>
public struct Door
{
    /// <summary>벽 위에 뚫린 칸.</summary>
    public Vector3Int Position;

    /// <summary>문 바로 바깥. 통로는 여기서 출발한다.</summary>
    public Vector3Int Outside;

    public Door(Vector3Int position, Vector3Int outside)
    {
        Position = position;
        Outside = outside;
    }
}

/// <summary>
/// 방 하나. 위치와 크기, 종류, 그리고 뚫린 문들을 안다.
/// Bounds 는 벽을 포함한 바깥 테두리이고, 실제로 걸어 다닐 수 있는 곳은 그 안쪽이다.
/// </summary>
public class RoomInfo
{
    public RectInt Bounds { get; }
    public RoomKind Kind { get; set; } = RoomKind.Normal;
    public List<Door> Doors { get; } = new List<Door>();

    public RoomInfo(int x, int y, int width, int height)
    {
        Bounds = new RectInt(x, y, width, height);
    }

    // 바닥이 칠해지는 안쪽 범위(양 끝 포함).
    public int InteriorMinX => Bounds.xMin + 1;
    public int InteriorMaxX => Bounds.xMax - 1;
    public int InteriorMinY => Bounds.yMin + 1;
    public int InteriorMaxY => Bounds.yMax - 1;

    public Vector3Int Center => new Vector3Int(
        (InteriorMinX + InteriorMaxX) / 2,
        (InteriorMinY + InteriorMaxY) / 2,
        0);

    /// <summary>
    /// 적이 "지금 방 안에 있는가" 를 판정할 때 쓰는 월드 좌표 영역.
    /// 칸의 중심이 x+0.5 이므로 안쪽 칸들의 중심만 포함하도록 잡는다.
    /// </summary>
    public Rect InteriorWorldRect => new Rect(
        Bounds.xMin + 1.5f,
        Bounds.yMin + 1.5f,
        Bounds.width - 1,
        Bounds.height - 1);

    public bool ContainsInterior(int x, int y)
    {
        return x >= InteriorMinX && x <= InteriorMaxX
            && y >= InteriorMinY && y <= InteriorMaxY;
    }

    /// <summary>벽까지 포함해서 이 칸이 방에 속하는가. 통로가 방을 뚫지 않게 검사할 때 쓴다.</summary>
    public bool ContainsIncludingWall(int x, int y)
    {
        return x >= Bounds.xMin && x <= Bounds.xMax
            && y >= Bounds.yMin && y <= Bounds.yMax;
    }

    /// <summary>안쪽 아무 칸.</summary>
    public Vector3Int RandomInteriorCell()
    {
        return new Vector3Int(
            Random.Range(InteriorMinX, InteriorMaxX + 1),
            Random.Range(InteriorMinY, InteriorMaxY + 1),
            0);
    }

    /// <summary>가장자리 사이 간격. 중심 거리보다 실제 여백을 정확히 나타낸다.</summary>
    public int GapTo(RoomInfo other)
    {
        int gapX = Mathf.Max(other.Bounds.xMin - Bounds.xMax, Bounds.xMin - other.Bounds.xMax);
        int gapY = Mathf.Max(other.Bounds.yMin - Bounds.yMax, Bounds.yMin - other.Bounds.yMax);
        return Mathf.Max(gapX, gapY);
    }
}
