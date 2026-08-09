using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원거리 판정. 어떤 지점에서 한 방향으로 날아가 무엇에 맞는지를 계산한다.
///
/// 던진 아이템이 지금 이것을 쓰고, 앞으로 화살·지팡이 같은 원거리 수단도
/// 같은 계산을 쓴다. 그래서 아이템에 대해서는 아무것도 모르도록 만들었다.
/// 대상은 그냥 '칸 목록' 이고, 맞은 대상은 인덱스로 돌려준다.
///
/// ── 이동과 다른 점 ────────────────────────────────────────────────
/// 이동에는 모서리 규칙이 있다. 벽 두 개가 대각선으로 맞물린 틈은
/// 몸으로는 빠져나갈 수 없다(TileRules.CanMoveDiagonally).
/// 하지만 투사체는 그 틈으로 날아간다.
///
///     # E #        상하좌우가 전부 벽이라 근접 공격은 어느 쪽으로도 안 되지만,
///     # . #        대각선 틈으로 아이템은 던질 수 있다.
///
/// 그래서 여기서는 모서리 규칙을 적용하지 않고, 날아가는 칸이 벽인지만 본다.
/// </summary>
public static class RangedShot
{
    /// <summary>맞을 대상이 없었음을 뜻하는 인덱스.</summary>
    public const int NoTarget = -1;

    public readonly struct Result
    {
        /// <summary>맞은 대상의 인덱스. 없으면 NoTarget.</summary>
        public readonly int TargetIndex;

        /// <summary>맞은 칸, 또는 아무것도 못 맞혔다면 멈춘 칸.</summary>
        public readonly Vector3Int ImpactCell;

        /// <summary>벽에 막혀 멈췄는가.</summary>
        public readonly bool StoppedByWall;

        /// <summary>실제로 날아간 칸 수.</summary>
        public readonly int TravelledCells;

        public bool HitTarget => TargetIndex != NoTarget;

        public Result(int targetIndex, Vector3Int impactCell, bool stoppedByWall, int travelledCells)
        {
            TargetIndex = targetIndex;
            ImpactCell = impactCell;
            StoppedByWall = stoppedByWall;
            TravelledCells = travelledCells;
        }
    }

    /// <summary>
    /// origin 에서 direction 쪽으로 최대 maxDistance 칸까지 날린다.
    /// 대상에 먼저 닿으면 거기서 멈추고, 벽을 만나면 벽 바로 앞 칸에서 멈춘다.
    /// </summary>
    public static Result Trace(
        IWalkableGrid grid,
        Vector3Int origin,
        Vector2Int direction,
        int maxDistance,
        IReadOnlyList<Vector3Int> targets)
    {
        if (direction == Vector2Int.zero)
            return new Result(NoTarget, origin, false, 0);

        Vector3Int current = origin;

        for (int travelled = 1; travelled <= maxDistance; travelled++)
        {
            var next = new Vector3Int(current.x + direction.x, current.y + direction.y, 0);

            // 벽에 막히면 바로 앞 칸에 떨어진다.
            // 모서리 규칙을 보지 않으므로 벽 사이 대각선 틈은 그대로 통과한다.
            if (grid.IsWall(next))
                return new Result(NoTarget, current, true, travelled - 1);

            int hit = IndexOfTargetAt(targets, next);
            if (hit != NoTarget)
                return new Result(hit, next, false, travelled);

            current = next;
        }

        return new Result(NoTarget, current, false, maxDistance);
    }

    private static int IndexOfTargetAt(IReadOnlyList<Vector3Int> targets, Vector3Int cell)
    {
        if (targets == null) return NoTarget;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == cell) return i;
        }
        return NoTarget;
    }
}
