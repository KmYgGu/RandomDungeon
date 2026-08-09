using UnityEngine;

/// <summary>
/// 방향키 입력을 읽는 곳. 두 가지 규칙을 갖고 있다.
///
/// ── 1. "한 번 떼기 전까지는 무시" ──────────────────────────────────
/// 상태가 바뀌는 순간(달리기가 멈췄을 때, 가방을 닫았을 때) 플레이어는 대개
/// 아직 키를 누르고 있다. 그 눌린 상태가 새 상태로 새어 들어가면
/// 의도하지 않은 행동이 한 번 일어난다.
/// (달리기가 갈림길에서 멈췄는데 곧바로 한 칸 더 걸어가던 문제)
///
/// ── 2. "대각선 유예" ──────────────────────────────────────────────
/// 사람이 방향키 두 개를 완전히 동시에 누르는 것은 불가능하다. 보통 수십 ms
/// 차이가 나는데, 게임이 첫 프레임에 곧바로 이동을 확정해 버리면
/// "오른쪽으로 한 칸 간 다음에 대각선" 이 되어 버린다.
///
/// 그래서 방향키를 새로 누른 순간에는 바로 확정하지 않고 아주 잠깐 기다린다.
/// 그 사이에 옆 키가 같이 눌리면 대각선으로 합쳐서 내보낸다.
/// 이미 누르고 있는 중(연속 이동)에는 기다리지 않으므로, 지연은 첫 걸음에만 생긴다.
/// </summary>
public class DirectionInput
{
    /// <summary>
    /// 새로 누른 방향키를 확정하기 전에 옆 키를 기다려 주는 시간(초).
    /// 0 이면 기다리지 않는다(예전 동작).
    /// </summary>
    public float DiagonalGraceSeconds { get; set; } = 0.06f;

    private bool needsRelease;

    // 대각선 유예 상태
    private bool idle = true;
    private bool waiting;
    private float waitStartedAt;
    private Vector2 pending;

    /// <summary>
    /// 방향이 실제로 눌려 있는가. 대각선 유예가 아직 안 끝났어도 true.
    ///
    /// "방향을 잡은 채 B 를 눌렀는가" 를 볼 때 쓴다. 유예가 끝나기를 기다리면
    /// 방향과 B 를 거의 동시에 누른 경우에 달리기가 대기로 잘못 판정된다.
    /// </summary>
    public bool IsPressed
    {
        get
        {
            if (needsRelease) return false;
            return (int)Input.GetAxisRaw("Horizontal") != 0
                || (int)Input.GetAxisRaw("Vertical") != 0;
        }
    }

    /// <summary>방향키를 완전히 뗄 때까지 입력을 무시한다.</summary>
    public void RequireRelease()
    {
        needsRelease = true;
        CancelWait();
    }

    /// <summary>
    /// 손을 뗐는지만 확인해서 잠금을 푼다.
    ///
    /// 플레이어 차례가 아닐 때도 불러 줘야 한다. 적이 움직이는 동안에는 방향 입력을
    /// 읽지 않는데, 그 사이에 키를 뗐다 다시 누르면 뗀 순간을 놓쳐서 잠금이
    /// 계속 걸려 있게 된다. 그러면 한 번 더 떼야만 움직일 수 있다.
    /// </summary>
    public void PollRelease()
    {
        if ((int)Input.GetAxisRaw("Horizontal") == 0 && (int)Input.GetAxisRaw("Vertical") == 0)
        {
            needsRelease = false;
            idle = true;
            CancelWait();
        }
    }

    /// <summary>
    /// 방향 입력을 읽는다. 읽을 것이 없거나, 아직 잠겨 있거나,
    /// 대각선을 기다리는 중이면 false.
    /// </summary>
    /// <param name="diagonalOnly">대각선 입력만 받아들일지(Shift 를 누른 상태).</param>
    public bool TryRead(bool diagonalOnly, out Vector2 direction)
    {
        return Resolve(
            (int)Input.GetAxisRaw("Horizontal"),
            (int)Input.GetAxisRaw("Vertical"),
            diagonalOnly,
            Time.time,
            out direction);
    }

    /// <summary>
    /// 규칙 자체. Unity 입력과 시간에서 떼어 놓아서 이 부분만 따로 검증할 수 있다.
    /// </summary>
    public bool Resolve(int horizontal, int vertical, bool diagonalOnly, float now, out Vector2 direction)
    {
        direction = Vector2.zero;

        // 손을 뗐다. 모든 상태를 처음으로 돌린다.
        if (horizontal == 0 && vertical == 0)
        {
            needsRelease = false;
            idle = true;
            CancelWait();
            return false;
        }

        if (needsRelease) return false;

        var raw = new Vector2(horizontal, vertical);
        bool isDiagonal = horizontal != 0 && vertical != 0;

        // Shift 를 누르고 있으면 축 이동으로 미끄러지지 않도록 대각선만 받는다.
        // 이때는 플레이어가 대각선을 명시했으므로 유예도 필요 없다.
        if (diagonalOnly)
        {
            idle = false;
            CancelWait();
            if (!isDiagonal) return false;

            direction = raw;
            return true;
        }

        // 이미 누르고 있던 중이면(연속 이동) 그대로 내보낸다. 지연 없음.
        if (!idle && !waiting)
        {
            direction = raw;
            return true;
        }

        // 방금 새로 누른 경우.
        if (idle)
        {
            idle = false;

            // 두 축이 같은 프레임에 들어왔다면 기다릴 이유가 없다.
            if (isDiagonal || DiagonalGraceSeconds <= 0f)
            {
                direction = raw;
                return true;
            }

            waiting = true;
            waitStartedAt = now;
            pending = raw;
            return false;
        }

        // 옆 키를 기다리는 중.
        if (isDiagonal)
        {
            // 기다린 보람이 있었다. 대각선으로 합쳐서 내보낸다.
            CancelWait();
            direction = raw;
            return true;
        }

        if (now - waitStartedAt < DiagonalGraceSeconds)
            return false;

        // 시간이 다 됐다. 지금 눌려 있는 방향으로 확정한다.
        CancelWait();
        direction = raw != Vector2.zero ? raw : pending;
        return direction != Vector2.zero;
    }

    private void CancelWait()
    {
        waiting = false;
        pending = Vector2.zero;
    }
}
