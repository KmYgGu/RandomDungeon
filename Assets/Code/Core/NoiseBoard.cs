using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방금 어디서 소리가 났는가. <b>싸움 · 던지기 · 함정</b>이 여기에 적는다.
///
/// ── 왜 필요한가 ───────────────────────────────────────────────────
/// 플레이어를 놓친 동료가 아무 데나 헤매면, 다시 만나는 것이 순전히 운이 된다.
/// 실제로는 싸우는 소리가 나면 그쪽으로 가 보는 것이 자연스럽다. 소리를
/// 남겨 두면 <b>"왜 저기로 가지" 가 화면에서 설명된다.</b>
///
/// <b>동료 전용이 아니다.</b> 몇몇 적도 같은 것을 듣게 할 예정이라, 듣는 쪽을
/// 가리지 않는 자리에 둔다. 적어 두는 쪽도 마찬가지다 — 누가 싸웠는지가
/// 아니라 <b>어디서 소리가 났는지</b>만 남긴다.
///
/// ── 왜 방이 아니라 칸인가 ─────────────────────────────────────────
/// 방 번호로 적으면 통로에서 난 소리를 남길 자리가 없고, 듣는 쪽이 방 목록을
/// 알아야 한다. 칸 하나로 적어 두면 통로든 방이든 그대로 가리킬 수 있고,
/// "그 방으로 간다" 는 것은 결국 그 칸으로 가는 것과 같다.
///
/// Unity 것을 계산에 쓰지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class NoiseBoard
{
    /// <summary>
    /// 소리가 남아 있는 턴 수.
    ///
    /// 너무 길면 이미 끝난 싸움 자리로 계속 몰려가고, 너무 짧으면 통로 하나
    /// 건너오는 사이에 사라진다. 던전 한 층을 가로지르는 데 드는 걸음 수를
    /// 생각하면 이만큼이 "가 볼 만한" 시간이다.
    /// </summary>
    public const int FadeTurns = 15;

    private bool heard;
    private Vector3Int cell;
    private int turn;

    /// <summary>
    /// 소리가 났다고 적는다. <b>나중 것이 앞의 것을 덮는다.</b>
    ///
    /// 여러 곳에서 동시에 나면 가장 최근 것만 남는다. 목록으로 쌓아 두면
    /// 듣는 쪽이 "어느 것부터 가 볼까" 를 또 정해야 하는데, 그건 지금 필요한
    /// 것보다 훨씬 복잡한 물음이다.
    /// </summary>
    public void Report(Vector3Int where, int currentTurn)
    {
        heard = true;
        cell = where;
        turn = currentTurn;
    }

    /// <summary>
    /// 아직 들리는 소리가 있으면 그 자리를 준다. 없거나 오래됐으면 false.
    /// </summary>
    public bool TryGetLatest(int currentTurn, out Vector3Int where)
    {
        where = cell;
        if (!heard) return false;

        // 시간이 거꾸로 가는 일은 없지만(턴은 늘어나기만 한다), 새 판에서
        // 턴이 0 으로 돌아가면 옛 소리가 미래의 것으로 보인다. 그건 Clear 가 막는다.
        return currentTurn - turn <= FadeTurns;
    }

    /// <summary>새 층·새 판. 지난 층의 소리를 따라가면 갈 수 없는 자리를 향해 걷는다.</summary>
    public void Clear()
    {
        heard = false;
        cell = default;
        turn = 0;
    }
}
