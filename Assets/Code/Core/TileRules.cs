using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// "이 칸을 지나갈 수 있는가" 에 대한 유일한 판정 기준.
///
/// 예전에는 같은 내용의 IsBlockedByTile 이 MovingObject 와 GameManager 에
/// 각각 복사되어 있었고, 막는 타일 이름 목록도 세 군데(MovingObject.Start,
/// GameManager 필드 초기화)에 흩어져 있었다.
/// 타일을 하나 추가할 때 한 군데만 고치면 되도록 여기로 모았다.
/// </summary>
public static class TileRules
{
    /// <summary>누구도 통과할 수 없는 타일(벽).</summary>
    public static readonly List<string> ImpassableTileNames = new List<string> { "S1Wall" };

    /// <summary>특수 상태가 아니면 통과할 수 없는 타일(물).</summary>
    public static readonly List<string> WaterTileNames = new List<string> { "S1Water" };

    /// <summary>
    /// 닿은 것을 태워 없애는 타일(용암).
    ///
    /// 아직 이 이름의 타일 애셋은 없다. 지형별 처리를 먼저 만들어 두었으니,
    /// 타일을 만들 때 이름을 여기 적힌 것으로 두거나 이 목록에 더하면 곧바로 동작한다.
    /// </summary>
    public static readonly List<string> LavaTileNames = new List<string> { "S1Lava" };

    /// <summary>
    /// 떨어진 것이 되돌아오지 않는 타일(구멍 · 절벽).
    /// 용암과 마찬가지로 아직 애셋은 없다.
    /// </summary>
    public static readonly List<string> PitTileNames = new List<string> { "S1Pit" };

    /// <summary>땅의 종류. 무엇이 떨어졌을 때 어떻게 되는지가 여기서 갈린다.</summary>
    public enum TerrainKind
    {
        /// <summary>평범한 바닥. 타일이 아예 없는 칸도 여기로 본다(기존 판정과 맞춘다).</summary>
        Floor,
        Wall,
        Water,
        Lava,
        Pit,
    }

    /// <summary>
    /// 이 자리가 어떤 땅인가.
    ///
    /// 이름 목록을 여기저기서 따로 뒤지면 "물인지 묻는 곳" 과 "용암인지 묻는 곳" 이
    /// 서로 다른 답을 낼 수 있다. 판정을 한 곳으로 모아 둔다.
    /// </summary>
    public static TerrainKind KindAt(Tilemap tilemap, Vector3 worldPosition)
        => KindOfTile(TileNameAt(tilemap, worldPosition));

    /// <summary>
    /// 타일 이름만 보고 땅의 종류를 가른다.
    ///
    /// Tilemap 을 받지 않으므로 Unity 없이 그대로 확인할 수 있다.
    /// 이름 목록을 고쳤을 때 어디가 어떻게 갈리는지가 검사로 남는다.
    /// </summary>
    public static TerrainKind KindOfTile(string tileName)
    {
        if (tileName == null) return TerrainKind.Floor;

        if (ImpassableTileNames.Contains(tileName)) return TerrainKind.Wall;
        if (WaterTileNames.Contains(tileName)) return TerrainKind.Water;
        if (LavaTileNames.Contains(tileName)) return TerrainKind.Lava;
        if (PitTileNames.Contains(tileName)) return TerrainKind.Pit;

        return TerrainKind.Floor;
    }

    private static string TileNameAt(Tilemap tilemap, Vector3 worldPosition)
    {
        if (tilemap == null) return null;
        TileBase tile = tilemap.GetTile(tilemap.WorldToCell(worldPosition));
        return tile != null ? tile.name : null;
    }

    /// <summary>해당 위치가 벽인가.</summary>
    public static bool IsImpassable(Tilemap tilemap, Vector3 worldPosition)
        => KindAt(tilemap, worldPosition) == TerrainKind.Wall;

    /// <summary>
    /// 격자 좌표로 직접 묻는 통행 가능 여부. 길찾기처럼 칸 단위로 훑을 때 쓴다.
    /// 타일이 아예 없는 칸(맵 밖)은 갈 수 없는 것으로 본다.
    /// </summary>
    public static bool IsWalkableCell(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null) return false;

        TileBase tile = tilemap.GetTile(cell);
        if (tile == null) return false;

        return !ImpassableTileNames.Contains(tile.name)
            && !WaterTileNames.Contains(tile.name);
    }

    /// <summary>격자 좌표로 묻는 벽 여부. 대각선 통과 판정에 쓴다.</summary>
    public static bool IsImpassableCell(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null) return false;

        TileBase tile = tilemap.GetTile(cell);
        return tile != null && ImpassableTileNames.Contains(tile.name);
    }

    /// <summary>해당 위치가 물인가.</summary>
    public static bool IsWater(Tilemap tilemap, Vector3 worldPosition)
        => KindAt(tilemap, worldPosition) == TerrainKind.Water;

    /// <summary>
    /// 특정 상태의 대상이 해당 위치로 들어갈 수 있는가.
    /// 물은 isSpecialState 인 대상만 지나갈 수 있다.
    /// </summary>
    public static bool CanEnter(Tilemap tilemap, Vector3 worldPosition, bool isSpecialState)
    {
        string name = TileNameAt(tilemap, worldPosition);
        if (name == null) return true;
        if (ImpassableTileNames.Contains(name)) return false;
        if (WaterTileNames.Contains(name) && !isSpecialState) return false;
        return true;
    }

    private static readonly Vector2[] Cardinals =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
    };

    /// <summary>
    /// 상하좌우 중 들어갈 수 있는 칸이 몇 개인지 센다. exclude 방향은 세지 않는다.
    ///
    /// 달리기가 통로에서 갈림길을 만났는지 판단할 때 쓴다.
    /// 왔던 방향을 빼고 갈 수 있는 곳이 둘 이상이면 갈림길이다.
    /// </summary>
    public static int CountOpenDirections(
        Tilemap tilemap, Vector2 position, bool isSpecialState, Vector2 exclude)
    {
        int open = 0;

        foreach (Vector2 direction in Cardinals)
        {
            if (direction == exclude) continue;

            Vector2 neighbour = position + direction;

            // 맵 밖(타일이 아예 없는 곳)은 길로 세지 않는다.
            // CanEnter 는 타일이 없으면 통과 가능으로 보는데, 그 규칙을 그대로 쓰면
            // 맵 가장자리의 통로가 늘 갈림길로 잘못 잡힌다.
            if (TileNameAt(tilemap, neighbour) == null) continue;
            if (!CanEnter(tilemap, neighbour, isSpecialState)) continue;

            open++;
        }

        return open;
    }

    /// <summary>
    /// 대각선 이동 가능 여부. 두 벽 사이의 틈으로 비스듬히 빠져나가는 것을 막는다.
    /// </summary>
    public static bool CanMoveDiagonally(Tilemap tilemap, Vector2 start, Vector2 direction)
    {
        if (tilemap == null) return true;

        return !IsImpassable(tilemap, start + new Vector2(direction.x, 0f))
            && !IsImpassable(tilemap, start + new Vector2(0f, direction.y));
    }
}
