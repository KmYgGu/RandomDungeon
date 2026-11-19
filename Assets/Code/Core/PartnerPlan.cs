using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>동료가 이번 턴에 할 일.</summary>
public enum PartnerAction
{
    /// <summary>제자리. 플레이어 곁에 이미 붙어 있고 적도 없다.</summary>
    Stay,
    /// <summary>Target 칸으로 한 걸음.</summary>
    Move,
    /// <summary>Target 칸의 적을 때린다.</summary>
    Attack,
}

/// <summary>말을 걸었을 때 동료가 하는 말.</summary>
public enum PartnerTalk
{
    /// <summary>지금 체력이 어떤지. 숫자가 아니라 말투로 드러난다.</summary>
    Hp,
    /// <summary>특기를 청한다. 드물게 나오고, 쓰고 나면 한동안 못 청한다.</summary>
    Action,
    /// <summary>다음 레벨까지 얼마나 남았는지.</summary>
    Exp,
}

/// <summary>동료 하나의 이번 턴 결정.</summary>
public struct PartnerTurn
{
    public PartnerAction Action;
    public Vector3Int Target;

    public static PartnerTurn Stay() => new PartnerTurn { Action = PartnerAction.Stay };

    public static PartnerTurn MoveTo(Vector3Int cell)
        => new PartnerTurn { Action = PartnerAction.Move, Target = cell };

    public static PartnerTurn AttackAt(Vector3Int cell)
        => new PartnerTurn { Action = PartnerAction.Attack, Target = cell };
}

/// <summary>
/// 동료가 이번 턴에 무엇을 할지 정한다. <b>실제로 움직이지는 않는다.</b>
///
/// 판정만 여기 두는 것은 Unity 없이 확인하기 위해서다. 적 AI(Enemy)는 판정과
/// 이동이 한 클래스에 섞여 있어 에디터를 켜야만 확인할 수 있었고, 실제로
/// "논리는 갈 수 있다는데 물리가 막는" 버그를 두 번 냈다. 같은 길을 가지 않는다.
/// (적도 나중에 같은 나눔으로 옮겼다 — EnemyPlan.)
///
/// ── 차례 ──────────────────────────────────────────────────────────
///   1. 붙어 있는 적이 있으면 때린다. 플레이어와도 붙어 있는 적을 먼저 친다(협공).
///   2. 가까이 적이 있으면 그쪽으로 한 걸음.
///   3. 플레이어와 멀어졌으면 플레이어 쪽으로 한 걸음.
///   4. 아니면 제자리.
///
/// 3번이 2번보다 뒤인 것이 중요하다. 반대로 두면 동료가 플레이어 곁을 지키느라
/// 눈앞의 적을 지나친다 — 그러면 데리고 다닐 이유가 없어진다.
/// </summary>
public static class PartnerPlan
{
    /// <summary>
    /// 플레이어와 이 걸음 수 안이면 굳이 더 붙지 않는다.
    ///
    /// <b>1 이어야 한다.</b> 2 로 두었더니 간격이 늘 두 칸으로 벌어졌다 — 한 칸
    /// 물러난 자리에서 멈추므로, 플레이어가 걸을 때마다 따라붙었다 멈췄다 하며
    /// 뒤처지는 것처럼 보인다. 붙어 있는 상태(1)를 기준으로 삼아야 플레이어가
    /// 한 걸음 뗄 때마다 동료도 한 걸음 뗀다.
    ///
    /// 0 으로 두면 안 된다. 플레이어 칸에는 들어갈 수 없으므로 영영 만족하지
    /// 못하고 매 턴 자리를 바꿔 가며 서성인다.
    /// </summary>
    public const int FollowDistance = 1;

    /// <summary>이 걸음 수 안의 적에게는 먼저 달려든다.</summary>
    public const int EnemySeekDistance = 5;

    /// <summary>
    /// 이 걸음 수보다 멀어지면 따라가기를 그만두고 탐색으로 돌아간다.
    ///
    /// 적의 EnemyPlan.ChaseGiveUpDistance(20)와 같은 뜻이지만 <b>더 짧다.</b> 동료는 붙어
    /// 다니는 것이 기본이라 이만큼 벌어졌다면 이미 놓친 것이고, 길게 잡으면
    /// 워프로 맵 반대편에 떨어져도 지도를 보고 정확히 찾아온다 — 그게
    /// 개연성 없어 보인다는 것이 이 규칙을 만든 이유다.
    /// </summary>
    public const int FollowGiveUpDistance = 8;

    /// <summary>
    /// 지금도 플레이어를 따라가는 중인가.
    ///
    /// ── 왜 '보이는가' 만으로 정하지 않는가 ───────────────────────────
    /// 통로에서는 둘 다 어느 방에도 없어서 <b>바로 옆에 있어도 안 보인다</b>고
    /// 나온다. 그것만으로 정하면 통로에 들어서는 순간 동료가 따라오기를
    /// 멈춘다. 그래서 적과 같은 구조를 쓴다 — <b>한 번 만나면 계속 따라가고,
    /// 너무 멀어지거나 갈 수 없게 되면 그만둔다.</b>
    ///
    /// 워프처럼 순식간에 멀어지는 경우가 이 규칙이 실제로 걸리는 자리다.
    /// </summary>
    /// <param name="wasFollowing">직전까지 따라가고 있었는가.</param>
    /// <param name="playerVisible">지금 플레이어가 보이는가(같은 방에 있는가).</param>
    /// <param name="stepsToPlayer">플레이어까지의 걸음 수. 갈 수 없으면 음수.</param>
    public static bool KeepsFollowing(bool wasFollowing, bool playerVisible, int stepsToPlayer)
    {
        // 보이면 무조건 따라간다. 처음 만나는 순간이 여기다.
        if (playerVisible) return true;

        if (!wasFollowing) return false;

        // 안 보여도 따라가던 중이면 이어 간다 — 통로에서 끊기지 않게.
        // 다만 갈 수 없게 됐거나 너무 멀어졌으면 놓친 것으로 본다.
        if (stepsToPlayer < 0) return false;

        return stepsToPlayer <= FollowGiveUpDistance;
    }

    // ── 말을 걸었을 때 무슨 말을 하는가 ────────────────────────────────────
    //
    // 셋을 한꺼번에 늘어놓지 않고 <b>하나만 고른다.</b> 매번 같은 세 줄이 뜨면
    // 읽지 않게 되고, 그러면 대사를 적어 둔 뜻이 없어진다.
    //
    // 특기를 청하는 말만 드물다. 그것은 실제로 무언가 일어나는 말이라
    // 자주 나오면 특별할 것이 없어진다.

    public const float TalkWeightHp = 4f;
    public const float TalkWeightExp = 4f;
    public const float TalkWeightAction = 1f;

    /// <summary>
    /// 특기를 한 번 쓰면 이만큼 턴이 지나야 다시 청할 수 있다.
    ///
    /// 쿨타임이 없으면 특기가 세지든 약하든 매 턴 청하는 것이 최선이 되어,
    /// 말 걸기가 버튼 연타가 된다.
    /// </summary>
    public const int ActionCooldownTurns = 20;

    /// <summary>
    /// 이번에 무슨 말을 할지 고른다.
    ///
    /// 특기가 아직 식지 않았거나 그 동료에게 특기 대사가 없으면 그것만 빼고 고른다.
    /// 빼는 것을 부르는 쪽에 맡기지 않는 이유는, 빼먹으면 없는 대사가 뜨기 때문이다.
    /// </summary>
    /// <param name="roll">0 이상 1 이하의 무작위 수.</param>
    public static PartnerTalk PickTalk(float roll, bool actionAvailable)
    {
        var weights = new List<float>
        {
            TalkWeightHp,
            actionAvailable ? TalkWeightAction : 0f,
            TalkWeightExp,
        };

        int picked = WeightedPick.Choose(weights, roll * WeightedPick.Total(weights));

        switch (picked)
        {
            case 0: return PartnerTalk.Hp;
            case 1: return PartnerTalk.Action;
            default: return PartnerTalk.Exp;
        }
    }

    /// <summary>
    /// 아직 남인 동료가 한 번 움직이는 데 드는 턴 수(무거움).
    ///
    /// <b>플레이어가 쫓아가서 말을 걸 수 있어야 한다.</b> 같은 속도로 서성이면
    /// 영영 따라잡지 못해 말을 걸 방법이 없다. 두 턴에 한 걸음이면 플레이어가
    /// 확실히 따라붙는다.
    ///
    /// 동료가 되면 풀린다 — 함께 다니기로 한 뒤에도 느리면 계단마다 두고 가게 된다.
    /// </summary>
    public const int NeutralTurnCost = 2;

    /// <summary>
    /// 이번 턴에 움직일 차례인가. 동료가 된 뒤에는 늘 움직인다.
    /// </summary>
    /// <param name="turnsSeen">이 동료가 지금까지 맞은 턴 수(0 부터).</param>
    public static bool ActsThisTurn(int turnsSeen, bool joined)
    {
        if (joined) return true;

        return turnsSeen % NeutralTurnCost == 0;
    }

    /// <summary>
    /// 아직 남일 때의 한 걸음. <b>플레이어를 쫓지도 적을 치지도 않고 서성인다.</b>
    ///
    /// 적의 Patrol 처럼 목표를 정해 두고 가지는 않는다. 목표를 정하면 방을 나가
    /// 통로로 사라지는데, 지금은 플레이어가 찾아가 말을 걸어야 하는 상대라
    /// 제자리 근처에 머무는 편이 낫다.
    ///
    /// 무작위는 여기서 굴리지 않고 <b>굴린 결과를 받는다</b>(ItemEnhancement 와 같은 이유).
    /// </summary>
    /// <summary>
    /// 목표를 정해 두고 그리로 걷는다. <b>탐색이 이 모양이다.</b>
    ///
    /// 서성이는 것(DecideNeutral)과 다르다. 서성이면 제자리를 맴돌아 층을
    /// 뒤지는 데 한참이 걸리고, 보는 쪽에서는 길을 잃은 것처럼 보인다.
    /// 목표를 향해 곧게 걸으면 <b>어디로 가려는지가 화면에서 읽힌다.</b>
    ///
    /// 목표에 닿았거나 갈 곳이 막혔으면 Stay 다 — 부르는 쪽이 그것을 보고
    /// 새 목표를 고른다. 어디로 갈지 고르는 일은 방·문 목록을 아는 쪽의 몫이다.
    /// </summary>
    public static PartnerTurn DecideSearch(
        Vector3Int self, Vector3Int goal, Func<Vector3Int, bool> canEnter)
    {
        if (self == goal) return PartnerTurn.Stay();

        Vector3Int? step = StepToward(self, goal, canEnter);
        return step.HasValue ? PartnerTurn.MoveTo(step.Value) : PartnerTurn.Stay();
    }

    /// <param name="roll">0 이상 1 이하의 무작위 수. 갈 수 있는 칸 중 하나를 고른다.</param>
    /// <param name="cameFrom">직전에 서 있던 칸. 되돌아가지 않으려고 피한다.</param>
    public static PartnerTurn DecideNeutral(
        Vector3Int self, Func<Vector3Int, bool> canEnter, float roll, Vector3Int? cameFrom = null)
    {
        var open = new List<Vector3Int>();

        foreach (Vector3Int neighbour in Neighbours(self))
        {
            if (canEnter != null && !canEnter(neighbour)) continue;

            open.Add(neighbour);
        }

        if (open.Count == 0) return PartnerTurn.Stay();

        // 왔던 칸은 빼 둔다. 두 칸 사이를 오가면 서성이는 것이 아니라
        // 고장 난 것처럼 보인다. 그것밖에 없으면 어쩔 수 없이 되돌아간다.
        if (cameFrom.HasValue && open.Count > 1) open.Remove(cameFrom.Value);

        int picked = Mathf.Clamp(Mathf.FloorToInt(roll * open.Count), 0, open.Count - 1);
        return PartnerTurn.MoveTo(open[picked]);
    }

    /// <summary>
    /// 이번 턴의 결정.
    /// </summary>
    /// <param name="self">동료가 선 칸.</param>
    /// <param name="player">플레이어가 선 칸.</param>
    /// <param name="enemies">지금 살아 있는 적들의 칸.</param>
    /// <param name="canEnter">그 칸에 들어갈 수 있는가(벽·물·다른 생물을 이미 걸러 낸 것).</param>
    /// <param name="stepsToPlayer">그 칸에서 플레이어까지의 걸음 수. 갈 수 없으면 음수.</param>
    public static PartnerTurn Decide(
        Vector3Int self,
        Vector3Int player,
        IReadOnlyList<Vector3Int> enemies,
        Func<Vector3Int, bool> canEnter,
        Func<Vector3Int, int> stepsToPlayer)
    {
        // 1. 붙어 있는 적.
        Vector3Int? adjacent = PickAdjacentEnemy(self, player, enemies);
        if (adjacent.HasValue) return PartnerTurn.AttackAt(adjacent.Value);

        // 2. 가까운 적에게 다가간다.
        Vector3Int? nearby = PickNearestEnemy(self, enemies, EnemySeekDistance);
        if (nearby.HasValue)
        {
            Vector3Int? toward = StepToward(self, nearby.Value, canEnter);
            if (toward.HasValue) return PartnerTurn.MoveTo(toward.Value);
        }

        // 3. 플레이어에게 돌아간다. 걸음 수 지도를 쓰므로 벽을 크게 돌아가야 하는
        //    지형에서도 제대로 따라온다.
        int here = stepsToPlayer != null ? stepsToPlayer(self) : -1;
        if (here < 0 || here > FollowDistance)
        {
            Vector3Int? follow = StepDownhill(self, canEnter, stepsToPlayer);
            if (follow.HasValue) return PartnerTurn.MoveTo(follow.Value);

            // 지도를 못 쓰는 자리(물 건너편 등)에서는 곧장 다가가 본다.
            Vector3Int? direct = StepToward(self, player, canEnter);
            if (direct.HasValue) return PartnerTurn.MoveTo(direct.Value);
        }

        return PartnerTurn.Stay();
    }

    /// <summary>
    /// 붙어 있는 적 하나. <b>플레이어와도 붙어 있는 적을 먼저 고른다.</b>
    ///
    /// 그래야 둘이 같은 적을 친다. 각자 다른 적을 치면 어느 쪽도 빨리 쓰러지지
    /// 않고, 플레이어는 맞는 적의 수만 늘어난다.
    ///
    /// <b>여기 오는 목록은 이미 '칠 수 있는 적' 만 담고 있다</b>(Partner.EnemyCells).
    /// 무기가 어디까지 닿는지는 무기의 규칙이라, 이 판단에 섞으면 원거리 무기를
    /// 든 동료가 생겼을 때 여기에 예외를 파야 한다.
    /// </summary>
    public static Vector3Int? PickAdjacentEnemy(
        Vector3Int self, Vector3Int player, IReadOnlyList<Vector3Int> enemies)
    {
        if (enemies == null) return null;

        Vector3Int? fallback = null;

        foreach (Vector3Int enemy in enemies)
        {
            if (!IsAdjacent(self, enemy)) continue;


            if (IsAdjacent(player, enemy)) return enemy;

            if (!fallback.HasValue) fallback = enemy;
        }

        return fallback;
    }

    /// <summary>가장 가까운 적. 그 거리가 limit 를 넘으면 없는 것으로 본다.</summary>
    public static Vector3Int? PickNearestEnemy(
        Vector3Int self, IReadOnlyList<Vector3Int> enemies, int limit)
    {
        if (enemies == null) return null;

        Vector3Int? best = null;
        int bestDistance = int.MaxValue;

        foreach (Vector3Int enemy in enemies)
        {
            int distance = ChebyshevDistance(self, enemy);
            if (distance > limit || distance >= bestDistance) continue;

            bestDistance = distance;
            best = enemy;
        }

        return best;
    }

    /// <summary>목표에 가장 가까워지는 이웃 칸. 갈 곳이 없으면 null.</summary>
    private static Vector3Int? StepToward(
        Vector3Int self, Vector3Int goal, Func<Vector3Int, bool> canEnter)
    {
        Vector3Int? best = null;
        int bestDistance = ChebyshevDistance(self, goal);

        foreach (Vector3Int neighbour in Neighbours(self))
        {
            if (canEnter != null && !canEnter(neighbour)) continue;

            int distance = ChebyshevDistance(neighbour, goal);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = neighbour;
        }

        return best;
    }

    /// <summary>걸음 수가 줄어드는 이웃 칸. 지도가 없거나 갈 곳이 없으면 null.</summary>
    private static Vector3Int? StepDownhill(
        Vector3Int self, Func<Vector3Int, bool> canEnter, Func<Vector3Int, int> stepsToPlayer)
    {
        if (stepsToPlayer == null) return null;

        int here = stepsToPlayer(self);
        Vector3Int? best = null;

        // 지금 자리를 못 재면(갈 수 없는 자리) 아무 칸이나 잴 수 있는 곳으로 나선다.
        int bestSteps = here >= 0 ? here : int.MaxValue;

        foreach (Vector3Int neighbour in Neighbours(self))
        {
            if (canEnter != null && !canEnter(neighbour)) continue;

            int steps = stepsToPlayer(neighbour);
            if (steps < 0 || steps >= bestSteps) continue;

            bestSteps = steps;
            best = neighbour;
        }

        return best;
    }

    /// <summary>여덟 이웃 칸.</summary>
    private static IEnumerable<Vector3Int> Neighbours(Vector3Int cell)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                yield return new Vector3Int(cell.x + dx, cell.y + dy, cell.z);
            }
        }
    }

    /// <summary>대각선을 한 걸음으로 세는 거리. 이 게임의 이동이 여덟 방향이라 그렇다.</summary>
    public static int ChebyshevDistance(Vector3Int a, Vector3Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    /// <summary>서로 붙어 있는가(대각선 포함). 같은 칸은 붙은 것이 아니다.</summary>
    public static bool IsAdjacent(Vector3Int a, Vector3Int b)
    {
        int distance = ChebyshevDistance(a, b);
        return distance == 1;
    }
}
