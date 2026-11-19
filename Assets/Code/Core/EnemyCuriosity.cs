using System;
using UnityEngine;

/// <summary>원거리 피격 방향을 기억해 막힐 때까지 직진한다. 공격자의 현재 위치는 추적하지 않는다.</summary>
public sealed class EnemyCuriosity
{
    public Vector2 Direction { get; private set; }
    public bool Active => Direction != Vector2.zero;

    public bool TryStart(bool patrolling, int damage, int remainingHp,
        Vector3Int self, Vector3Int attackOrigin)
    {
        if (!patrolling || damage <= 0 || remainingHp <= 0) return false;
        Vector2 direction = MoveIntent.DirectionBetween(self, attackOrigin);
        if (direction == Vector2.zero) return false;
        Direction = direction;
        return true;
    }

    // 실제 이동 함수가 지형·생물·예약 칸을 함께 검사한다. 우회 후보는 만들지 않는다.
    public bool TryStep(Func<Vector2, bool> move)
    {
        if (!Active) return false;
        if (move(Direction)) return true;
        Stop();
        return false;
    }

    public void Stop() => Direction = Vector2.zero;
}
