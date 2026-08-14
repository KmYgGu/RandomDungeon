using System.Collections.Generic;

/// <summary>
/// 함정이 작동했을 때 무슨 일이 일어나는가. <b>번호 → 동작 표</b>다.
///
/// ── 왜 표인가 ─────────────────────────────────────────────────────
/// 예전에는 GameManager 안에 <c>switch (trapId)</c> 로 있었다. 함정을 하나
/// 더 만들려면 1700 줄짜리 갓 오브젝트를 열어야 했다(OCP 위반). 아이템은
/// 이미 표로 되어 있는데(<see cref="ItemEffects"/>) 함정만 그러지 않았다.
/// <b>이제 함정 추가 = 아래 표에 한 줄.</b>
///
/// ── 여기 있는 것과 없는 것 ────────────────────────────────────────
/// 여기 있는 것은 "어느 함정이 무엇을 부르는가" 뿐이다. 워프·날리기·상태이상
/// 같은 <b>동작 자체는 GameManager 에 그대로 둔다.</b> 함정만 쓰는 것이 아니라
/// 지팡이·능력에서도 부를 것들이라(WarpPlayer 주석) 함정 파일에 가둘 수 없다.
///
/// 그래서 새 함정이 기존 동작을 조합하는 것이면 표에 한 줄이면 끝이고,
/// 아주 새로운 동작이 필요할 때만 GameManager 에 그 동작을 하나 더한다.
///
/// ── 갈래는 이름으로 나눈다 ────────────────────────────────────────
/// 시트의 id2(hole · warp …)가 열쇠다. 번호로 나누면 코드에 10005 만 남아
/// 그게 무엇인지 알 수 없고, note 열의 글자로 나누면 메모의 말투를 다듬는
/// 순간 게임이 조용히 바뀐다(문서 5절).
/// </summary>
public static class TrapActions
{
    /// <summary>함정 하나가 하는 일. 부르는 쪽(GameManager)을 받아 그 동작을 시킨다.</summary>
    public delegate void Effect(GameManager game);

    /// <summary>
    /// 시트의 id2 → 동작.
    ///
    /// 표에 없는 함정은 <b>아무 일도 하지 않는다.</b> 문구는 이미 떴으므로
    /// 플레이어에게는 "밟았지만 별일 없었다" 로 보인다. 아직 만들지 않은
    /// 함정(soil)이 그 상태다 — 시트가 코드보다 앞서 있는 것은 정상이다.
    /// </summary>
    private static readonly Dictionary<string, Effect> table =
        new Dictionary<string, Effect>
        {
            [TrapIds.Hole] = game => game.RunFallThroughHole(),

            [TrapIds.Warp] = game => game.RunWarpPlayer(),

            [TrapIds.Blow] = game => game.RunBlowPlayer(),

            [TrapIds.Dart] = game => game.LoseStrength(TrapEffects.DartStrengthLoss),

            // 아래 셋은 거는 상태만 다르다. 지속 턴도 문구도 시트가 정한다.
            [TrapIds.Gas] = game => game.ApplyCondition(ConditionCatalog.SleepId),
            [TrapIds.Spin] = game => game.ApplyCondition(ConditionCatalog.DisarrayId),
            [TrapIds.Restraint] = game => game.ApplyCondition(ConditionCatalog.RestraintId),

            [TrapIds.Sticky] = game => game.MakeRandomBagItemSticky(),
        };

    /// <summary>이 함정의 동작. 아직 만들지 않은 함정이면 null.</summary>
    public static Effect Find(string trapId)
    {
        if (string.IsNullOrEmpty(trapId)) return null;

        return table.TryGetValue(trapId, out Effect effect) ? effect : null;
    }

    /// <summary>동작이 붙어 있는 함정인가. 시트를 훑어 볼 때 쓴다.</summary>
    public static bool IsImplemented(string trapId) => Find(trapId) != null;

    /// <summary>
    /// 몸짓을 함정 동작보다 먼저 보여 줄 것인가.
    ///
    /// 워프만 아니다. 워프의 '빙글' 은 위로 올라가는 동안 도는 것이라 그
    /// 연출 안에 이미 들어 있고, 밖에서 또 돌리면 두 바퀴가 아니라 네 바퀴를 돈다.
    /// </summary>
    public static bool PlaysAnimationFirst(string trapId) => trapId != TrapIds.Warp;
}
