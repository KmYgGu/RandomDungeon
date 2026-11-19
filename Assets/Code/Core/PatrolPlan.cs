using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순찰의 <b>목표 고르기</b>. 적과 동료가 함께 쓴다.
///
/// ── 적이 하던 방식 그대로다 ───────────────────────────────────────
/// 지금 서 있는 <b>방의 문만</b> 후보로 본다. 층 전체의 문에서 아무거나 고르면
/// 목표가 벽 너머 저편일 수 있는데, 길찾기 없이 한 걸음씩 다가가는 방식으로는
/// 그런 목표에 닿을 수가 없다. 실제로 동료를 그렇게 만들었더니 벽 앞에서
/// 목표만 갈아 끼웠다.
///
/// <b>이미 다녀온 문은 뺀다.</b> 그러지 않으면 두 문 사이를 오간다.
/// 후보가 다 떨어지면 기억을 지우고 처음부터 다시 돈다.
///
/// 통로 한가운데면 고를 수 없다. 방을 벗어나 있는 동안은 <b>목표 없이</b>
/// 걷다가, 방에 들어서면 그때 새로 고른다.
///
/// Unity 것을 계산에 쓰지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class PatrolPlan
{
    /// <summary>
    /// 방 경계 밖으로 이만큼 더 살펴본다.
    ///
    /// 문은 방의 <b>벽 위</b>에 있어서 방 영역에 딱 맞추면 하나도 안 걸린다.
    /// 적이 쓰던 값 그대로다.
    /// </summary>
    public const int SearchMargin = 1;

    /// <summary>지금 서 있는 방. 통로면 Rect.zero.</summary>
    public static Rect RoomContaining(IReadOnlyList<Rect> rooms, Vector2 position)
    {
        if (rooms == null) return Rect.zero;

        foreach (Rect room in rooms)
        {
            if (room.Contains(position)) return room;
        }

        return Rect.zero;
    }

    /// <summary>
    /// 다음에 가 볼 문. 고를 수 없으면 false.
    /// </summary>
    /// <param name="visited">이미 다녀온 문들. 후보가 없으면 <b>여기서 비운다.</b></param>
    /// <param name="roll">0 이상 1 이하의 무작위 수.</param>
    public static bool TryPickTarget(
        Vector2 self,
        IReadOnlyList<Rect> rooms,
        IReadOnlyList<Vector3> exits,
        List<Vector2> visited,
        float roll,
        out Vector3 target,
        ISet<Vector2> excluded = null)
    {
        target = default;

        Rect room = RoomContaining(rooms, self);
        if (room == Rect.zero) return false;          // 통로 한가운데
        if (exits == null || exits.Count == 0) return false;

        var area = new Rect(
            room.xMin - SearchMargin,
            room.yMin - SearchMargin,
            room.width + SearchMargin * 2,
            room.height + SearchMargin * 2);

        List<Vector3> candidates = Collect(area, exits, visited, excluded);

        // 이 방의 문을 다 돌았다. 기억을 지우고 다시 돈다 — 그러지 않으면
        // 방에 갇힌 채 영영 목표를 못 고른다.
        if (candidates.Count == 0)
        {
            visited?.Clear();
            candidates = Collect(area, exits, null, excluded);
        }

        if (candidates.Count == 0) return false;

        int picked = Mathf.Clamp(Mathf.FloorToInt(roll * candidates.Count), 0, candidates.Count - 1);
        target = candidates[picked];
        return true;
    }

    private static List<Vector3> Collect(
        Rect area, IReadOnlyList<Vector3> exits, List<Vector2> visited, ISet<Vector2> excluded)
    {
        var result = new List<Vector3>();

        foreach (Vector3 point in exits)
        {
            var flat = new Vector2(point.x, point.y);
            if (!area.Contains(flat)) continue;
            if (excluded != null && excluded.Contains(flat)) continue;
            if (visited != null && visited.Contains(flat)) continue;

            result.Add(point);
        }

        return result;
    }
}
