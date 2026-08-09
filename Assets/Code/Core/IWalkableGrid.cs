using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 길찾기가 필요로 하는 격자 정보의 최소 집합.
///
/// ChaseMap 이 Tilemap 을 직접 알지 않게 하려고 둔 경계다.
/// 덕분에 길찾기 로직을 Unity 씬 없이 가짜 격자로 검증할 수 있다.
/// </summary>
public interface IWalkableGrid
{
    BoundsInt Bounds { get; }

    /// <summary>이 칸에 들어설 수 있는가. 맵 밖이면 false.</summary>
    bool IsWalkable(Vector3Int cell);

    /// <summary>이 칸이 벽인가. 대각선으로 스쳐 지나갈 수 있는지 볼 때 쓴다.</summary>
    bool IsWall(Vector3Int cell);

    Vector3Int WorldToCell(Vector2 worldPosition);
}

/// <summary>실제 게임에서 쓰는 구현. Tilemap 과 TileRules 를 이어 준다.</summary>
public class TilemapWalkableGrid : IWalkableGrid
{
    private readonly Tilemap tilemap;

    public TilemapWalkableGrid(Tilemap tilemap)
    {
        this.tilemap = tilemap;
    }

    public BoundsInt Bounds => tilemap.cellBounds;

    public bool IsWalkable(Vector3Int cell) => TileRules.IsWalkableCell(tilemap, cell);

    public bool IsWall(Vector3Int cell) => TileRules.IsImpassableCell(tilemap, cell);

    public Vector3Int WorldToCell(Vector2 worldPosition) => tilemap.WorldToCell(worldPosition);
}
