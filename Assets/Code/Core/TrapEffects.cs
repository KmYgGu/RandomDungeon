using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함정이 작동했을 때 실제로 일어나는 일 중, 화면도 시간도 필요 없는 부분.
///
/// 연출과 실제 적용은 GameManager 가 코루틴으로 한다. 여기에는 "어디로 옮길
/// 것인가", "얼마나 아픈가" 처럼 답이 계산으로 정해지는 것만 둔다.
/// 그래야 Unity 없이 그대로 확인할 수 있다.
///
/// ── 여기는 칸 좌표로만 센다 ────────────────────────────────────────
/// 이 게임의 칸 중심은 월드에서 x+0.5 다(GridUtil.CellCenterOffset).
/// 그 값을 그대로 계산에 쓰면 "반 칸 어긋난다 / 한 칸 넘친다" 가 조용히 난다 —
/// 워프 목적지를 고르다 실제로 두 번 냈다. 그래서 규칙은 정수 칸으로만 세고,
/// 월드 좌표로 바꾸는 일은 부르는 쪽이 GridUtil.CellCenter 로 마지막에 한 번만 한다.
/// </summary>
public static class TrapEffects
{
    /// <summary>
    /// 구멍에 빠졌을 때의 피해. <b>방어력을 무시한다.</b>
    ///
    /// 구멍은 때에 따라 이득이 되는 함정이다(가고 싶은 층으로 공짜로 내려간다).
    /// 그래서 대가가 없으면 언제나 밟는 편이 낫고, 판단거리가 사라진다.
    /// 방어력으로 줄일 수 있게 하면 방어구를 갖춘 뒤에는 다시 공짜가 되므로
    /// 무시한다. 이 피해로 죽을 수도 있다.
    /// </summary>
    public const int HoleDamage = 5;

    /// <summary>독침에 찔렸을 때 깎이는 힘.</summary>
    public const int DartStrengthLoss = 1;

    /// <summary>날리기가 밀어내는 칸 수.</summary>
    public const int BlowDistance = 8;

    /// <summary>벽에 처박혔을 때의 피해. 구멍과 마찬가지로 방어력을 무시한다.</summary>
    public const int BlowWallDamage = 5;

    /// <summary>날려진 끝에 무슨 일이 일어나는가.</summary>
    public enum BlowOutcome
    {
        /// <summary>평범한 바닥에 떨어졌다. 자리만 옮겨진다.</summary>
        Landed,
        /// <summary>벽에 처박혔다. 방어력 무시 피해.</summary>
        HitWall,
        /// <summary>물에 빠졌다. 갇히지 않도록 강제로 워프된다.</summary>
        Water,
        /// <summary>용암에 빠졌다. 지금 체력의 절반을 잃고 워프된다.</summary>
        Lava,
        /// <summary>절벽으로 떨어졌다. 구멍 함정과 같은 처리.</summary>
        Pit,
    }

    /// <summary>
    /// 날려진 끝이 어떻게 되는가.
    ///
    /// ── 지형이 벽보다 우선이다 ────────────────────────────────────
    /// 벽에 부딪히면서 그 앞의 물에 빠질 수 있다. 그때 둘을 다 적용하면
    /// 벽 피해와 지형 처리가 겹쳐 갑자기 죽는 일이 생긴다. 빠진 쪽이
    /// 더 큰 일이므로 지형이 이긴다.
    ///
    /// 물과 용암이 워프로 이어지는 것은 <b>게임이 멈추지 않게 하기 위해서</b>다.
    /// 물에 들어갈 수 없는 상태로 물 한가운데에 놓이면 나올 길이 없다.
    ///
    /// Tilemap 도 GameObject 도 보지 않으므로 Unity 없이 그대로 확인할 수 있다.
    /// </summary>
    public static BlowOutcome ResolveBlow(TileRules.TerrainKind terrain, bool stoppedByWall)
    {
        switch (terrain)
        {
            case TileRules.TerrainKind.Water: return BlowOutcome.Water;
            case TileRules.TerrainKind.Lava: return BlowOutcome.Lava;
            case TileRules.TerrainKind.Pit: return BlowOutcome.Pit;
            default: return stoppedByWall ? BlowOutcome.HitWall : BlowOutcome.Landed;
        }
    }

    /// <summary>
    /// 용암에 빠졌을 때 잃는 체력. 지금 체력의 절반이다.
    ///
    /// 내림이라서 <b>이것만으로는 죽지 않는다.</b> 체력 1 에서 절반을 잃으면 0 이
    /// 되어야 하는데, 그러면 지나가다 밟은 함정 하나가 곧바로 판을 끝내 버린다.
    /// 용암은 아프게 하는 것이지 처형하는 것이 아니다.
    /// </summary>
    public static int LavaDamage(float currentHp) => Mathf.Max(0, Mathf.FloorToInt(currentHp / 2f));

    /// <summary>
    /// 워프가 데려갈 칸을 고른다. 고를 곳이 없으면 false.
    ///
    /// 규칙은 셋이다 —
    ///   1. 지금 있는 방이 아닌 곳을 고른다. 제자리 워프는 함정을 밟은 뜻이 없다.
    ///      방이 하나뿐이면 어쩔 수 없이 같은 방 안에서 옮긴다.
    ///   2. 누가 서 있는 칸은 고르지 않는다. 겹쳐 서면 둘 다 빠져나오지 못한다.
    ///   3. 방 가장자리(벽)가 아니라 안쪽 칸을 고른다. rooms 가 이미 안쪽 칸들이다.
    /// </summary>
    /// <param name="rooms">방들의 안쪽 칸(칸 좌표).</param>
    /// <param name="occupied">비켜야 할 칸들(적이 선 자리).</param>
    /// <param name="from">지금 있는 칸. 이 칸이 든 방은 뒤로 미룬다.</param>
    /// <param name="nextInt">min 이상 max 미만의 정수를 주는 것(무작위).</param>
    public static bool TryPickWarpDestination(
        IReadOnlyList<RectInt> rooms,
        IReadOnlyList<Vector3Int> occupied,
        Vector3Int from,
        Func<int, int, int> nextInt,
        out Vector3Int destination)
    {
        destination = from;
        if (rooms == null || rooms.Count == 0 || nextInt == null) return false;

        var here = new Vector2Int(from.x, from.y);

        // 지금 있는 방을 빼고 후보를 모은다. 남는 방이 없으면 전부를 후보로 쓴다.
        var candidates = new List<int>();
        for (int i = 0; i < rooms.Count; i++)
        {
            if (!rooms[i].Contains(here)) candidates.Add(i);
        }
        if (candidates.Count == 0)
        {
            for (int i = 0; i < rooms.Count; i++) candidates.Add(i);
        }

        // 고른 방에 설 자리가 없을 수 있으므로(적으로 가득 찬 방) 방을 바꿔 가며 본다.
        // 후보를 하나씩 빼면서 도므로 같은 방을 두 번 보지 않는다.
        while (candidates.Count > 0)
        {
            int pick = nextInt(0, candidates.Count);
            RectInt room = rooms[candidates[pick]];
            candidates.RemoveAt(pick);

            if (TryPickCellIn(room, occupied, nextInt, out destination)) return true;
        }

        return false;
    }

    /// <summary>
    /// 방 하나 안에서 아무도 없는 칸을 고른다.
    ///
    /// RectInt 는 xMax 가 마지막 칸의 <b>다음</b>이라 `x &lt; xMax` 로 세면
    /// 개수가 저절로 맞는다. 월드 좌표로 셀 때는 그 경계가 눈에 보이지 않아
    /// 한 칸 더 돌기 쉬웠고, 그래서 벽 안으로 워프하는 일이 있었다.
    /// </summary>
    private static bool TryPickCellIn(
        RectInt room, IReadOnlyList<Vector3Int> occupied, Func<int, int, int> nextInt,
        out Vector3Int cell)
    {
        cell = Vector3Int.zero;

        // 칸을 전부 모아 두고 그 안에서 고른다. 무작위로 찔러 보고 다시 뽑는
        // 방식은 빈 자리가 하나뿐일 때 언제 끝날지 알 수 없다.
        var free = new List<Vector3Int>();
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                var candidate = new Vector3Int(x, y, 0);
                if (!IsOccupied(candidate, occupied)) free.Add(candidate);
            }
        }

        if (free.Count == 0) return false;

        cell = free[nextInt(0, free.Count)];
        return true;
    }

    /// <summary>
    /// 그 칸에 누가 서 있는가.
    ///
    /// 칸 좌표라서 그냥 같은지 보면 된다. 월드 좌표로 하던 때는 소수점 때문에
    /// "얼마나 가까우면 같은 칸인가" 를 눈대중으로 정해 두어야 했다.
    /// </summary>
    private static bool IsOccupied(Vector3Int cell, IReadOnlyList<Vector3Int> occupied)
    {
        if (occupied == null) return false;

        foreach (Vector3Int taken in occupied)
        {
            if (taken.x == cell.x && taken.y == cell.y) return true;
        }
        return false;
    }
}
