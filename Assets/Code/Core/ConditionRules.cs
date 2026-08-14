using System.Collections.Generic;

/// <summary>
/// 지금 걸린 상태이상이 <b>무엇을 못 하게 하는가</b>.
///
/// ── 왜 따로 두는가 ────────────────────────────────────────────────
/// 예전에는 부르는 쪽이 효과를 직접 물었다 —
/// <c>if (HasEffect(ConditionEffect.SkipTurn))</c>. 그러면 <b>부르는 쪽이
/// 상태이상의 속사정을 알아야 한다.</b> 잠 말고 마비·기절이 생기고 그것들이
/// 다른 효과로 붙으면, 그 조건을 아는 자리를 전부 찾아 고쳐야 한다.
///
/// 여기서는 <b>뜻으로 묻는다</b> — "행동할 수 있는가", "방향이 흔들리는가".
/// 상태이상이 스무 종으로 늘어도 고칠 곳은 이 파일뿐이다.
///
/// ── 아직 없는 것은 만들지 않는다 ──────────────────────────────────
/// 시트에 20 종이 있지만 코드가 아는 효과는 다섯이다. 실명·이동불가가
/// 생기면 여기에 <c>CanSee</c>·<c>CanMove</c> 를 더하고 부르는 쪽 한 줄을
/// 고치면 된다. 지금 없는 물음을 미리 파 두지는 않는다 — 빈 물음은
/// 언제나 true 를 돌려주므로, 있으나 마나 한 검사가 코드에 남는다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ConditionRules
{
    /// <summary>
    /// 이번 턴에 스스로 행동할 수 있는가.
    ///
    /// 못 한다고 해서 턴을 멈추지는 않는다. 턴은 흘러야 남은 잠이 줄어들고
    /// 그래야 언젠가 깨어난다 — 부르는 쪽이 대기로 넘긴다.
    /// </summary>
    public static bool CanAct(IEnumerable<ConditionEffect> active)
        => !Any(active, ConditionEffect.SkipTurn);

    /// <summary>가려던 방향이 제멋대로 바뀌는가(혼란).</summary>
    public static bool ScramblesDirection(IEnumerable<ConditionEffect> active)
        => Any(active, ConditionEffect.ScrambleDirection);

    /// <summary>장비의 특수 능력이 막혀 있는가(봉인).</summary>
    public static bool SealsAbilities(IEnumerable<ConditionEffect> active)
        => Any(active, ConditionEffect.SealAbility);

    private static bool Any(IEnumerable<ConditionEffect> active, ConditionEffect wanted)
    {
        if (active == null) return false;

        foreach (ConditionEffect effect in active)
        {
            if (effect == wanted) return true;
        }

        return false;
    }
}
