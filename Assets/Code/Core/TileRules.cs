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

    /// <summary>
    /// 맵의 끝자락. 겉모습은 보통 벽과 같지만 <b>어떤 수단으로도 없앨 수 없다.</b>
    ///
    /// 벽을 허무는 수단(굴착 지팡이 같은 것)이 생겼을 때 맵 바깥으로 길이 뚫리면
    /// 플레이어가 타일이 없는 곳으로 나가 게임이 진행 불가능해진다. 날려지는 것도
    /// 마찬가지라서, 가장자리 한 줄은 처음부터 허물 수 없는 것으로 둔다.
    ///
    /// 통행 판정에서는 보통 벽과 똑같이 다룬다(KindOfTile 이 Wall 로 답한다).
    /// 다른 것은 IsDestructible 하나뿐이다.
    ///
    /// 아직 이 이름의 타일 애셋은 없다. 만들 때 이름을 여기 적힌 것으로 두거나
    /// 이 목록에 더하면 곧바로 동작한다. 그때까지는 보통 벽으로 칠해진다.
    /// </summary>
    /// <remarks>
    /// 겉모습이 여럿일 수 있다. 물로 가득한 층에서는 벽 모양 끝자락이 혼자 튀므로
    /// 물처럼 보이는 것을 따로 둔다. <b>둘 다 지나갈 수 없고 허물 수 없다</b> —
    /// 겉모습만 다르다. 물처럼 보인다고 통행까지 물의 규칙을 따르면, 물에 들어갈 수
    /// 있는 상태로 헤엄쳐 맵 밖으로 나가 버린다.
    /// </remarks>
    public static readonly List<string> BedrockTileNames =
        new List<string> { "S1Bedrock", "S1BedrockWater" };

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
    /// 판정을 한 곳으로 모아 둔다. 이름 목록을 여기저기서 따로 뒤지면
    /// "물인지 묻는 곳" 과 "용암인지 묻는 곳" 이 서로 다른 답을 낼 수 있다.
    /// </summary>
    public static TerrainKind KindAt(Tilemap tilemap, Vector3 worldPosition)
        => KindOf(TileAt(tilemap, worldPosition));

    /// <summary>
    /// 타일 애셋에게 직접 묻는다. 애셋이 답하지 않으면 이름으로 가른다.
    ///
    /// Rule Override Tile 은 바탕 타일의 그림만 갈아 끼운 것이라 지형도 바탕을
    /// 따라간다. <b>2스테이지 벽을 Override 로 만들면 코드를 한 줄도 고치지 않고
    /// 벽으로 동작한다</b> — 이 방식으로 옮긴 가장 큰 이유다.
    /// </summary>
    public static TerrainKind KindOf(TileBase tile)
        => Resolve(TerrainOf(tile), tile != null ? tile.name : null);

    /// <summary>이 타일을 없앨 수 있는가. 맵 끝자락만 false 다.</summary>
    public static bool IsDestructible(TileBase tile)
    {
        ITerrainTile terrain = TerrainOf(tile);
        if (terrain != null) return terrain.Destructible;

        return IsDestructible(tile != null ? tile.name : null);
    }

    /// <summary>
    /// 애셋이 답한 것과 이름으로 가른 것 중 무엇을 쓸 것인가.
    ///
    /// 애셋이 언제나 이긴다. 이름은 아직 옮기지 않은 타일을 위한 뒷길이라,
    /// 전부 옮기고 나면 이름 목록은 지워도 된다.
    ///
    /// Tilemap 을 받지 않으므로 Unity 없이 그대로 확인할 수 있다.
    /// </summary>
    public static TerrainKind Resolve(ITerrainTile asset, string tileName)
        => asset != null ? asset.Terrain : KindOfTile(tileName);

    /// <summary>
    /// 이 타일이 지형을 스스로 아는가. 모르면 null.
    ///
    /// Rule Override Tile 은 자기 자신이 아니라 <b>바탕 타일</b>에게 묻는다.
    /// </summary>
    private static ITerrainTile TerrainOf(TileBase tile)
    {
        switch (tile)
        {
            case null: return null;
            case ITerrainTile terrain: return terrain;
            case RuleOverrideTile overrideTile: return overrideTile.m_Tile as ITerrainTile;
            default: return null;
        }
    }

    /// <summary>
    /// 타일 이름만 보고 땅의 종류를 가른다. <b>아직 옮기지 않은 타일용 뒷길이다.</b>
    ///
    /// 새 타일은 이 목록에 더하지 말고 애셋에서 종류를 고를 것
    /// (SiblingRuleTile 의 Terrain 칸). 이름으로 가르면 스테이지가 늘 때마다
    /// 목록 넷을 손봐야 하고, 한 번 빠뜨리면 통과되는 벽이 조용히 생긴다.
    ///
    /// Tilemap 을 받지 않으므로 Unity 없이 그대로 확인할 수 있다.
    /// </summary>
    public static TerrainKind KindOfTile(string tileName)
    {
        if (tileName == null) return TerrainKind.Floor;

        // 끝자락도 통행 판정에서는 벽이다. 벽으로 답해야 길찾기 · 대각선 판정 ·
        // 날아가는 것이 멈추는 규칙이 전부 그대로 적용된다.
        if (ImpassableTileNames.Contains(tileName)) return TerrainKind.Wall;
        if (BedrockTileNames.Contains(tileName)) return TerrainKind.Wall;
        if (WaterTileNames.Contains(tileName)) return TerrainKind.Water;
        if (LavaTileNames.Contains(tileName)) return TerrainKind.Lava;
        if (PitTileNames.Contains(tileName)) return TerrainKind.Pit;

        return TerrainKind.Floor;
    }

    /// <summary>이름으로 묻는 허물기 여부. 위와 같은 뒷길이다.</summary>
    public static bool IsDestructible(string tileName)
        => tileName == null || !BedrockTileNames.Contains(tileName);

    /// <summary>해당 위치의 타일을 없앨 수 있는가.</summary>
    public static bool IsDestructible(Tilemap tilemap, Vector3 worldPosition)
        => IsDestructible(TileAt(tilemap, worldPosition));

    private static TileBase TileAt(Tilemap tilemap, Vector3 worldPosition)
        => tilemap != null ? tilemap.GetTile(tilemap.WorldToCell(worldPosition)) : null;

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

        TerrainKind kind = KindOf(tile);
        return kind != TerrainKind.Wall && kind != TerrainKind.Water;
    }

    /// <summary>격자 좌표로 묻는 벽 여부. 대각선 통과 판정에 쓴다.</summary>
    public static bool IsImpassableCell(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null) return false;

        TileBase tile = tilemap.GetTile(cell);
        return tile != null && KindOf(tile) == TerrainKind.Wall;
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
        switch (KindAt(tilemap, worldPosition))
        {
            case TerrainKind.Wall: return false;

            // 물은 특수 상태인 대상만 지나간다. 맵 끝자락의 물은 겉모습만 물이고
            // 종류가 Wall 이라 위에서 이미 걸러진다 — 헤엄쳐 맵 밖으로 나갈 수 없다.
            case TerrainKind.Water: return isSpecialState;

            default: return true;
        }
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
            if (TileAt(tilemap, neighbour) == null) continue;
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

    /// <summary>
    /// 근접 공격이 닿는가.
    ///
    /// 근접은 몸이 닿아야 하므로 이동과 같은 제약을 받는다. 벽 너머는 때릴 수 없고,
    /// 벽 두 개가 맞물린 대각선 틈으로도 손이 들어가지 않는다.
    /// <b>던진 아이템 같은 원거리는 이 제약을 받지 않는다</b>(RangedShot 참고).
    ///
    /// ── 왜 여기 있나 ──────────────────────────────────────────────────
    /// 원래 Player 안에만 있었다. 동료가 생기면서 같은 규칙이 필요해졌는데,
    /// 그때 동료 쪽에 따로 판정을 만들었더니 규칙이 둘이 되었다. 하나만 고치면
    /// 플레이어와 동료가 다르게 싸우게 되고, 그건 화면에서 설명할 수가 없다.
    ///
    /// 원거리 무기를 든 동료가 생기면 이 규칙을 <b>안 쓰면 된다</b> — 예외를
    /// 파는 것이 아니라 무기에 맞는 규칙(RangedShot)을 쓰는 것이다.
    /// </summary>
    public static bool CanReachWithMelee(Tilemap tilemap, Vector3 origin, Vector2 direction)
    {
        if (IsImpassable(tilemap, origin + (Vector3)direction)) return false;

        bool diagonal = direction.x != 0f && direction.y != 0f;
        return !diagonal || CanMoveDiagonally(tilemap, origin, direction);
    }
}
