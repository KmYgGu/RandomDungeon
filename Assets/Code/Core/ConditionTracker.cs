using System.Collections.Generic;

/// <summary>
/// 지금 걸려 있는 상태이상과 남은 턴.
///
/// 지금까지 상태이상은 <b>거는 쪽이 없었다.</b> 표에 15종이 적혀 있어도 실제로
/// 뜨는 것은 굶주림뿐이었는데, 그것은 만복도만 보면 알 수 있어서 따로 들고 있을
/// 필요가 없었기 때문이다. 함정·아이템·능력이 상태를 걸기 시작하면 "지금 무엇에
/// 걸려 있고 몇 턴 남았는가" 를 어딘가는 기억해야 한다. 그 자리가 여기다.
///
/// ── 굶주림은 여기 들어오지 않는다 ─────────────────────────────────
/// 만복도에서 저절로 따라 나오는 것은 여기에 넣지 않는다. 두 곳에 적어 두면
/// 밥을 먹었는데 '배고픔' 이 남아 있는 식으로 어긋난다.
/// 화면에 늘어놓을 때만 ConditionReport 가 둘을 합친다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class ConditionTracker
{
    /// <summary>
    /// 남은 턴이 이 값이면 스스로 풀리지 않는다.
    ///
    /// 시트의 turn 열이 0 인 줄이 그렇다(굶주림처럼 조건이 유지되는 동안 계속).
    /// 그런 것을 거는 수단이 생기면 푸는 수단도 함께 있어야 한다.
    /// </summary>
    public const int Forever = 0;

    private readonly Dictionary<string, int> remaining = new Dictionary<string, int>();

    /// <summary>걸려 있는 것이 하나라도 있는가.</summary>
    public bool IsEmpty => remaining.Count == 0;

    /// <summary>
    /// 상태를 건다. 이미 걸려 있으면 <b>남은 턴이 긴 쪽을 남긴다.</b>
    ///
    /// 덮어쓰면 5턴짜리 잠에 막 걸린 사람이 1턴짜리를 또 맞고 깨어난다.
    /// 더하면 같은 함정을 여러 번 밟아 영영 못 움직이게 된다.
    /// </summary>
    public void Apply(string id, int turns)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (turns < 0) return;

        if (!remaining.TryGetValue(id, out int already))
        {
            remaining[id] = turns;
            return;
        }

        // 영영 걸린 것은 무엇으로도 짧아지지 않는다.
        if (already == Forever) return;
        if (turns == Forever) { remaining[id] = Forever; return; }

        if (turns > already) remaining[id] = turns;
    }

    public bool Has(string id)
        => !string.IsNullOrEmpty(id) && remaining.ContainsKey(id);

    /// <summary>남은 턴. 걸려 있지 않으면 0, 영영이면 Forever(0)이라 Has 로 구분한다.</summary>
    public int RemainingTurns(string id)
        => id != null && remaining.TryGetValue(id, out int turns) ? turns : 0;

    /// <summary>손으로 푼다. 해독제 같은 것이 쓸 자리다.</summary>
    public void Remove(string id)
    {
        if (id != null) remaining.Remove(id);
    }

    /// <summary>새 판을 시작할 때.</summary>
    public void Clear() => remaining.Clear();

    /// <summary>
    /// 한 턴이 지났다. 남은 턴을 줄이고 다 된 것을 뗀다. 풀린 것들을 돌려준다.
    ///
    /// 푼 목록을 돌려주는 이유는 "독이 풀렸다" 를 알려야 하기 때문이다.
    /// 여기서 직접 알리지 않는 것은, 그러면 Unity 없이 확인할 수 없어지기 때문이다.
    /// </summary>
    public List<string> Tick()
    {
        var expired = new List<string>();
        if (remaining.Count == 0) return expired;

        // 도는 중에 사전을 고칠 수 없으므로 열쇠를 먼저 베껴 둔다.
        var ids = new List<string>(remaining.Keys);

        foreach (string id in ids)
        {
            int turns = remaining[id];
            if (turns == Forever) continue;

            turns--;
            if (turns <= 0)
            {
                remaining.Remove(id);
                expired.Add(id);
            }
            else
            {
                remaining[id] = turns;
            }
        }

        return expired;
    }

    /// <summary>걸려 있는 것들의 id. 표시 순서는 부르는 쪽이 정한다.</summary>
    public IEnumerable<string> ActiveIds => remaining.Keys;
}
