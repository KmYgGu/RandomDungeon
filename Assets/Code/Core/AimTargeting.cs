using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조준 모드에 들어갈 때 어느 쪽을 겨눌지 고른다.
///
/// 겨누는 순서는 셋이다.
///   1순위 - 바로 옆(대각선 포함)에 붙어 있는 적          … Player 가 직접 본다
///   2순위 - 직선으로 겨눌 수 있는, 벽에 가리지 않은 적    … 여기
///   기본  - 지금 캐릭터가 바라보고 있는 쪽
///
/// ── '겨눌 수 있다' 의 기준 ────────────────────────────────────────
/// 조준은 여덟 방향으로만 할 수 있으므로, 적이 그 여덟 갈래 중 하나 위에
/// 정확히 놓여 있어야 한다. 그리고 그 사이가 벽으로 막혀 있지 않아야 한다.
///
/// 막혔는지 아닌지는 RangedShot 이 판단한다. 실제로 던지거나 쏠 때 쓰는 것과
/// 같은 계산이다. 이게 중요한데, 판정을 따로 만들면 "겨눠지는데 안 맞는 자리"
/// 나 "안 겨눠지는데 맞는 자리" 가 생긴다. 특히 벽이 대각선으로 맞물린 틈은
/// 몸은 못 지나가지만 투사체는 지나가므로(RangedShot 주석 참고), 조준만 따로
/// 계산했다면 그 틈이 어긋났을 것이다.
///
/// 같은 방에 있는 적은 방 안이 뚫려 있으니 저절로 이 조건을 만족한다.
/// 통로에서 일직선으로 마주 보는 적도 마찬가지다. 그래서 "같은 방이거나
/// 시야에 있는 적" 을 방 목록 없이도 이 한 가지 규칙으로 가릴 수 있다.
/// </summary>
public static class AimTargeting
{
    /// <summary>조준할 수 있는 여덟 방향.</summary>
    public static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    /// <summary>
    /// 직선으로 겨눌 수 있는 적 쪽 방향. 없으면 false.
    ///
    /// 여럿이면 가까운 쪽을 고른다. 거리가 같으면 지금 바라보는 쪽에 가까운 방향을,
    /// 그것도 같으면 대각선보다 직선을 고른다. 1순위(붙어 있는 적)의 기준과 같다.
    /// </summary>
    public static bool TryFindInLine(
        IWalkableGrid grid,
        Vector3Int origin,
        IReadOnlyList<Vector3Int> enemies,
        Vector2Int facing,
        int maxDistance,
        out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        if (grid == null || enemies == null || enemies.Count == 0 || maxDistance <= 0)
            return false;

        int bestDistance = int.MaxValue;
        float bestScore = float.NegativeInfinity;

        foreach (Vector2Int candidate in Directions)
        {
            RangedShot.Result shot = RangedShot.Trace(grid, origin, candidate, maxDistance, enemies);
            if (!shot.HitTarget) continue;

            // 더 먼 적은 볼 것도 없다.
            if (shot.TravelledCells > bestDistance) continue;

            float score = ScoreOf(candidate, facing);

            // 거리가 같을 때만 방향 취향으로 가른다.
            if (shot.TravelledCells == bestDistance && score <= bestScore) continue;

            bestDistance = shot.TravelledCells;
            bestScore = score;
            direction = candidate;
        }

        return direction != Vector2Int.zero;
    }

    /// <summary>
    /// 겨눌 수 있는 방향을 전부 모은다. 조준을 다음 적으로 옮길 때 쓴다.
    ///
    /// Directions 의 순서대로 담기므로 결과가 늘 같다. 조준을 옮기는 순서가
    /// 누를 때마다 달라지면 손이 기억하지 못한다.
    /// </summary>
    public static List<Vector2Int> CollectAimable(
        IWalkableGrid grid,
        Vector3Int origin,
        IReadOnlyList<Vector3Int> enemies,
        int maxDistance)
    {
        var found = new List<Vector2Int>();

        if (grid == null || enemies == null || enemies.Count == 0 || maxDistance <= 0)
            return found;

        foreach (Vector2Int candidate in Directions)
        {
            if (RangedShot.Trace(grid, origin, candidate, maxDistance, enemies).HitTarget)
                found.Add(candidate);
        }

        return found;
    }

    /// <summary>
    /// 지금 겨누고 있는 방향 다음으로 옮길 방향. 옮길 곳이 없으면 false.
    ///
    /// 지금 방향이 목록에 없으면(적이 없는 허공을 겨누고 있었으면) 첫 번째로 간다.
    /// 겨눌 수 있는 적이 하나뿐이면 옮기지 않는다 — 같은 곳을 다시 겨누느라
    /// 버튼만 먹는 것보다, 아무 일도 일어나지 않는 편이 낫다.
    /// </summary>
    public static bool TryCycle(
        IReadOnlyList<Vector2Int> aimable,
        Vector2Int current,
        out Vector2Int next)
    {
        next = current;

        if (aimable == null || aimable.Count == 0) return false;

        int index = -1;
        for (int i = 0; i < aimable.Count; i++)
        {
            if (aimable[i] == current) { index = i; break; }
        }

        if (index < 0)
        {
            next = aimable[0];
            return true;
        }

        if (aimable.Count == 1) return false;

        next = aimable[(index + 1) % aimable.Count];
        return true;
    }

    /// <summary>지금 바라보는 쪽과 얼마나 가까운 방향인가. 직선이면 조금 더 쳐 준다.</summary>
    private static float ScoreOf(Vector2Int candidate, Vector2Int facing)
    {
        float score = 0f;

        if (facing != Vector2Int.zero)
        {
            Vector2 a = new Vector2(candidate.x, candidate.y).normalized;
            Vector2 b = new Vector2(facing.x, facing.y).normalized;
            score = Vector2.Dot(a, b);
        }

        // 점수가 같을 때 직선 쪽을 먼저 겨눈다.
        if (candidate.x == 0 || candidate.y == 0) score += 0.01f;

        return score;
    }
}
