using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 서로 다른 타일이면서도 <b>이어진 것으로 보이는</b> Rule Tile.
///
/// ── 왜 필요한가 ────────────────────────────────────────────────────
/// 기본 RuleTile 은 이웃 칸이 "나와 같은 타일 애셋인가" 만 본다(RuleMatch).
/// 그래서 보통 벽과 맵 끝자락은 겉모습이 같아도 서로를 남으로 여기고,
/// 맞닿은 자리마다 벽이 끊긴 것처럼 테두리가 그려진다.
///
/// siblings 에 상대를 적어 두면 그 타일도 '나' 로 쳐서 자연스럽게 이어진다.
/// 양쪽 애셋에 서로를 넣어야 한다 — 한쪽만 넣으면 그쪽에서만 이어져 보인다.
///
/// ── 겉모습과 규칙은 별개다 ────────────────────────────────────────
/// 이 클래스는 <b>그리는 방법</b>만 정한다. 지나갈 수 있는지 · 허물 수 있는지는
/// 여전히 TileRules 가 타일 <b>이름</b>으로 정한다. 그래서 "물처럼 보이지만
/// 지나갈 수 없는 끝자락" 같은 것이 가능하다.
/// </summary>
[CreateAssetMenu(fileName = "NewSiblingRuleTile", menuName = "Tiles/Sibling Rule Tile")]
public class SiblingRuleTile : RuleTile<SiblingRuleTile.Neighbor>, ITerrainTile
{
    /// <summary>이어진 것으로 볼 타일들. 보통 벽 ↔ 맵 끝자락처럼 짝으로 넣는다.</summary>
    public List<TileBase> siblings = new List<TileBase>();

    [Header("게임 규칙")]

    /// <summary>
    /// 이 타일이 만드는 땅의 종류.
    ///
    /// 겉모습과 무관하게 여기서 정한 것이 규칙이 된다. 맵 끝자락의 물은
    /// 물처럼 보이지만 Wall 로 두어야 헤엄쳐 나가지 못한다(ITerrainTile 주석).
    /// </summary>
    public TileRules.TerrainKind terrain = TileRules.TerrainKind.Floor;

    /// <summary>끄면 어떤 수단으로도 없앨 수 없다. 맵 끝자락이 그렇다.</summary>
    public bool destructible = true;

    public TileRules.TerrainKind Terrain => terrain;
    public bool Destructible => destructible;

    public class Neighbor : RuleTile.TilingRuleOutput.Neighbor { }

    public override bool RuleMatch(int neighbor, TileBase other)
    {
        // RuleOverrideTile 로 감싼 것은 알맹이를 꺼내 본다. 기본 구현과 같은 처리다.
        if (other is RuleOverrideTile overrideTile)
            other = overrideTile.m_InstanceTile;

        switch (neighbor)
        {
            case TilingRuleOutput.Neighbor.This:
                return other == this || siblings.Contains(other);

            case TilingRuleOutput.Neighbor.NotThis:
                return other != this && !siblings.Contains(other);
        }

        return base.RuleMatch(neighbor, other);
    }
}
