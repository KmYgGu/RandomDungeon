using UnityEngine;

/// <summary>
/// 판 하나의 상태. <b>세이브 파일이 담아야 할 것이 곧 여기 있는 것이다.</b>
///
/// ── 왜 모았나 ─────────────────────────────────────────────────────
/// 판 상태는 층을 넘어 살아남아야 해서 GameManager 의 static 열두 개에 흩어져
/// 있었다. "판이란 무엇인가" 의 목록은 ResetRun() 하나뿐이었고, 새 상태를 더할
/// 때마다 그 목록에 한 줄 더하는 것을 잊지 않아야 했다. 잊으면 에디터에서는
/// 멀쩡하고 빌드에서만 죽은 판의 값을 물려받는다(GameManager.ResetRun 주석).
///
/// 이제 판 상태를 더하려면 <b>이 클래스에 필드를 하나 늘리고 Reset 에 한 줄</b>을
/// 쓴다. 세이브를 붙일 때도 이 객체 하나를 쓰고 읽으면 된다 — 무엇을 담아야
/// 하는지 GameManager 를 뒤질 필요가 없다.
///
/// ── 두 수명 ──────────────────────────────────────────────────────
///   판 단위 — 층을 넘어 남는다. 새 판에서 처음으로(<see cref="Reset"/>)
///   층 단위 — 층마다 비운다(<see cref="BeginFloor"/>). 새 판에서도 비운다
///
/// ── 여기 없는 것 ─────────────────────────────────────────────────
/// "지금 무슨 일이 벌어지는 중인가" 는 판 상태가 아니다 — 빨리감기 중인가,
/// 강제 이동 연출 중인가, 밟은 칸을 살폈는가, 지금 층의 타일맵. 저장했다가
/// 불러올 값이 아니라서 GameManager 에 남겼다. 도감·기록은 판을 넘어 남으므로
/// 역시 여기 없다(EncyclopediaStorage).
///
/// ── 갈아 끼우지 않는 것들 ────────────────────────────────────────
/// 이 객체 자체와 Log · Conditions · Marks · Party 는 <b>새로 만들지 않고 비운다.</b>
/// 메시지 창처럼 처음에 받아 들고 계속 쓰는 쪽이 있어서, 갈아 끼우면 그쪽은
/// 지난 판의 것을 계속 본다.
///
/// Unity 것은 값 타입(Vector2)뿐이라 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public sealed class RunState
{
    private readonly int bagCapacity;

    public RunState(int bagCapacity)
    {
        this.bagCapacity = bagCapacity;
        Stats = new PlayerStats();
        Bag = new Inventory(bagCapacity);
    }

    // ── 판 단위 ─────────────────────────────────────────────────────────────

    public PlayerStats Stats { get; private set; }
    public Inventory Bag { get; private set; }

    /// <summary>지금 착용 중인 장비. 새 판에서는 비고, GameManager 가 만든다.</summary>
    public Equipment Gear { get; set; }

    /// <summary>데리고 다니는 동료 명단. 오브젝트는 층마다 이 명단대로 다시 세워진다.</summary>
    public PartnerRoster Party { get; } = new PartnerRoster();

    /// <summary>메시지 창을 지나간 글. 상태창의 '로그' 가 읽는다.</summary>
    public MessageLog Log { get; } = new MessageLog();

    /// <summary>걸려 있는 상태이상과 남은 턴.</summary>
    public ConditionTracker Conditions { get; } = new ConditionTracker();

    /// <summary>아이템 종류마다 손으로 붙여 둔 표식. 그 판에서 알아낸 것이다.</summary>
    public MarkBook Marks { get; } = new MarkBook();

    /// <summary>몇 층인가. 0 은 아직 첫 층에 들어서기 전이다.</summary>
    public int Stage { get; private set; }

    /// <summary>이 층에 들어와 몇 턴이 지났는가. 층마다 0 부터 센다.</summary>
    public int TurnCount { get; set; }

    /// <summary>바라보는 방향. 공격과 던지기가 이 방향을 쓴다.</summary>
    public Vector2 LookDirection { get; set; } = Vector2.down;

    /// <summary>
    /// 공격이 지금 몇 번 연달아 빗나갔는가. 최고 기록은 기록장이 들고,
    /// 지금 몇 번째인지는 판이 도는 동안 여기서 센다.
    /// </summary>
    public int MissStreak { get; set; }

    // ── 층 단위 ─────────────────────────────────────────────────────────────

    /// <summary>바닥에 놓인 물건의 칸.</summary>
    public GroundObjectRegistry Ground { get; private set; } = new GroundObjectRegistry();

    /// <summary>이번 턴의 플레이어까지 걸음 수 지도.</summary>
    public ChaseMap Chase { get; private set; } = new ChaseMap();

    /// <summary>순찰하는 적들이 나눠 쓰는 문까지의 지도.</summary>
    public RouteCache PatrolRoutes { get; } = new RouteCache();

    /// <summary>방금 어디서 소리가 났는가.</summary>
    public NoiseBoard Noise { get; } = new NoiseBoard();

    // ── 수명 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 새 판. 판 단위와 층 단위를 전부 처음으로 되돌린다.
    ///
    /// 가방 속 오브젝트를 지우는 일은 하지 않는다 — Unity 의 일이라 부르는 쪽
    /// (GameManager.ResetRun)이 이것보다 <b>먼저</b> 한다. 뒤에 하면 이미 새
    /// 가방이라 지울 것을 찾지 못한다.
    /// </summary>
    public void Reset()
    {
        Stats = new PlayerStats();
        Bag = new Inventory(bagCapacity);

        // 장비는 아이템이 있어야 만들 수 있다. 곧 GameManager 가 만든다.
        Gear = null;

        Party.Clear();
        Log.Clear();
        Conditions.Clear();
        Marks.Clear();

        Stage = 0;              // 뒤이어 BeginFloor 가 1층으로 올린다.
        TurnCount = 0;
        LookDirection = Vector2.down;

        // 지난 판의 것이 이어지면 한 번도 그만큼 연달아 빗나간 적이 없는데도
        // 기록이 선다.
        MissStreak = 0;

        // 층 단위도 비운다. 바닥 목록과 추격 지도는 새로 만든다 — 예전부터 그랬다.
        Ground = new GroundObjectRegistry();
        Chase = new ChaseMap();
        ClearFloor();
    }

    /// <summary>
    /// 다음 층에 들어섰다. 층을 올리고 층 단위 상태를 비운다. 판 단위는 그대로 남는다.
    /// </summary>
    public void BeginFloor()
    {
        Stage++;
        TurnCount = 0;
        LookDirection = Vector2.down;

        Ground.Clear();
        ClearFloor();
    }

    private void ClearFloor()
    {
        // 지난 층의 소리를 새 층에서 따라가면 안 된다.
        Noise.Clear();

        // 지난 층의 지도는 새 층의 벽을 모른다.
        PatrolRoutes.Clear();
    }
}
