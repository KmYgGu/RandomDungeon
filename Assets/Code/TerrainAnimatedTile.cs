using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 여러 장을 순서대로 재생하면서, 자기 지형도 스스로 아는 타일.
///
/// 물결이나 용암처럼 <b>움직이는 지형</b>에 쓴다. 2d-extras 의 AnimatedTile 은
/// 그림만 알고 지형을 모르므로, 그것만 쓰면 TileRules 가 이름으로 되짚어야 한다.
///
/// 규칙 타일(SiblingRuleTile)과 애니메이션 타일은 서로 다른 계보라 상속으로
/// 합칠 수 없다. 대신 둘 다 ITerrainTile 을 구현해서, TileRules 입장에서는
/// 똑같이 "자기 지형을 아는 타일" 로 보이게 했다.
/// </summary>
[CreateAssetMenu(fileName = "NewTerrainAnimatedTile", menuName = "Tiles/Terrain Animated Tile")]
public class TerrainAnimatedTile : AnimatedTile, ITerrainTile
{
    [Header("게임 규칙")]

    /// <summary>이 타일이 만드는 땅의 종류. 물결이면 Water, 용암이면 Lava.</summary>
    public TileRules.TerrainKind terrain = TileRules.TerrainKind.Floor;

    /// <summary>끄면 어떤 수단으로도 없앨 수 없다.</summary>
    public bool destructible = true;

    public TileRules.TerrainKind Terrain => terrain;
    public bool Destructible => destructible;
}
