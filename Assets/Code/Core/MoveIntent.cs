using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "어디로 가고 싶은가" 를 <b>시도할 방향의 차례</b>로 바꾼다.
/// 적과 동료가 함께 쓴다.
///
/// ── 왜 방향 목록인가 ──────────────────────────────────────────────
/// 갈 수 있는지는 부르는 쪽만 안다(벽 · 물 · 다른 생물 · 예약된 칸). 그래서
/// 여기서는 <b>가고 싶은 차례만</b> 내놓고, 실제로 갈 수 있는 첫 방향을 고르는
/// 일은 부르는 쪽이 한다. 적의 TryMoveAlong 이 이미 그렇게 되어 있다.
///
/// 한 칸만 보고 고르면(가장 가까워지는 이웃) 벽 앞에서 <b>갈 곳이 없다</b>가
/// 되어 그 자리에 선다. 차례로 물러서면 벽을 끼고도 계속 나아간다 —
/// 적이 벽이 많은 지형에서도 잘 도는 이유가 이것이다.
///
/// ── 앞으로 늘어날 자리 ────────────────────────────────────────────
/// 도망치기 · 거리 벌리기 · 아이템에게 다가가기가 전부 "어느 쪽으로 가고
/// 싶은가" 의 차이일 뿐이다. 모드를 늘릴 때는 여기 메서드를 하나 더한다.
/// 적이든 동료든 같은 것을 쓴다.
///
/// Unity 의 Vector2 만 쓰고 씬을 건드리지 않으므로 Unity 없이 확인할 수 있다.
/// </summary>
public static class MoveIntent
{
    /// <summary>
    /// 목표 쪽으로. 막히면 차례로 물러선다.
    ///
    /// 적의 CandidateDirections 를 그대로 옮긴 것이다 —
    ///   대각선이면 → 가로만 → 세로만
    ///   축 방향이면 → 수직으로 비켜 → 반대쪽 수직 → 뒤로
    ///
    /// 뒤로 물러서는 것까지 넣어 두는 이유는, 막다른 골목에서 아무 방향도
    /// 못 고르면 영영 그 자리에 서 있기 때문이다.
    /// </summary>
    public static IEnumerable<Vector2> Toward(Vector2 preferred)
    {
        if (preferred == Vector2.zero) yield break;
        yield return preferred;

        if (preferred.x != 0 && preferred.y != 0)
        {
            yield return new Vector2(preferred.x, 0);
            yield return new Vector2(0, preferred.y);
            yield break;
        }

        var perpendicular = new Vector2(preferred.y, preferred.x);
        yield return perpendicular;
        yield return -perpendicular;
        yield return -preferred;
    }

    /// <summary>
    /// 목표 칸을 향하는 방향의 차례. 같은 칸이면 아무것도 내놓지 않는다.
    /// </summary>
    public static IEnumerable<Vector2> Toward(Vector3Int self, Vector3Int goal)
    {
        Vector2 preferred = DirectionBetween(self, goal);
        if (preferred == Vector2.zero) yield break;

        foreach (Vector2 direction in Toward(preferred)) yield return direction;
    }

    /// <summary>
    /// <b>무조건 도망치기.</b> 위협의 반대쪽으로, 막히면 옆으로 새 본다.
    ///
    /// 반대 방향 하나만 보면 등 뒤가 벽일 때 그대로 붙잡힌다. 옆으로라도
    /// 벌어지는 길이 있으면 그쪽이 낫다 — 도망은 방향보다 거리가 중요하다.
    /// </summary>
    public static IEnumerable<Vector2> AwayFrom(Vector3Int self, Vector3Int threat)
    {
        Vector2 toward = DirectionBetween(self, threat);
        if (toward == Vector2.zero) yield break;

        foreach (Vector2 direction in Toward(-toward)) yield return direction;
    }

    /// <summary>
    /// <b>거리 벌리기.</b> 정해진 걸음 수를 사이에 두려 한다.
    ///
    /// 가까우면 물러나고 멀면 다가간다. 딱 맞으면 아무것도 내놓지 않는다 —
    /// 부르는 쪽은 그것을 "제자리" 로 다룬다. 활을 든 적처럼 <b>붙지도
    /// 떨어지지도 않으려는</b> 것들이 쓸 자리다.
    /// </summary>
    public static IEnumerable<Vector2> KeepDistance(Vector3Int self, Vector3Int other, int range)
    {
        int distance = Distance(self, other);

        if (distance < range)
        {
            foreach (Vector2 direction in AwayFrom(self, other)) yield return direction;
            yield break;
        }

        if (distance > range)
        {
            foreach (Vector2 direction in Toward(self, other)) yield return direction;
        }
    }

    /// <summary>두 칸 사이의 한 걸음짜리 방향(-1 · 0 · 1).</summary>
    public static Vector2 DirectionBetween(Vector3Int from, Vector3Int to)
        => new Vector2(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));

    /// <summary>대각선을 한 걸음으로 세는 거리. 이 게임의 이동이 여덟 방향이라 그렇다.</summary>
    public static int Distance(Vector3Int a, Vector3Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
