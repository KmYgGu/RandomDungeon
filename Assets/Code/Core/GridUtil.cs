using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일 좌표 ↔ 월드 좌표 변환.
///
/// 예전에는 "+ 0.5f 두 번" 이라는 같은 계산이 RandomDung 안에만
/// ApplyTilemapOffset 한 번 + 손으로 쓴 SetGround.x/y 세 번으로 흩어져 있었다.
/// 오프셋 값을 바꿀 일이 생기면 네 군데를 모두 찾아야 했다.
/// </summary>
public static class GridUtil
{
    /// <summary>타일 한 칸의 중심으로 보내기 위한 오프셋.</summary>
    public const float CellCenterOffset = 0.5f;

    /// <summary>
    /// 여덟 방향. 적의 이웃 살피기와 혼란의 방향 뒤틀기가 함께 쓴다.
    ///
    /// 예전에는 Enemy 안에만 있었다. 혼란(방향 교란)이 같은 목록을 필요로 하면서,
    /// 한쪽만 고치면 "적이 갈 수 있는 방향" 과 "혼란이 보내는 방향" 이 어긋나게 됐다.
    /// </summary>
    public static readonly Vector2[] EightDirections =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
        new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1),
    };

    /// <summary>타일 좌표를 그 칸의 중심 월드 좌표로 바꾼다.</summary>
    public static Vector3 CellCenter(Tilemap tilemap, Vector3Int cellPosition)
    {
        Vector3 world = tilemap.CellToWorld(cellPosition);
        world.x += CellCenterOffset;
        world.y += CellCenterOffset;
        return world;
    }

    /// <summary>타일맵 없이, 정수 좌표만으로 칸 중심을 구한다.</summary>
    public static Vector3 CellCenter(Vector3Int cellPosition)
    {
        return new Vector3(
            cellPosition.x + CellCenterOffset,
            cellPosition.y + CellCenterOffset,
            0f);
    }
}
