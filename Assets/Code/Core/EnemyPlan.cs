using UnityEngine;

/// <summary>적이 지금 무엇을 하며 다니는가.</summary>
public enum EnemyMode
{
    /// <summary>플레이어를 쫓는다.</summary>
    Chase,
    /// <summary>통로 쪽 출구를 향해 돌아다닌다.</summary>
    Patrol,
    /// <summary>원거리 공격이 날아온 방향을 확인한다.</summary>
    Curious,
    /// <summary>경보가 울린 몬스터 하우스로 집결.</summary>
    Rally,
}

/// <summary>이번 턴에 누구를 때리는가.</summary>
public enum EnemyTarget
{
    /// <summary>붙어 있는 상대가 없다. 움직인다.</summary>
    None,
    Player,
    Partner,
}

/// <summary>
/// 적이 이번 턴의 판단에 쓰는 사실들. <b>씬에서 긁어모으는 일은 Enemy 가 한다.</b>
///
/// 이 묶음만 받으면 판단은 Unity 없이 돈다. 판단에 무엇이 필요한지가 여기 한
/// 곳에 적혀 있어서, 새 성향(예: 소리를 듣는 적)을 더할 때도 이 묶음에 칸을
/// 하나 늘리고 판단에서 읽으면 된다.
/// </summary>
public struct EnemySight
{
    /// <summary>지금의 모드. 판단은 여기서 출발한다.</summary>
    public EnemyMode Current;

    /// <summary>돌아선 상인인가. 놓치는 법이 없다.</summary>
    public bool HostileMerchant;

    /// <summary>몬스터 하우스 경보로 집결하는 중인가, 그리고 그 방에 닿았는가.</summary>
    public bool Rallying;
    public bool RallyArrived;

    /// <summary>플레이어가 보이는가 — 같은 방에 있거나 바로 옆에 붙어 있다.</summary>
    public bool PlayerVisible;

    /// <summary>이번 턴의 걸음 수 지도(GameManager.Chase)가 준비돼 있는가.</summary>
    public bool ChaseMapReady;

    /// <summary>플레이어까지의 걸음 수. 지도가 없거나 갈 수 없으면 음수.</summary>
    public int StepsToPlayer;

    /// <summary>플레이어까지의 직선 거리. 지도가 없을 때만 쓴다.</summary>
    public float StraightDistance;
}

/// <summary>모드를 바꾸면서 함께 해야 하는 뒷일.</summary>
public struct EnemyModeChange
{
    public EnemyMode Mode;

    /// <summary>순찰을 처음부터 다시 잡는다(지나온 칸과 목표를 잊는다).</summary>
    public bool ResetPatrol;

    /// <summary>원거리 공격 방향을 확인하던 것을 그만둔다.</summary>
    public bool StopCuriosity;

    /// <summary>집결을 끝낸다. 방에 닿았다.</summary>
    public bool LeaveRally;
}

/// <summary>
/// 적이 이번 턴에 어떤 모드로 다니고 누구를 때릴지 정한다. <b>실제로 움직이지는 않는다.</b>
///
/// ── 왜 떼어 냈나 ──────────────────────────────────────────────────
/// 예전에는 이 판단이 Enemy 안에서 이동·물리·연출과 한 덩어리였다. 그래서
/// "언제 추격을 포기하는가" 같은 규칙 하나를 확인하려 해도 에디터를 켜고 적을
/// 그 상황까지 끌고 가야 했다. 동료는 처음부터 판단(PartnerPlan)과 실행(Partner)을
/// 갈라 두었고, 그쪽은 Verify 가 규칙을 직접 검사한다. 적도 같은 길로 옮겼다.
///
/// 난이도는 <b>어떤 적이 나오는가</b>로 조절하기로 했으므로(DungeonPopulator 주석)
/// 적의 종류와 성향이 곧 늘어난다. 성향은 이 판단에 들어오므로, 여기가 Unity 없이
/// 확인되지 않으면 적을 하나 더할 때마다 플레이로만 검사하게 된다.
///
/// ── 여기 없는 것 ──────────────────────────────────────────────────
/// 어느 칸으로 걸을지는 여전히 Enemy 가 정한다(TryMoveAlong). 그쪽은 다른 적이
/// 찜해 둔 칸 · 아직 안 움직인 적 · 지형을 함께 봐야 해서 씬과 떼기 어렵다.
/// 순찰 목표 고르기는 이미 PatrolPlan 에, 잠과 호기심은 EnemySleep ·
/// EnemyCuriosity 에 따로 있다.
/// </summary>
public static class EnemyPlan
{
    /// <summary>
    /// 이 걸음 수보다 멀어지면 추격을 포기한다.
    /// 직선 거리가 아니라 실제로 걸어가야 하는 걸음 수다.
    /// </summary>
    public const int ChaseGiveUpDistance = 20;

    /// <summary>
    /// 이번 턴의 모드. 차례가 곧 우선순위다.
    ///
    ///   1. 돌아선 상인        → 언제나 추격
    ///   2. 집결 중이고 덜 왔다 → 집결. 닿았으면 집결을 끝내고 아래로 내려간다
    ///   3. 플레이어가 보인다   → 추격. 확인하던 것은 그만둔다
    ///   4. 추격 중이 아니다    → 그대로(순찰은 순찰, 확인은 확인)
    ///   5. 추격 중인데 놓쳤다  → 순찰로. 너무 멀거나 아예 갈 수 없을 때
    ///
    /// 놓쳤는지는 <b>직선 거리가 아니라 걸음 수</b>로 잰다. 벽 하나를 사이에 두고
    /// 직선으로는 두 칸이지만 돌아가려면 서른 걸음인 경우가 흔한데, 직선으로
    /// 재면 그런 상대를 영영 포기하지 않고 벽만 민다. 지도가 없을 때만 직선으로 잰다.
    /// </summary>
    public static EnemyModeChange NextMode(EnemySight sight)
    {
        var change = new EnemyModeChange { Mode = sight.Current };

        if (sight.HostileMerchant)
        {
            change.Mode = EnemyMode.Chase;
            return change;
        }

        if (sight.Rallying)
        {
            if (!sight.RallyArrived)
            {
                change.Mode = EnemyMode.Rally;
                return change;
            }

            // 방에 닿았다. 집결을 끝내고 평소처럼 판단한다 — 그 방에 플레이어가
            // 있으면 바로 아래에서 추격이 된다.
            change.LeaveRally = true;
            change.Mode = EnemyMode.Patrol;
        }

        if (sight.PlayerVisible)
        {
            change.Mode = EnemyMode.Chase;
            change.StopCuriosity = true;
            return change;
        }

        if (change.Mode != EnemyMode.Chase) return change;

        if (LostTrack(sight))
        {
            change.Mode = EnemyMode.Patrol;
            change.ResetPatrol = true;
        }

        return change;
    }

    /// <summary>추격하던 플레이어를 놓쳤는가.</summary>
    private static bool LostTrack(EnemySight sight)
    {
        if (sight.ChaseMapReady)
            return sight.StepsToPlayer < 0 || sight.StepsToPlayer > ChaseGiveUpDistance;

        return sight.StraightDistance > ChaseGiveUpDistance;
    }

    /// <summary>
    /// 붙어 있는 상대 중 누구를 때릴지 고른다.
    ///
    /// ── 둘 다 붙어 있으면 반반이다 ────────────────────────────────────
    /// 원래는 적 성향이 갈라야 할 자리다 — 공격력이 높은 쪽을 노리는 적, 물건을
    /// 많이 든 쪽을 노리는 적처럼. 그런 성향은 시트에 열을 하나 두고 여기서
    /// 읽으면 된다(EquipAbility 와 같은 방식). 지금은 그 목록이 없어 반반으로 둔다.
    ///
    /// 치우치지 않는 것이 중요하다. 늘 플레이어만 때리면 동료는 맞지 않는 벽이
    /// 되어 데리고 다니는 것이 무조건 이득이 되고, 늘 동료만 때리면 동료가
    /// 순식간에 녹아 데리고 다닐 수가 없다.
    /// </summary>
    /// <param name="roll">0~1. 둘 다 붙어 있을 때만 읽는다.</param>
    public static EnemyTarget PickTarget(bool playerAdjacent, bool partnerAdjacent, float roll)
    {
        if (playerAdjacent && partnerAdjacent)
            return roll < 0.5f ? EnemyTarget.Partner : EnemyTarget.Player;

        if (playerAdjacent) return EnemyTarget.Player;
        if (partnerAdjacent) return EnemyTarget.Partner;

        return EnemyTarget.None;
    }
}
