using UnityEngine;

/// <summary>달리기가 멈춘 이유. 로그와 디버그에 쓴다.</summary>
public enum RunStopReason
{
    None,
    /// <summary>눈앞이 벽이었다. 더 갈 곳이 없어 달리기가 풀린다.</summary>
    HitWall,
    /// <summary>
    /// 벽은 아닌데 나아갈 수 없었다(적·물·대각선 제약 등).
    /// 옆으로 돌아갈 길조차 없을 때만 여기까지 온다.
    /// </summary>
    PathObstructed,
    /// <summary>통로에서 갈림길을 만났다.</summary>
    Junction,
    /// <summary>방에 들어갔거나 방에서 나왔다(문을 지났다).</summary>
    RoomBoundary,
    /// <summary>아이템·함정·계단 같은 것을 밟았다.</summary>
    SteppedOnObject,
    /// <summary>적이 코앞까지 왔다.</summary>
    EnemyNearby,
    /// <summary>피해를 입었다.</summary>
    TookDamage,
    /// <summary>굶주려서 매 턴 체력이 깎이는 중이다.</summary>
    Starving,
    /// <summary>메시지창이나 가방이 열렸다.</summary>
    UiInterrupted,
    /// <summary>안전 상한에 걸렸다.</summary>
    StepLimit,
    /// <summary>적 턴이 끝나지 않아 더 진행할 수 없었다(비정상 상황 방어).</summary>
    TurnStalled,
}

/// <summary>
/// 달리기(오토런) 한 번의 상태를 들고 있는 클래스.
///
/// "지금 달리는 중인가, 어느 방향으로, 몇 걸음째인가" 만 안다.
/// 무엇에 부딪혔는지 같은 바깥 세계의 사정은 Player 가 확인해서
/// Stop() 으로 알려 준다. 덕분에 이 클래스는 Unity 씬 없이도 이해할 수 있다.
///
/// 달리기의 한 걸음은 평범한 이동과 완전히 같은 '한 턴' 이다.
/// 그래서 달리는 동안에도 적은 자기 차례를 갖고, 따라잡혀 맞을 수 있다.
/// </summary>
public class AutoRunner
{
    /// <summary>
    /// 한 번에 달릴 수 있는 최대 걸음 수.
    /// 어떤 정지 조건도 걸리지 않는 이상한 지형이 나와도 반드시 멈추게 하는 안전장치다.
    /// </summary>
    public const int MaxSteps = 60;

    public bool IsRunning { get; private set; }

    /// <summary>달리는 방향.</summary>
    public Vector2 Direction { get; private set; }

    /// <summary>지금 서 있다고 보는 칸의 월드 좌표.</summary>
    /// <remarks>
    /// transform.position 을 직접 읽지 않는 이유: 이동이 Rigidbody2D.MovePosition 으로
    /// 이뤄져서 다음 물리 갱신 전까지는 좌표가 아직 옛 값일 수 있다.
    /// 스스로 세어 두는 편이 확실하다.
    /// </remarks>
    public Vector2 Cell { get; private set; }

    /// <summary>달리기를 시작한 곳이 방 안이었는가. 방 경계를 넘었는지 판단할 기준.</summary>
    public bool StartedInsideRoom { get; private set; }

    /// <summary>직전 걸음 시점의 체력. 줄어들면 무언가에 맞은 것이다.</summary>
    public float HpAtLastStep { get; private set; }

    public RunStopReason LastStopReason { get; private set; } = RunStopReason.None;

    public int StepsTaken { get; private set; }

    public bool CanTakeAnotherStep => StepsTaken < MaxSteps;

    public void Begin(Vector2 startCell, Vector2 direction, bool insideRoom, float hp)
    {
        IsRunning = true;
        Direction = direction;
        Cell = startCell;
        StartedInsideRoom = insideRoom;
        HpAtLastStep = hp;
        StepsTaken = 0;
        LastStopReason = RunStopReason.None;
    }

    /// <summary>한 칸 옮긴 뒤에 부른다.</summary>
    public void RecordStep(float hp)
    {
        Cell += Direction;
        HpAtLastStep = hp;
        StepsTaken++;
    }


    public void Stop(RunStopReason reason)
    {
        if (!IsRunning) return;

        IsRunning = false;
        LastStopReason = reason;
    }
}
