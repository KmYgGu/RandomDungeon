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

    /// <summary>
    /// 월드 좌표가 놓인 칸. 타일맵 없이 구한다.
    ///
    /// 칸 (x, y) 는 월드에서 x 이상 x+1 미만을 차지하고 중심이 x+0.5 다
    /// (CellCenterOffset). 그래서 내림 한 번이면 칸이 나온다. 타일맵의
    /// WorldToCell 과 같은 답이지만 Unity 없이도 돈다.
    /// </summary>
    public static Vector3Int CellOf(Vector3 world)
        => new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);

    /// <summary>
    /// 두 자리가 같은 칸인가.
    ///
    /// "거리가 0.1 보다 가까운가" 로 묻던 것을 대신한다. 그 물음은 여러 파일에
    /// 저마다의 0.1 로 흩어져 있었고, 걸어가는 도중의 좌표처럼 칸 중심에서
    /// 벗어난 값이 들어오면 같은 칸인데도 아니라고 답했다. 칸으로 물으면
    /// 임계값이 필요 없다.
    /// </summary>
    public static bool SameCell(Vector3 a, Vector3 b)
        => Mathf.FloorToInt(a.x) == Mathf.FloorToInt(b.x)
        && Mathf.FloorToInt(a.y) == Mathf.FloorToInt(b.y);

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
