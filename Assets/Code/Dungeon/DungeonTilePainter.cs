using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일맵에 실제로 타일을 칠하는 일만 담당한다.
///
/// 방을 만들 때도, 통로를 팔 때도, 빈 곳을 벽으로 메울 때도
/// 결국 하는 일은 "이 칸에 이 타일을 놓는다" 하나다.
/// 예전에는 그 코드가 RandomDung 안 여섯 군데에 흩어져 있었고,
/// 미니맵에 같이 칠하는 것을 빠뜨린 곳도 있었다.
/// </summary>
public class DungeonTilePainter
{
    private readonly Tilemap floorTilemap;
    private readonly Tilemap minimap;

    private readonly TileBase roomFloorTile;
    private readonly TileBase corridorFloorTile;
    private readonly TileBase doorTile;
    private readonly TileBase wallTile;
    private readonly TileBase waterTile;
    private readonly TileBase minimapTile;

    public DungeonTilePainter(
        Tilemap floorTilemap,
        Tilemap minimap,
        TileBase roomFloorTile,
        TileBase corridorFloorTile,
        TileBase doorTile,
        TileBase wallTile,
        TileBase waterTile,
        TileBase minimapTile)
    {
        this.floorTilemap = floorTilemap;
        this.minimap = minimap;
        this.roomFloorTile = roomFloorTile;
        this.corridorFloorTile = corridorFloorTile;
        this.doorTile = doorTile;
        this.wallTile = wallTile;
        this.waterTile = waterTile;
        this.minimapTile = minimapTile;
    }

    public void Clear()
    {
        floorTilemap.ClearAllTiles();
        minimap.ClearAllTiles();
    }

    public bool IsEmpty(Vector3Int position) => floorTilemap.GetTile(position) == null;

    public bool IsCorridor(Vector3Int position) => floorTilemap.GetTile(position) == corridorFloorTile;

    public bool IsDoor(Vector3Int position)
        => doorTile != null && floorTilemap.GetTile(position) == doorTile;

    /// <summary>이미 파 놓은 길(통로 또는 문)인가.</summary>
    public bool IsPassage(Vector3Int position) => IsCorridor(position) || IsDoor(position);

    /// <summary>칸을 도로 비운다. 나중에 FillEmptySpace 가 벽으로 메운다.</summary>
    public void Erase(Vector3Int position)
    {
        floorTilemap.SetTile(position, null);
        minimap.SetTile(position, null);
    }

    public void PaintRoomFloor(Vector3Int position) => Paint(position, roomFloorTile, alsoMinimap: true);

    public void PaintCorridorFloor(Vector3Int position) => Paint(position, corridorFloorTile, alsoMinimap: true);

    public void PaintWater(Vector3Int position) => Paint(position, waterTile, alsoMinimap: false);

    /// <summary>
    /// 방의 출입구. 전용 타일(EntranceTile)이 지정돼 있지 않으면 통로 바닥으로 대신한다.
    /// 문 타일은 반드시 통행 가능해야 하므로 TileRules 의 금지 목록에 넣으면 안 된다.
    /// </summary>
    public void PaintDoor(Vector3Int position)
        => Paint(position, doorTile != null ? doorTile : corridorFloorTile, alsoMinimap: true);

    private void Paint(Vector3Int position, TileBase tile, bool alsoMinimap)
    {
        floorTilemap.SetTile(position, tile);
        if (alsoMinimap)
            minimap.SetTile(position, minimapTile);
    }

    /// <summary>
    /// 아직 아무것도 없는 칸을 전부 메운다.
    /// 드물게(10%) 벽 대신 물로 채워서 물이 많은 층이 나오게 한다.
    /// </summary>
    public void FillEmptySpace(int mapWidth, int mapHeight, bool floodWithWater)
    {
        TileBase filler = floodWithWater ? waterTile : wallTile;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                var position = new Vector3Int(x, y, 0);
                if (IsEmpty(position))
                    floorTilemap.SetTile(position, filler);
            }
        }
    }

    /// <summary>타일 칸의 중심 월드 좌표.</summary>
    public Vector3 CellCenter(Vector3Int cellPosition) => GridUtil.CellCenter(floorTilemap, cellPosition);
}
