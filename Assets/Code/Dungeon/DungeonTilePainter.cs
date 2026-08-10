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
    private readonly TileBase bedrockTile;
    private readonly TileBase bedrockWaterTile;
    private readonly TileBase minimapTile;

    public DungeonTilePainter(
        Tilemap floorTilemap,
        Tilemap minimap,
        TileBase roomFloorTile,
        TileBase corridorFloorTile,
        TileBase doorTile,
        TileBase wallTile,
        TileBase waterTile,
        TileBase bedrockTile,
        TileBase bedrockWaterTile,
        TileBase minimapTile)
    {
        this.floorTilemap = floorTilemap;
        this.minimap = minimap;
        this.roomFloorTile = roomFloorTile;
        this.corridorFloorTile = corridorFloorTile;
        this.doorTile = doorTile;
        this.wallTile = wallTile;
        this.waterTile = waterTile;

        // 끝자락 타일을 아직 안 만들었으면 보통 벽으로 대신한다. 겉모습이 같아야
        // 하는 타일이라 그래도 어색하지 않고, 허물 수 없다는 성질만 못 갖는다.
        this.bedrockTile = bedrockTile != null ? bedrockTile : wallTile;

        // 물 모양 끝자락이 없으면 벽 모양으로 대신한다. 물로 가득한 층에서
        // 가장자리만 벽으로 보이지만, 게임이 멈추지는 않는다.
        this.bedrockWaterTile = bedrockWaterTile != null ? bedrockWaterTile : this.bedrockTile;

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
    /// 맵 바깥을 허물 수 없는 타일로 두껍게 두른다.
    ///
    /// 두께가 한 줄이면 안 된다. 카메라는 플레이어를 그대로 따라갈 뿐 맵 경계에서
    /// 멈추지 않아서(CameraPlayer), 가장자리에 서면 화면 절반이 맵 바깥이 되고
    /// 거기에 카메라 배경색이 그대로 드러난다. 그래서 화면 절반보다 넉넉히 두른다.
    ///
    /// 이미 바닥이 깔린 칸은 건드리지 않는다 — 방이나 통로가 가장자리까지
    /// 닿아 있을 때 그것을 막아 버리면 던전이 끊긴다.
    /// </summary>
    /// <param name="floodedStage">물로 가득한 층인가. 끝자락 겉모습이 갈린다.</param>
    public void PaintBorder(int mapWidth, int mapHeight, bool floodedStage)
    {
        // 물로 가득한 층에서 가장자리만 벽이면 혼자 튄다. 겉모습만 바꾸는 것이고
        // 지나갈 수 없다는 성질은 같다(TileRules.BedrockTileNames).
        TileBase edge = floodedStage ? bedrockWaterTile : bedrockTile;

        for (int x = -BorderMargin; x < mapWidth + BorderMargin; x++)
        {
            for (int y = -BorderMargin; y < mapHeight + BorderMargin; y++)
            {
                bool outside = x < 0 || y < 0 || x >= mapWidth || y >= mapHeight;
                bool onEdge = x == 0 || y == 0 || x == mapWidth - 1 || y == mapHeight - 1;
                if (!outside && !onEdge) continue;

                var position = new Vector3Int(x, y, 0);
                TileBase current = floorTilemap.GetTile(position);

                // 걸어 다닐 수 있는 칸은 그대로 둔다.
                if (current != null
                    && TileRules.KindOfTile(current.name) == TileRules.TerrainKind.Floor)
                    continue;

                floorTilemap.SetTile(position, edge);
            }
        }
    }

    /// <summary>
    /// 맵 바깥으로 얼마나 더 두를 것인가(칸).
    ///
    /// 화면이 가로 18칸이라 절반이 9칸이고, 가장자리에 서면 그만큼이 맵 바깥이 된다.
    /// 창 비율에 따라(Pixel Perfect 의 Stretch Fill) 더 보일 수 있어 넉넉히 잡는다.
    /// 세로는 10칸이라 가로 기준이면 저절로 덮인다.
    /// </summary>
    private const int BorderMargin = 12;

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
