using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 생성된 TileType[,] 격자를 하나의 Tilemap 에 그려 내는 클래스.
///
/// 던전을 '어떻게 만드는가'와 '어떻게 보이는가'를 나누는 경계다.
/// 생성 쪽은 TileType 만 알면 되고, 어떤 타일 애셋을 쓸지는 여기서만 정한다.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class DungeonRenderer : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap tilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase waterTile;

    /// <summary>grid 의 TileType 을 바탕으로 Tilemap 을 다시 그린다.</summary>
    public void Render(TileType[,] grid)
    {
        tilemap.ClearAllTiles();

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileBase tile = TileFor(grid[x, y]);
                if (tile != null)
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private TileBase TileFor(TileType type)
    {
        switch (type)
        {
            case TileType.Floor:
            case TileType.Roomfloor:
            case TileType.Corridor:
                return floorTile;
            case TileType.Wall:
                return wallTile;
            case TileType.Water:
                return waterTile;
            default:
                return null;
        }
    }
}
