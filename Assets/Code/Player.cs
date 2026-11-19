using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 플레이어 조작. 입력을 읽어 "이동 / 공격 / 달리기 / 가방 열기" 중 하나를 고르고,
/// 결과 판정은 GameManager 와 PlayerStats 에 맡긴다.
/// </summary>
public class Player : MovingObject
{
    /// <summary>공격이 맞을 확률(10%는 빗나감).</summary>
    private const float HitChance = 0.9f;

    /// <summary>
    /// 치명타를 내는 적이 실제로 치명타를 낼 확률(%).
    ///
    /// <b>확률은 여기 하나뿐이다.</b> 시트(Monster 의 critical)는 "내는 적인가" 만
    /// 1/0 으로 정한다. 적마다 숫자를 적게 하면 균형을 다시 잡을 때 표의 모든 줄을
    /// 만져야 하고, 그러다 한 줄만 옛 값으로 남으면 그 적만 조용히 다르게 논다.
    /// </summary>
    private const int IncomingCriticalPercent = 20;

    /// <summary>
    /// 적의 치명타 배율. 어느 적이 내든 배율은 같다.
    /// 플레이어가 낼 때와도 같은 값이다 — 한쪽만 다르면 그 차이를 설명할 길이 없다.
    /// </summary>
    private const double IncomingCriticalMultiplier = 1.8;

    /// <summary>달리는 중에 이 거리 안으로 적이 들어오면 멈춘다(대각선 포함 한 칸).</summary>
    private const float EnemyAlertRadius = 1.5f;

    /// <summary>적 턴이 이만큼 기다려도 안 끝나면 비정상으로 보고 달리기를 접는다.</summary>
    private const int TurnStallLimit = 120;

    public GameObject ArrowTransform;

    /// <summary>
    /// 방향키를 새로 누른 뒤, 대각선을 만들 옆 키를 기다려 주는 시간(초).
    ///
    /// 사람이 두 키를 완전히 동시에 누르는 것은 불가능해서 보통 수십 ms 차이가 난다.
    /// 이 시간이 0 이면 먼저 눌린 키로 곧장 한 칸 가 버리고 그다음부터 대각선이 된다.
    /// 너무 길면 조작이 굼떠 보이므로 0.05~0.08 정도가 적당하다.
    /// 지연이 전혀 없는 편이 좋다면 0 으로 두고 Shift + 방향키로 대각선을 쓰면 된다.
    /// </summary>
    [Range(0f, 0.2f)]
    public float diagonalGraceSeconds = 0.06f;

    /// <summary>
    /// 플레이어가 지금 어떤 상태인가. 상태마다 받아들이는 입력이 다르다.
    ///
    /// 예전에는 isAiming 과 runner.IsRunning 이라는 별개의 플래그를 조합해서
    /// Update 안의 긴 if 사슬로 처리했고, 검사 순서가 곧 규칙이 되어 버렸다.
    /// 상태를 하나의 값으로 두면 "이 상태에서 이 키가 먹히는가" 가
    /// 각 상태의 처리 함수 안에만 적혀 있게 된다.
    /// </summary>
    private enum Mode
    {
        /// <summary>평소. 이동·공격·가방·조준·달리기 모두 가능.</summary>
        Normal,
        /// <summary>조준. 제자리에서 방향만 바꾼다.</summary>
        Aiming,
        /// <summary>달리는 중. 입력을 읽지 않고 RunRoutine 이 진행한다.</summary>
        Running,

        /// <summary>
        /// 공격 같은 행동이 끝나기를 기다리는 중. 입력을 읽지 않는다.
        ///
        /// 이 상태가 없으면 공격 연출이 도는 동안에도 조작이 살아 있어서,
        /// 한 턴에 공격과 이동을 함께 하고 적에게는 턴을 두 번 주게 된다.
        /// </summary>
        Acting,
    }

    private Mode mode = Mode.Normal;

    private readonly AutoRunner runner = new AutoRunner();
    private readonly DirectionInput directionInput = new DirectionInput();

    /// <summary>지난 프레임에 플레이어가 입력 권한을 갖고 있었는가.</summary>
    private bool hadFocus = true;

    /// <summary>
    /// 달리기 요청이 들어왔고, 방향이 확정되기를 기다리는 중.
    /// 방향과 B 를 거의 동시에 누른 경우 대각선 유예가 끝날 때까지 한두 프레임 걸린다.
    /// </summary>
    private bool runRequested;

    /// <summary>
    /// 이번 걸음이 "줍지 않고 지나간다" 인가.
    ///
    /// 밟은 물건을 집지 않고 그 위에 올라서기만 한다. 주우면 사라지는 것
    /// (돈·화살 등)을 일부러 남겨 두고 지나갈 수단이 필요해서다.
    /// 평소 걸음으로 밟았을 때는 그대로 줍는다.
    ///
    /// 예전에는 달리기 전용이었고 이름도 "달려서 왔는가" 였다.
    /// 그런데 적이 옆에 붙으면 달리기가 시작조차 되지 않으므로(RunRoutine 첫 검사),
    /// 쫓기는 동안에는 "아이템 위에 올라서서 그것을 던진다" 라는 수를 아예 쓸 수 없었다.
    /// 지금은 한 걸음짜리 이동도 이 표시를 쓴다.
    /// </summary>
    private bool steppingWithoutTaking;

    /// <summary>방 안쪽 영역들. 달리기가 방 경계를 넘었는지 판단하는 데 쓴다.</summary>
    private List<Rect> roomRects = new List<Rect>();

    // 판정 좌표는 기존 격자를 유지하고 표시만 보간한다.
    // 공격/워프가 Sprite 로컬 좌표를 바꿔도 걷기 표시와 충돌하지 않도록 부모를 분리한다.
    private Transform walkPresentation;
    private Vector3 walkStart;
    private Vector3 walkEnd;
    private float walkStartedAt;
    private bool walkingPresentation;

    protected override void MoveTo(Vector2 end)
    {
        walkStart = walkingPresentation && walkPresentation != null
            ? walkPresentation.position : transform.position;
        walkEnd = new Vector3(end.x, end.y, transform.position.z);
        walkStartedAt = Time.time;
        walkingPresentation = !GameManager.FastForward && mode != Mode.Running;
        base.MoveTo(end);
    }

    /// <summary>카메라의 LateUpdate 에서 표시와 카메라에 같은 좌표를 적용한다.</summary>
    public Vector3 UpdateWalkPresentation()
    {
        if (walkPresentation == null) return transform.position;
        if (!walkingPresentation)
        {
            walkPresentation.localPosition = Vector3.zero;
            return transform.position;
        }
        bool immediate = GameManager.FastForward || mode == Mode.Running ||
            mode == Mode.Acting || !InputFocus.PlayerHasFocus;
        float progress = CameraFollowMotion.Progress(Time.time - walkStartedAt, moveTime, immediate);
        if (progress >= 1f)
        {
            walkingPresentation = false;
            walkPresentation.localPosition = Vector3.zero;
            return transform.position;
        }
        Vector3 displayed = Vector3.Lerp(walkStart, walkEnd, progress);
        walkPresentation.position = displayed;
        return displayed;
    }
    protected override void Start()
    {
        ArrowTransform = GameObject.Find("2arrow");
        if (ArrowTransform != null) ArrowTransform.SetActive(false);
        else Debug.LogWarning("조준 화살표(2arrow)를 씬에서 찾지 못했습니다. 조준 방향이 화면에 보이지 않습니다.", this);

        directionInput.DiagonalGraceSeconds = diagonalGraceSeconds;

        // 적과 같은 방식으로 이번 층의 방 정보를 받아 둔다.
        RandomDung dungeon = GameManager.instance.MapMake;
        if (dungeon != null) roomRects = dungeon.RoomData;

        // 서 있는 자리를 알려 둔다. 이 값은 이동할 때만 갱신되므로, 층에 들어와
        // 한 번도 움직이지 않은 채 물건을 놓으면 지난 층 좌표에 놓이고 있었다.
        GameManager.instance.PlayerPosition = transform.position;

        // 시작 칸은 "이미 살펴본 것" 으로 둔다.
        //
        // 이 값은 '도착' 단위의 표시라(TryStep 참고), 열어 둔 채로 층이 시작되면
        // 첫 물리 프레임에 발밑의 것이 곧바로 처리된다. 그러면 층에 들어서자마자
        // 아무 입력도 하기 전에 아이템을 줍거나 함정을 밟은 것이 되고,
        // 함정이라면 손쓸 새 없이 죽을 수도 있다.
        //
        // 던전을 만들 때도 시작 칸에 물건·함정이 놓이지 않도록 피하고 있지만
        // (DungeonPopulator), 그 규칙이 어떤 이유로 무너져도 시작하자마자
        // 당하는 일만은 없도록 여기서 한 겹 더 막는다.
        //
        // 발밑 명령은 RememberUnderFoot 로 따로 기억해 두므로 그대로 쓸 수 있다.
        // 한 걸음 떼면(TryStep) 평소처럼 열린다.
        GameManager.InteractionHandled = true;

        base.Start();
        walkPresentation = new GameObject("WalkPresentation").transform;
        walkPresentation.SetParent(transform, false);
        childTransform.SetParent(walkPresentation, false);
    }

    private void Update()
    {
        // 1. 입력 권한부터 확인한다. 권한이 없으면 어떤 키도 읽지 않는다.
        //    이 검사가 맨 앞에 있으므로, 가방이 열린 채로 조준 토글이 먹는 일은 없다.
        if (!InputFocus.PlayerHasFocus)
        {
            if (hadFocus)
            {
                StopRun(RunStopReason.UiInterrupted);
                hadFocus = false;
            }
            return;
        }

        // 2. 권한을 막 되찾았다면, 누르고 있던 키가 새어 들어오지 않게 잠근다.
        //    창을 닫은 그 B 를 떼는 순간이 '짧게 누름' 으로 잡혀 대기 한 턴이
        //    공짜로 지나가 버리는 것도 함께 막는다.
        if (!hadFocus)
        {
            hadFocus = true;
            directionInput.RequireRelease();
            GameInput.ConsumeAssist();
            runRequested = false;
        }

        // 3. 달리기나 공격이 진행 중이면 그쪽이 끝날 때까지 입력을 읽지 않는다.
        if (mode == Mode.Running || mode == Mode.Acting) return;

        // 4. 내 차례가 아니어도 손을 뗐는지는 계속 지켜본다.
        //    적이 움직이는 사이에 뗐다 다시 누른 경우를 놓치지 않기 위해서다.
        if (!GameManager.instance.playersTurn)
        {
            directionInput.PollRelease();
            return;
        }

        switch (mode)
        {
            case Mode.Normal:
                HandleNormalInput();
                break;
            case Mode.Aiming:
                HandleAimingInput();
                break;
        }
    }

    /// <summary>
    /// 평소 상태의 입력.
    ///
    ///   A             : 공격
    ///   B 누른 채 방향 : 달리기 (적이 붙어 있거나 굶주리면 한 걸음만, 줍지 않고)
    ///   B 살짝        : 조준 모드
    ///   A + B         : 제자리 대기(한 턴 넘김)
    ///
    /// B + 방향이 하는 일은 어느 쪽이든 "물건을 건드리지 않고 간다" 로 같다.
    /// 한 입력에 여러 턴이 지나가면 손쓸 수 없게 되는 상황에서만
    /// 여러 칸을 내달리지 않는다(MustStepOneAtATime).
    /// </summary>
    private void HandleNormalInput()
    {
        // 잠들어 있으면 아무것도 못 한다. 입력을 읽기 전에 턴부터 넘긴다.
        //
        // 입력을 무시하기만 하면 게임이 멈춘 것처럼 보인다. 턴은 흘러야
        // 남은 잠이 줄어들고, 그래야 언젠가 깨어난다.
        //
        // 아무도 없는 곳에서 자면 화면에서도 아무 일이 일어나지 않아, 턴이 도는
        // 것조차 보이지 않는다. 그래서 자고 있다는 것을 매 턴 알린다.
        // 같은 말이 이미 떠 있으면 다시 띄우지 않으므로 이력이 더러워지지 않는다.
        if (!GameManager.PlayerCanAct)
        {
            GameManager.instance.ShowMessageUnlessRepeated(GameMessage.Sleeping);
            WaitOneTurn();
            return;
        }

        // 개발용: 계단을 찾지 않고 바로 다음 층으로 내려간다.
        if (GameInput.SkipFloorPressed)
        {
            GameManager.instance.DescendImmediately();
            return;
        }

        // 방향 상태를 프레임마다 한 번 갱신한다(대각선 유예가 여기서 진행된다).
        bool hasDirection = directionInput.TryRead(
            Input.GetKey(InputKeys.DiagonalLock), out Vector2 direction);

        // 두 버튼을 함께 눌렀다면 대기다. 다른 어떤 판정보다 먼저 본다.
        //
        // 누르고 있는 동안 계속 쉰다. 한 턴이 끝나면 적이 움직이고, 내 차례가
        // 돌아왔을 때 여전히 눌려 있으면 또 한 턴을 넘긴다. 따로 시간을 재지
        // 않아도 턴 자체가 속도를 정해 준다(적 턴의 연출 대기).
        //
        // 다만 적이 옆에 붙었거나 굶주릴 때는 이어 가지 않는다. 달리기가 같은
        // 상황에서 한 걸음만 가는 것과 같은 이유이고, 판정도 같은 것을 쓴다 —
        // 손쓸 수 없는 사이에 여러 턴이 지나가면 안 된다. 그때는 누를 때마다
        // 한 턴씩만 쉰다(WaitRequested 는 새로 누른 순간에만 참이다).
        if (GameInput.WaitRequested || (GameInput.WaitHeld && !MustStepOneAtATime()))
        {
            GameInput.ConsumeAction();
            GameInput.ConsumeAssist();
            runRequested = false;
            WaitOneTurn();
            return;
        }

        if (GameInput.ActionPressed) { Attack(); return; }
        if (GameInput.AimShortcutPressed) { EnterAiming(); return; }

        // PC 임시: 최종적으로는 체력/인벤토리 UI 를 눌러서 연다.
        if (GameInput.BagShortcutPressed) { GameManager.instance.OpenBag(); return; }

        // 캐릭터 상태창. 턴을 쓰지 않고 들여다보기만 한다.
        if (GameInput.StatusPressed) { GameManager.instance.OpenStatus(); return; }

        // B 를 누르고 있는 동안 방향을 밀면 달린다.
        // 누르는 순서는 상관없다 — B 먼저든 방향 먼저든 똑같이 달린다.
        // 자리 바꾸기로 이번 눌림을 다 썼으면(AssistHeldAvailable) 손을 뗐다
        // 다시 눌러야 달린다. 안 그러면 바꾸자마자 그대로 내달린다.
        if (GameInput.AssistHeldAvailable && directionInput.IsPressed)
        {
            GameInput.ConsumeAssist();
            runRequested = true;
        }

        if (runRequested)
        {
            // 방향을 놓았으면 없던 일로 한다.
            if (!directionInput.IsPressed) { runRequested = false; }
            else if (hasDirection)
            {
                // 대각선 유예가 끝나 방향이 확정된 뒤에 출발한다.
                runRequested = false;
                GameManager.LookDirection = direction;
                FaceDirection(direction);

                // 앞이 동료면 달리는 대신 자리를 바꾼다.
                // 좁은 통로에서 동료가 앞을 막으면 이것 말고는 지나갈 길이 없다.
                if (TrySwapWithPartner(direction)) return;

                // 내달려서는 안 되는 상황이면 한 걸음만 간다.
                // 아무 일도 안 하면 "아이템 위에 올라서서 그것을 던진다" 같은 수가
                // 막혀 버리므로, 달리기 대신 한 칸짜리로 낮춰서 내준다.
                if (MustStepOneAtATime())
                    StepWithoutTaking(direction);
                else
                    BeginRun(direction);
            }
            return;
        }

        // 아무 일에도 쓰지 않고 B 를 뗐다면 조준이다.
        if (GameInput.AssistTapped) { EnterAiming(); return; }

        if (!hasDirection) return;

        // 혼란 상태면 가려던 쪽이 아니라 엉뚱한 쪽으로 간다.
        direction = ScrambleIfConfused(direction);

        GameManager.LookDirection = direction;
        FaceDirection(direction);

        steppingWithoutTaking = false;
        TryStep(direction);
    }

    /// <summary>
    /// 그 칸에 아직 남인 동료가 서 있으면 말을 건다. 걸었으면 true.
    ///
    /// 턴을 쓰지 않는다 — 말을 거는 것은 자리를 옮기지도 때리지도 않는데,
    /// 그 사이에 적이 한 걸음 다가오면 말을 걸었다는 이유로 손해를 본다.
    /// </summary>
    private bool TryTalkToPartner(Vector2 destination)
    {
        // 중립인 상인도 말을 거는 상대다. 돌아선 뒤에는 적이라 아래 공격으로 넘어간다.
        Merchant merchant = Merchant.At(destination);
        if (merchant != null && !merchant.Hostile)
        {
            StartCoroutine(merchant.Shop.Talk());
            return true;
        }

        Partner partner = GameManager.instance.PartnerAt(destination);
        if (partner == null) return false;

        // 아직 남이면 동행을 묻고, 이미 동료면 안부를 듣는다.
        if (partner.Joined) GameManager.instance.TalkToPartner(partner);
        else StartCoroutine(GameManager.instance.PromptJoin(partner));

        return true;
    }

    /// <summary>
    /// B 를 누른 채 동료에게 걸어 들어가면 <b>서로 자리를 맞바꾼다.</b>
    ///
    /// 시리즈에서 동료 시스템에 반드시 있는 장치다. 1칸 통로에서 동료가 앞을
    /// 막으면 이것 없이는 지나갈 방법이 아예 없다.
    ///
    /// ── 바꾼 뒤에는 달리지 않는다 ─────────────────────────────────
    /// 이번 B 눌림을 다 쓴 것으로 표시하고(SpendAssist) 끝낸다. 그러지 않으면
    /// B 를 누른 채였으므로 다음 프레임에 곧바로 달리기가 걸려, 자리만 바꾸려던
    /// 손이 통로 끝까지 내달린다. 다시 달리려면 B 를 뗐다 눌러야 한다.
    ///
    /// 대각선 규칙은 걸어갈 때와 같다. 모서리를 못 빠져나가는 자리는 바꿀 수도 없다 —
    /// 여기서만 봐주면 벽을 뚫고 자리를 바꾸는 길이 생긴다.
    /// </summary>
    private bool TrySwapWithPartner(Vector2 direction)
    {
        Vector2 here = transform.position;
        Vector2 destination = here + direction;

        Partner partner = GameManager.instance.PartnerAt(destination);
        if (partner == null) return false;

        if (!CanMoveDiagonally(here, direction)) return false;

        partner.SwapTo(here);
        MoveTo(destination);

        // 옮긴 자리를 알린다. <b>이걸 빠뜨리면 서로 겹친다.</b>
        //
        // 동료는 "플레이어가 선 칸" 을 GameManager.PlayerPosition 으로 판단하는데
        // (Partner.CanEnter), 갱신하지 않으면 그 값이 <b>방금 비운 칸</b>을 가리킨다.
        // 그래서 동료는 플레이어의 새 칸을 빈 칸으로 보고 그리로 들어와 버린다.
        // 걸어서 옮길 때는 TryStep 이 해 주던 일이라, 자리 바꾸기에만 구멍이 있었다.
        GameManager.instance.PlayerPosition = destination;

        GameInput.SpendAssist();

        // 자리를 바꾼 것도 한 걸음이다. 공짜로 지나가면 동료를 벽처럼 밀고 다닌다.
        GameManager.instance.EndPlayerTurn();
        return true;
    }

    /// <summary>
    /// 혼란에 걸려 있으면 여덟 방향 중 아무 쪽으로나 바꾼다.
    ///
    /// 방향을 <b>바꾸기만</b> 하고 걸음 자체는 그대로 둔다. 아예 못 움직이게 하면
    /// 잠과 다를 바가 없고, 혼란은 "가긴 가는데 엉뚱한 데로 간다" 가 재미다.
    ///
    /// 바뀐 방향이 원래와 같을 수도 있다. 여덟 중 하나는 제대로 가는 셈인데,
    /// 그래야 혼란에 걸린 채로도 운이 좋으면 빠져나갈 수 있다.
    /// </summary>
    private Vector2 ScrambleIfConfused(Vector2 direction)
    {
        if (!GameManager.PlayerDirectionScrambled) return direction;

        return GridUtil.EightDirections[Random.Range(0, GridUtil.EightDirections.Length)];
    }

    /// <summary>
    /// 조준 상태의 입력. 방향만 바뀌고 이동은 하지 않는다.
    ///
    ///   A      : 그 방향으로 공격
    ///   B 살짝 : 다음 적으로 조준을 옮긴다. 옮길 곳이 없으면 조준 해제
    ///   B 길게 : 조준 해제
    ///   방향   : 손으로 조준
    ///   A + B  : 제자리 대기
    ///
    /// 짧게 누름이 상황에 따라 두 가지 일을 하는 것처럼 보이지만, 규칙은 하나다.
    /// "옮길 곳이 있으면 옮기고, 없으면 끝낸다." 그래서 눌러도 아무 일이 없는
    /// 경우가 없다. 적이 여럿일 때는 끝까지 돌지 않고도 끝낼 수 있어야 하므로
    /// 길게 누름이 따로 필요하다.
    /// </summary>
    private void HandleAimingInput()
    {
        if (GameInput.WaitRequested)
        {
            GameInput.ConsumeAction();
            GameInput.ConsumeAssist();
            WaitOneTurn();
            return;
        }

        if (GameInput.ActionPressed) { Attack(); return; }

        // B 를 길게 누르고 있으면 조준을 끝낸다.
        // 떼기를 기다리지 않고 기준 시간이 넘는 순간 바로 끝내야, 얼마나 더 눌러야
        // 하는지 몰라 손이 머뭇거리는 일이 없다. 여기서 소비해 두면 나중에 손을 뗄 때
        // Tapped 가 다시 나와 조준이 옮겨지는 일도 막힌다.
        if (GameInput.AssistHeldLong)
        {
            GameInput.ConsumeAssist();
            EnterMode(Mode.Normal);
            return;
        }

        // PC 임시 단축키는 예전처럼 곧바로 조준을 끈다.
        if (GameInput.AimShortcutPressed)
        {
            EnterMode(Mode.Normal);
            return;
        }

        // B 를 짧게 누르면 겨눌 수 있는 다음 적으로 옮긴다.
        // 옮길 곳이 없으면(겨눌 적이 없거나, 하나뿐인데 이미 그것을 겨누고 있으면)
        // 조준을 끝낸다. 그러지 않으면 그 상황에서 짧게 누름이 아무 일도 하지 않아,
        // 버튼이 먹은 것인지 원래 그런 것인지 알 수 없다.
        if (GameInput.AssistTapped)
        {
            GameInput.ConsumeAssist();

            if (!TryCycleAimTarget())
                EnterMode(Mode.Normal);

            return;
        }

        if (!directionInput.TryRead(Input.GetKey(InputKeys.DiagonalLock), out Vector2 direction)) return;

        GameManager.LookDirection = direction;
        FaceDirection(direction);
    }

    /// <summary>
    /// 제자리에서 한 턴을 넘긴다.
    ///
    /// 체력이 자연 회복되기를 기다리거나, 통로 입구에서 적이 오기를 기다릴 때 쓴다.
    /// 예전에는 이 기능이 없어서 빈 공간에 공격을 휘둘러 대신하고 있었다.
    /// </summary>
    private void WaitOneTurn()
    {
        GameManager.instance.EndPlayerTurn();
    }

    /// <summary>
    /// 조준 모드로 들어간다.
    ///
    /// 조준 방향의 기본값은 지금 캐릭터가 바라보는 쪽이다.
    /// 예전에는 화살표를 켜기만 해서, 지난번에 조준했던 엉뚱한 방향이 그대로 남아 있었다.
    ///
    /// 겨누는 순서는 넷이다.
    ///   1순위 - 바로 옆(대각선 포함)에 붙어 있는 적
    ///   2순위 - 벽에 가리지 않고 직선으로 겨눌 수 있는 적(같은 방이거나 마주 보이는 통로)
    ///   3순위 - 바로 옆(대각선 포함)에 있는 동료(중립이든 이미 합류했든 같다)
    ///   기본  - 지금 바라보는 쪽
    ///
    /// 붙어 있는 적을 먼저 보는 이유는, 조준이 대개 그 적을 때리려고 켜는 것이기
    /// 때문이다. 멀리 있는 적이 더 곧게 놓였다고 그쪽을 겨누면 손이 어긋난다.
    /// 동료는 적보다 뒤인데, 적이 있을 땐 그쪽을 보는 게 우선이고 동료는
    /// A 로 때리는 대신 말을 거는 것뿐이라 급하지 않기 때문이다.
    /// </summary>
    private void EnterAiming()
    {
        Vector2 aim;

        if (TryFindAdjacentEnemy(out Vector2 towardAdjacent))
            aim = towardAdjacent;
        else if (TryFindEnemyInLine(out Vector2 towardDistant))
            aim = towardDistant;
        else if (TryFindAdjacentPartner(out Vector2 towardPartner))
            aim = towardPartner;
        else
            aim = GameManager.LookDirection;

        GameManager.LookDirection = aim;
        EnterMode(Mode.Aiming);

        // 화살표는 조준 상태일 때만 회전하므로 EnterMode 뒤에 불러야 한다.
        FaceDirection(aim);
    }

    /// <summary>
    /// 한 칸 이내(대각선 포함)에 있는 적의 방향. 없으면 false.
    ///
    /// 여럿이면 지금 바라보는 쪽에 가장 가까운 적을 고르고,
    /// 그것도 비슷하면 대각선보다 직선 방향을 우선한다.
    /// </summary>
    private bool TryFindAdjacentEnemy(out Vector2 direction)
    {
        Vector2 origin = transform.position;
        Vector2 facing = GameManager.LookDirection;

        direction = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            // 정체를 숨긴 적은 바로 옆에 있어도 겨누지 않는다. 2순위와 같은 규칙이다.
            if (!enemy.CanBeAimedAt) continue;

            ConsiderAdjacent(enemy.transform.position, origin, facing, ref bestScore, ref direction);
        }

        return direction != Vector2.zero;
    }

    /// <summary>
    /// 한 칸 이내(대각선 포함)에 있는 동료의 방향. 없으면 false.
    ///
    /// 아직 남인 동료도 이미 합류한 동료도 똑같이 본다 — 이 우선순위는
    /// "근처에 동료가 있다" 는 사실만 보는 것이지 합류 여부를 가리지 않는다.
    /// 여럿이면 지금 바라보는 쪽에 가장 가까운 쪽을 고르고, 그것도 비슷하면
    /// 대각선보다 직선 방향을 우선한다. 1순위(붙어 있는 적)와 같은 기준이다.
    /// </summary>
    private bool TryFindAdjacentPartner(out Vector2 direction)
    {
        Vector2 origin = transform.position;
        Vector2 facing = GameManager.LookDirection;

        direction = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        foreach (Partner partner in GameManager.instance.partners)
        {
            if (partner == null || !partner.gameObject.activeInHierarchy) continue;

            ConsiderAdjacent(partner.transform.position, origin, facing, ref bestScore, ref direction);
        }

        return direction != Vector2.zero;
    }

    /// <summary>
    /// 바로 옆(대각선 포함)에 선 것 하나를 조준 후보로 견줘 본다.
    /// 지금까지의 후보보다 나으면 bestScore 와 direction 을 갈아 끼운다.
    ///
    /// 적과 동료가 <b>같은 기준</b>으로 뽑혀야 해서 한 곳에 둔다. 따로 두었을 때는
    /// 서른 줄이 글자까지 같았고, 한쪽만 고치면 "적은 직선 우선인데 동료는 아닌"
    /// 식으로 조용히 갈라질 자리였다.
    /// </summary>
    private static void ConsiderAdjacent(
        Vector2 position, Vector2 origin, Vector2 facing,
        ref float bestScore, ref Vector2 direction)
    {
        Vector2 offset = position - origin;
        if (Mathf.Abs(offset.x) > 1.1f || Mathf.Abs(offset.y) > 1.1f) return;

        var step = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
        if (step == Vector2.zero) return;

        float score = Vector2.Dot(step.normalized, facing.normalized);

        // 점수가 같을 때 직선 쪽을 먼저 겨눈다.
        bool cardinal = step.x == 0f || step.y == 0f;
        if (cardinal) score += 0.01f;

        if (score <= bestScore) return;

        bestScore = score;
        direction = step;
    }

    /// <summary>
    /// 벽에 가리지 않고 직선으로 겨눌 수 있는 적의 방향. 없으면 false.
    ///
    /// 어느 쪽을 고를지는 AimTargeting 이 정한다. 여기서는 씬에 흩어져 있는 것들
    /// (타일맵, 적 목록)을 칸 좌표로 바꿔 넘겨 주기만 한다.
    /// </summary>
    private bool TryFindEnemyInLine(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!TryBuildAimContext(out TilemapWalkableGrid grid,
                                out Vector3Int origin,
                                out List<Vector3Int> enemyCells,
                                out int maxDistance))
            return false;

        Vector2 facing = GameManager.LookDirection;
        var facingCell = new Vector2Int(
            Mathf.RoundToInt(facing.x), Mathf.RoundToInt(facing.y));

        if (!AimTargeting.TryFindInLine(
                grid, origin, enemyCells, facingCell, maxDistance, out Vector2Int found))
            return false;

        direction = new Vector2(found.x, found.y);
        return true;
    }

    /// <summary>
    /// 겨눌 수 있는 다음 적으로 조준을 옮긴다. 옮길 곳이 없었으면 false.
    ///
    /// 겨눌 적이 하나뿐이어도, 지금 엉뚱한 쪽을 겨누고 있었다면 그 하나로 옮겨 준다.
    /// 벽을 향한 채 조준을 끝내는 것보다 그편이 쓸모 있다.
    /// 이미 그 하나를 겨누고 있었다면 옮길 곳이 없으므로 false 가 되고,
    /// 부르는 쪽이 조준을 끝낸다.
    /// </summary>
    private bool TryCycleAimTarget()
    {
        if (!TryBuildAimContext(out TilemapWalkableGrid grid,
                                out Vector3Int origin,
                                out List<Vector3Int> enemyCells,
                                out int maxDistance))
            return false;

        Vector2 aim = GameManager.LookDirection;
        var current = new Vector2Int(Mathf.RoundToInt(aim.x), Mathf.RoundToInt(aim.y));

        List<Vector2Int> aimable =
            AimTargeting.CollectAimable(grid, origin, enemyCells, maxDistance);

        if (!AimTargeting.TryCycle(aimable, current, out Vector2Int next)) return false;

        var direction = new Vector2(next.x, next.y);
        GameManager.LookDirection = direction;
        FaceDirection(direction);
        return true;
    }

    /// <summary>
    /// 조준 판정에 필요한 것들을 씬에서 긁어모은다.
    ///
    /// 정체를 숨기고 있는 적(변장·투명)은 여기서 빠진다. 조준이 그런 적을 집어 주면
    /// 그것만으로 정체가 드러나 버리기 때문이다. 처음 겨눌 때와 다음 적으로 옮길 때가
    /// 같은 목록을 보도록 한 곳에 모아 두었다.
    /// </summary>
    private bool TryBuildAimContext(
        out TilemapWalkableGrid grid,
        out Vector3Int origin,
        out List<Vector3Int> enemyCells,
        out int maxDistance)
    {
        grid = null;
        origin = Vector3Int.zero;
        enemyCells = null;
        maxDistance = 0;

        Tilemap floor = GameManager.FloorTilemap;
        if (floor == null) return false;

        grid = new TilemapWalkableGrid(floor);
        origin = grid.WorldToCell(transform.position);

        enemyCells = new List<Vector3Int>();
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            if (!enemy.CanBeAimedAt) continue;

            enemyCells.Add(grid.WorldToCell(enemy.transform.position));
        }

        if (enemyCells.Count == 0) return false;

        // 벽이 알아서 사거리를 막아 주므로, 층을 가로지르는 길이면 충분하다.
        BoundsInt bounds = grid.Bounds;
        maxDistance = Mathf.Max(bounds.size.x, bounds.size.y);
        return true;
    }

    /// <summary>
    /// 상태를 바꾼다. 상태가 바뀔 때 해야 할 일은 전부 여기 모여 있다.
    /// </summary>
    private void EnterMode(Mode next)
    {
        SetMode(next);

        // 상태를 바꾼 그 프레임의 눌린 방향키가 새 상태로 새어 들어가지 않게 한다.
        directionInput.RequireRelease();
    }

    /// <summary>
    /// 상태 값과 조준 화살표를 <b>함께</b> 바꾼다. mode 에 직접 대입하는 곳은 여기뿐이다.
    ///
    /// 예전에는 공격 · 워프 · 날려지기 · 함정 몸짓이 저마다 mode 에 직접 대입했다.
    /// 그러면 화살표가 따라 바뀌지 않아서, 조준 중에 워프 함정을 밟으면 상태는
    /// 평소인데 화살표만 떠 있는 채로 남았다.
    ///
    /// EnterMode 와 다른 점은 <b>방향키를 잠그지 않는다</b>는 것 하나다. 연출이
    /// 끝나고 돌아올 때까지 잠그면, 방향키를 누른 채 공격을 섞어 가며 걷는
    /// 손이 공격할 때마다 키를 뗐다 눌러야 한다.
    ///
    /// Acting 은 잠깐 지나가는 상태라 화살표를 건드리지 않는다. 조준한 채 휘두르는
    /// 동안에는 화살표가 그대로 보이다가, 끝나고 돌아오는 상태에 맞춰 정리된다.
    /// </summary>
    private void SetMode(Mode next)
    {
        mode = next;

        if (next != Mode.Acting && ArrowTransform != null)
            ArrowTransform.SetActive(next == Mode.Aiming);
    }

    /// <summary>
    /// 달리기를 멈춘다. 모든 정지 경로가 이 메서드 하나를 지나가므로,
    /// "멈출 때 반드시 해야 하는 일" 을 빠뜨릴 수 없다.
    /// </summary>
    public void InterruptForMonsterHouse() => InterruptForRoomEvent();

    public void InterruptForRoomEvent()
    {
        StopRun(RunStopReason.UiInterrupted);
        GameManager.FastForward = false;
    }

    private void StopRun(RunStopReason reason)
    {
        if (!runner.IsRunning) return;

        runner.Stop(reason);

        // 어떤 경로로 끝나든 빨리감기가 켜진 채로 남지 않게 한다.
        GameManager.FastForward = false;

        // 조준 중에 달리기를 시작할 수는 없으므로 언제나 평소 상태로 돌아온다.
        // EnterMode 가 방향키 잠금까지 걸어 주기 때문에, 멈춘 자리에서
        // 누르고 있던 키 때문에 한 칸 더 걸어가는 일이 없다.
        EnterMode(Mode.Normal);
    }

    /// <summary>
    /// 한 칸 이동하고, 성공했으면 턴을 넘긴다. 실제로 움직였으면 true.
    ///
    /// 예전에는 Player.AttemptMove 가 base.AttemptMove 를 부른 뒤 Move 를 한 번 더
    /// 불러서, 한 번의 입력에 이동 판정이 두 번 돌고 있었다.
    /// </summary>
    private bool TryStep(Vector2 direction)
    {
        Vector2 destination = (Vector2)transform.position + direction;
        // 값을 안 치른 물건을 든 채로는 상점을 나서지 못한다.
        if (ShopRoom.BlocksWalk(transform.position, destination)) return false;

        if (!Move(direction, out _)) return false;

        // 여기부터는 실제로 칸을 옮겼다.
        //
        // "밟은 것을 다시 살펴본다" 는 문은 반드시 옮겨 간 뒤에만 연다.
        // 예전에는 부르는 쪽에서 Move 보다 먼저 열었는데, 벽이나 적에 막혀 제자리인
        // 경우에도 문이 열린 채로 남았다. 그러면 지금 밟고 선 물건을 그 프레임에
        // 집어 들면서, 이동이 실패했으니 턴은 쓰지 않는다. 즉 벽을 향해 미는 것만으로
        // 발밑의 물건을 공짜로 가져갈 수 있었다.
        GameManager.InteractionHandled = false;

        GameManager.instance.PlayerPosition = destination;
        GameManager.instance.GameOver();
        GameManager.instance.EndPlayerTurn();
        return true;
    }

    // ── 달리기(오토런) ──────────────────────────────────────────────────────

    /// <summary>
    /// 지금은 여러 칸을 내달리면 안 되는 상황인가.
    ///
    /// 둘 다 "한 입력에 여러 턴이 지나가면 손쓸 수 없게 된다" 는 같은 이유다.
    ///
    ///   적이 붙어 있다 - 앞을 보지 않고 내달리면 그대로 얻어맞는다.
    ///
    ///   굶주리는 중이다 - 굶주림 피해는 다섯 턴에 한 번이 아니라 매 턴 1씩이다
    ///   (GameManager.ApplyHungerTick). 달리기는 최대 60칸이므로 한 번 달리는 것만으로
    ///   60을 잃을 수 있다. 최대 체력이 스물 남짓인 게임에서 그건 입력 한 번에
    ///   죽는다는 뜻이고, 도중에 먹을 틈조차 없다.
    ///
    ///   기존의 "피해를 입으면 멈춘다" 검사로는 이걸 잡지 못한다. 그 검사는 걸음마다
    ///   기록해 둔 체력과 비교하는데, 굶주림 피해는 그 기록보다 먼저 적용되어
    ///   두 값이 늘 같아지기 때문이다.
    /// </summary>
    private bool MustStepOneAtATime()
        => IsEnemyNearby(transform.position) || GameManager.Stats.IsStarving;

    /// <summary>
    /// 한 걸음만, 밟은 물건을 집지 않고 간다.
    ///
    /// 달리기의 한 걸음과 똑같이 처리한다. 다른 점은 다음 걸음을 잇지 않는다는 것뿐이다.
    /// </summary>
    private void StepWithoutTaking(Vector2 direction)
    {
        steppingWithoutTaking = true;
        TryStep(direction);
    }

    /// <summary>달리기를 시작한다. 실제 진행은 RunRoutine 이 맡는다.</summary>
    private void BeginRun(Vector2 direction)
    {
        Vector2 start = transform.position;
        runner.Begin(start, direction, IsInsideRoom(start), GameManager.Stats.Hp);
        EnterMode(Mode.Running);
        StartCoroutine(RunRoutine());
    }

    /// <summary>
    /// 달리기 전체를 처리한다.
    ///
    /// 한 칸이 한 프레임이다. GameManager.FastForward 로 연출용 대기(적 턴 사이의
    /// 0.1초, 공격 모션, 물리 프레임 대기)를 전부 없애면 한 턴이 한 프레임 안에서
    /// 계산되므로, 초당 60칸 정도로 빠르면서도 지나가는 길이 눈에 보인다.
    ///
    /// 걸음 하나하나는 평범한 이동과 똑같은 한 턴이라, 달리는 동안에도 적은 제
    /// 차례에 움직이고 따라잡아 때린다.
    ///
    /// 방향은 절대 바뀌지 않는다. 처음 바라본 방향으로만 직진한다.
    /// </summary>
    private IEnumerator RunRoutine()
    {
        GameManager.FastForward = true;

        while (runner.IsRunning)
        {
            // ── 걸음을 내딛기 전에 확인하는 것들 ──
            if (!InputFocus.PlayerHasFocus) { StopRun(RunStopReason.UiInterrupted); break; }
            if (GameManager.Stats.Hp < runner.HpAtLastStep) { StopRun(RunStopReason.TookDamage); break; }
            if (GameManager.Stats.IsDead) { StopRun(RunStopReason.TookDamage); break; }
            if (IsEnemyNearby()) { StopRun(RunStopReason.EnemyNearby); break; }

            // 달리는 도중에 만복도가 바닥날 수 있다. 그 뒤로는 걸음마다 체력이
            // 깎이므로 여기서 접는다.
            if (GameManager.Stats.IsStarving) { StopRun(RunStopReason.Starving); break; }
            if (!runner.CanTakeAnotherStep) { StopRun(RunStopReason.StepLimit); break; }

            steppingWithoutTaking = true;

            if (!TryStep(runner.Direction))
            {
                StopForBlockedStep();
                break;
            }

            runner.RecordStep(GameManager.Stats.Hp);

            // 죽었으면 여기서 접는다. GameManager 가 꺼지면서 적 턴 코루틴도 함께
            // 멈추므로, 아래 대기 루프에 들어가면 헛되이 기다리게 된다.
            if (GameManager.Stats.IsDead) { StopRun(RunStopReason.TookDamage); break; }

            // 적들이 이번 턴을 마칠 때까지 기다린다.
            // 빨리감기 중에는 적 턴이 동기로 끝나므로 여기서 한 프레임도 쓰지 않는다.
            int framesWaited = 0;
            while (!GameManager.instance.playersTurn && framesWaited++ < TurnStallLimit)
            {
                // 중요한 알림이 뜨면 조작 권한이 그쪽으로 넘어가고 적 턴도 멈춘다.
                // 그대로 기다리면 헛되이 TurnStallLimit 을 다 채우게 되므로 먼저 접는다.
                if (!InputFocus.PlayerHasFocus) break;
                yield return null;
            }

            if (!InputFocus.PlayerHasFocus)
            {
                StopRun(RunStopReason.UiInterrupted);
                break;
            }

            if (!GameManager.instance.playersTurn)
            {
                StopRun(RunStopReason.TurnStalled);
                break;
            }

            CheckStopConditionsAfterStep();

            // 한 칸에 한 프레임. 이게 없으면 달리기 전체가 한 프레임에 끝나 버려서
            // 어디를 어떻게 지나왔는지 화면에 남지 않는다.
            if (runner.IsRunning) yield return null;
        }

        GameManager.FastForward = false;
    }

    /// <summary>
    /// 한 걸음을 못 내디뎠을 때. 어느 쪽이든 달리기는 끝난다.
    ///
    /// 달리기는 무조건 직진이므로 옆으로 돌아가지 않는다. 다만 눈앞이 진짜 벽이었는지,
    /// 벽은 아닌데 막힌 것이었는지(물·대각선 제약 등)는 구분해서 남긴다.
    /// </summary>
    private void StopForBlockedStep()
    {
        Vector2 ahead = runner.Cell + runner.Direction;

        StopRun(TileRules.IsImpassable(tilemap, ahead)
            ? RunStopReason.HitWall
            : RunStopReason.PathObstructed);
    }

    /// <summary>한 칸 옮긴 직후에 확인하는 정지 조건들.</summary>
    private void CheckStopConditionsAfterStep()
    {
        Vector2 here = runner.Cell;

        // 아이템·함정·계단 같은 것을 밟았다.
        if (GameManager.Ground.Contains(here))
        {
            StopRun(RunStopReason.SteppedOnObject);
            return;
        }

        // 방에 들어갔거나 방에서 나왔다(문을 지났다).
        bool insideRoom = IsInsideRoom(here);
        if (insideRoom != runner.StartedInsideRoom)
        {
            StopRun(RunStopReason.RoomBoundary);
            return;
        }

        // 방 안은 사방이 뚫려 있어 갈림길을 따질 수 없다. 벽에 닿을 때까지 직진한다.
        if (insideRoom) return;

        // 대각선으로 달리는 중이면 갈림길을 따지지 않는다.
        // 폭 1칸짜리 통로는 애초에 대각선으로 지날 수 없어 다음 걸음에서 막힌다.
        bool cardinal = runner.Direction.x == 0f || runner.Direction.y == 0f;
        if (!cardinal) return;

        // 통로에서 옆으로 새는 길을 만나면 멈춰서 플레이어가 고르게 한다.
        // 길이 하나뿐인 모퉁이는 갈림길이 아니므로 그냥 직진하다 벽에 부딪혀 끝난다.
        if (TileRules.CountOpenDirections(tilemap, here, isSpecialState, -runner.Direction) >= 2)
            StopRun(RunStopReason.Junction);
    }

    private bool IsInsideRoom(Vector2 position)
    {
        foreach (Rect room in roomRects)
        {
            if (room.Contains(position)) return true;
        }
        return false;
    }

    /// <summary>바로 옆(대각선 포함)에 살아 있는 적이 있는가.</summary>
    private bool IsEnemyNearby() => IsEnemyNearby(runner.Cell);

    private bool IsEnemyNearby(Vector2 here)
    {
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || !enemy.IsCombatTarget) continue;
            if (Vector2.Distance(enemy.transform.position, here) <= EnemyAlertRadius)
                return true;
        }
        return false;
    }

    /// <summary>스프라이트와 조준 화살표를 바라보는 방향으로 돌린다.</summary>
    private void FaceDirection(Vector2 direction)
    {
        var (zRotation, spriteIndex) = GetDirectionInfo(direction);

        spriteRenderer.sprite = directionsprites[spriteIndex];

        if (mode == Mode.Aiming && ArrowTransform != null)
            ArrowTransform.transform.localEulerAngles = new Vector3(0f, 0f, zRotation);
    }

    private void Attack()
    {
        Vector3 origin = transform.position;

        // 혼란은 이동만이 아니라 공격도 흔든다. 때리려던 쪽이 아니라 엉뚱한 데를
        // 친다. 이동만 흔들면 "옆에 붙은 적을 정확히 때릴 수는 있는" 상태가 되어
        // 혼란이 별것 아닌 것이 된다.
        Vector2 direction = ScrambleIfConfused(GameManager.LookDirection);
        if (direction == Vector2.zero) return;

        // 엉뚱한 쪽을 치더라도 그쪽을 보고 친다. 보는 방향과 때리는 방향이
        // 다르면 무엇이 일어났는지 화면에서 읽을 수 없다.
        GameManager.LookDirection = direction;
        FaceDirection(direction);

        // 그쪽에 아직 남인 동료가 서 있으면 때리는 대신 말을 건다.
        //
        // 걸어 들어가는 것으로 하지 않은 이유는, 좁은 통로에서 지나가려던 걸음이
        // 매번 말 걸기가 되어 버리기 때문이다. A 는 "이 방향에 무언가 한다" 는
        // 뜻이라 적을 때리는 것과도 결이 같다.
        if (TryTalkToPartner((Vector2)origin + direction))
        {
            // 조준 중에 말을 걸었으면 조준을 푼다. 화살표가 계속 떠 있으면
            // 다음 방향 입력이 대화가 아니라 조준 전환으로 먹혀, 손이
            // "말을 걸면 조준이 끝난다" 는 걸 배우지 못한다.
            if (mode == Mode.Aiming) EnterMode(Mode.Normal);
            return;
        }

        // 연출이 끝날 때까지 조작을 잠근다.
        // 잠그지 않으면 공격 도중에 이동까지 해서 한 턴에 두 행동을 하게 된다.
        SetMode(Mode.Acting);

        StartCoroutine(AttackRoutine(direction, TileRules.CanReachWithMelee(tilemap, origin, direction)));
    }

    /// <summary>
    /// 앞으로 찌르는 모션을 보여 준 뒤, 닿는 자리에 적이 있으면 피해를 준다.
    /// 벽을 향해 휘둘렀더라도 턴은 그대로 지나간다.
    /// </summary>
    private IEnumerator AttackRoutine(Vector2 direction, bool canReach)
    {
        yield return LungeTowards(direction);

        if (canReach)
        {
            // 때리는 자리는 '지금' 위치를 기준으로 다시 구한다.
            Enemy hitEnemy = FindEnemyAt(transform.position + (Vector3)direction);
            if (hitEnemy != null)
                hitEnemy.Damaged(GameManager.AttackPower);
        }

        // 조작을 돌려주고 턴을 넘긴다.
        // 공격하고 나면 조준은 자동으로 풀린다. 한 번 쏘고 나서 커서가 계속
        // 떠 있으면 다음 이동 입력이 방향 전환으로 먹혀 답답하기 때문이다.
        SetMode(Mode.Normal);

        GameManager.instance.EndPlayerTurn();
    }

    // ── 워프 ───────────────────────────────────────────────────────────────

    /// <summary>스프라이트가 화면 밖으로 올라가는 높이(칸). 화면 세로가 10칸이다.</summary>
    private const float WarpRiseHeight = 8f;

    /// <summary>올라가는 데, 그리고 내려오는 데 걸리는 시간.</summary>
    private const float WarpRiseSeconds = 0.35f;
    private const float WarpFallSeconds = 0.25f;

    /// <summary>올라가는 동안 도는 바퀴 수.</summary>
    private const int WarpSpinTurns = 1;

    /// <summary>
    /// 워프 함정에 걸렸을 때. 한 바퀴 돌며 위로 사라졌다가 다른 방에서 내려온다.
    ///
    /// 본체(transform)를 옮기는 순간 카메라도 따라가므로, 스프라이트를 화면
    /// 밖으로 올려 둔 채 옮기면 '사라졌다가 저쪽에서 내려오는' 것으로 보인다.
    /// 움직이는 것은 자식(childTransform)이고 본체는 순간이동한다 —
    /// 본체를 천천히 옮기면 벽을 통과하는 모습이 그대로 보인다.
    /// </summary>
    public IEnumerator WarpTo(Vector3 destination)
    {
        // 달리던 중에 밟았을 수 있다. 그대로 두면 내려오자마자 마저 달린다.
        //
        // <b>멈추는 것이 먼저다.</b> StopRun 은 평소 상태로 되돌리므로, 순서가
        // 반대면 방금 건 잠금이 곧바로 풀려 연출 도중에 조작이 살아난다.
        StopRun(RunStopReason.SteppedOnObject);
        SetMode(Mode.Acting);

        yield return MoveSpriteVertically(
            WarpRiseHeight, WarpRiseSeconds, spin: true);

        // 달리기가 세어 두는 칸은 맞춰 줄 필요가 없다.
        // AutoRunner.Begin 이 다음 달리기를 시작할 때 그 자리를 다시 받는다.
        transform.position = destination;
        var mainCamera = Camera.main;
        var follow = mainCamera != null ? mainCamera.GetComponent<CameraPlayer>() : null;
        if (follow != null) follow.SnapTo(destination);

        yield return MoveSpriteVertically(
            0f, WarpFallSeconds, spin: false);

        SetMode(Mode.Normal);
    }

    /// <summary>
    /// 스프라이트를 제자리 기준 targetHeight 만큼 위로(또는 원위치로) 옮긴다.
    ///
    /// spin 이 켜져 있으면 올라가는 동안 여덟 방향을 차례로 돌아 한 바퀴 돈다.
    /// 방향 스프라이트를 그대로 쓰므로 새 그림이 필요 없다 — 이것이
    /// TrapAnimation.Spin 이 뜻하는 몸짓이다.
    /// </summary>
    private IEnumerator MoveSpriteVertically(float targetHeight, float seconds, bool spin)
    {
        Vector3 start = childTransform.localPosition;
        Vector3 end = spriteHomePosition + Vector3.up * targetHeight;

        // 빨리감기 중이면 연출을 건너뛴다. 달리기로 함정을 밟았을 때
        // 화면이 멈춰 서지 않게 하는 규칙을 여기서도 지킨다.
        if (GameManager.FastForward || seconds <= 0f)
        {
            childTransform.localPosition = end;
            yield break;
        }

        int spriteCount = directionsprites != null ? directionsprites.Length : 0;

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(t / seconds);
            childTransform.localPosition = Vector3.Lerp(start, end, progress);

            if (spin && spriteCount > 0)
            {
                int step = Mathf.FloorToInt(progress * spriteCount * WarpSpinTurns);
                spriteRenderer.sprite = directionsprites[step % spriteCount];
            }

            yield return null;
        }

        childTransform.localPosition = end;

        // 돌기를 끝냈으면 원래 바라보던 쪽으로 되돌린다.
        if (spin) FaceDirection(GameManager.LookDirection);
    }

    // ── 날려지기 ───────────────────────────────────────────────────────────

    /// <summary>한 칸을 날아가는 데 걸리는 시간. 던진 아이템보다 조금 느리다.</summary>
    private const float BlowSecondsPerCell = 0.03f;

    /// <summary>벽에 부딪혀 튀는 데 걸리는 시간과 밀려 들어가 보이는 깊이(칸).</summary>
    private const float BlowBounceSeconds = 0.1f;
    private const float BlowBounceDepth = 0.3f;

    /// <summary>
    /// 날리기 함정에 밀려 destination 까지 날아간다.
    ///
    /// 워프와 달리 본체가 실제로 <b>지나간다.</b> 날려지는 길은 이미 지나갈 수
    /// 있는 칸들이라(RangedShot 이 벽에서 멈춘다) 벽을 뚫고 가는 모습이 되지 않는다.
    ///
    /// 벽에 처박혔으면 던진 아이템이 벽에 부딪힐 때와 같은 몸짓을 보여 준다.
    /// 같은 일이 일어난 것이므로 같아 보여야 한다.
    /// </summary>
    public IEnumerator BlowTo(Vector3 destination, Vector2 direction, bool hitWall)
    {
        StopRun(RunStopReason.SteppedOnObject);   // 멈춘 뒤에 잠근다(WarpTo 주석).
        SetMode(Mode.Acting);

        Vector3 start = transform.position;
        int cells = Mathf.RoundToInt(Vector3.Distance(start, destination));

        if (!GameManager.FastForward && cells > 0)
        {
            float duration = cells * BlowSecondsPerCell;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, destination, t / duration);
                yield return null;
            }
        }

        transform.position = destination;

        if (hitWall) yield return BounceOffWall(direction.normalized);

        SetMode(Mode.Normal);
    }

    /// <summary>벽 쪽으로 한 번 밀려 들어갔다가 제자리로 돌아온다.</summary>
    private IEnumerator BounceOffWall(Vector2 direction)
    {
        if (GameManager.FastForward) yield break;

        Vector3 home = spriteHomePosition;
        Vector3 into = home + (Vector3)direction * BlowBounceDepth;

        // 들어갔다 나오는 두 토막. 같은 시간을 나눠 쓴다.
        for (float t = 0f; t < BlowBounceSeconds; t += Time.deltaTime)
        {
            float half = BlowBounceSeconds / 2f;
            childTransform.localPosition = t < half
                ? Vector3.Lerp(home, into, t / half)
                : Vector3.Lerp(into, home, (t - half) / half);
            yield return null;
        }

        childTransform.localPosition = home;
    }

    // ── 함정이 시키는 몸짓 ─────────────────────────────────────────────────

    /// <summary>한 바퀴 도는 데 걸리는 시간.</summary>
    private const float SpinSeconds = 0.45f;

    /// <summary>강제로 돌려질 때는 더 빠르게, 더 여러 바퀴.</summary>
    private const float SpinFastSeconds = 0.5f;
    private const int SpinFastTurns = 2;

    /// <summary>
    /// 함정이 시키는 몸짓을 보여 준다.
    ///
    /// 도는 것은 8방향 스프라이트를 차례로 넘기는 것이라 새 그림이 필요 없다.
    /// 피격·수면은 전용 그림이 있어야 해서 <b>아직 아무것도 하지 않는다</b> —
    /// 그림이 생기면 여기에 채우면 되고, 그때까지 함정은 문구와 효과로만 동작한다.
    /// </summary>
    public IEnumerator PlayTrapAnimation(TrapAnimation animation)
    {
        switch (animation)
        {
            case TrapAnimation.Spin:
                yield return SpinInPlace(SpinSeconds, turns: 1);
                break;

            case TrapAnimation.SpinFast:
                yield return SpinInPlace(SpinFastSeconds, SpinFastTurns);
                break;

            // TrapAnimation.Hit · Sleep 은 그림이 아직 없다.
            // TrapAnimation.None 은 원래 아무것도 하지 않는다.
        }
    }

    /// <summary>제자리에서 여덟 방향을 차례로 넘겨 turns 바퀴 돈다.</summary>
    private IEnumerator SpinInPlace(float seconds, int turns)
    {
        int spriteCount = directionsprites != null ? directionsprites.Length : 0;
        if (spriteCount == 0 || seconds <= 0f) yield break;

        // 달리다 밟았으면 연출을 건너뛴다. 달리기가 멈추지 않게 하는 규칙이다.
        if (GameManager.FastForward) yield break;

        // 제자리에서 도는 것뿐이라 조준은 그대로 둔다. 돌고 나면 겨누던 채로 돌아온다.
        bool wasAiming = mode == Mode.Aiming;
        SetMode(Mode.Acting);

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            int step = Mathf.FloorToInt(t / seconds * spriteCount * turns);
            spriteRenderer.sprite = directionsprites[step % spriteCount];
            yield return null;
        }

        // 돌기를 끝냈으면 원래 바라보던 쪽으로 되돌린다.
        SetMode(wasAiming ? Mode.Aiming : Mode.Normal);
        FaceDirection(GameManager.LookDirection);
    }

    // ── 구멍에 빠지기 ──────────────────────────────────────────────────────

    /// <summary>사라지는 데 걸리는 시간.</summary>
    private const float FallSeconds = 0.4f;

    /// <summary>
    /// 구멍에 빠졌을 때. 제자리에서 작아지며 사라진다.
    ///
    /// 아래로 내리지 않고 작아지게 하는 이유는, 내리면 지금 층의 바닥 타일
    /// <b>위를</b> 미끄러져 가는 모습이 되기 때문이다. 멀어지듯 작아져야
    /// 발밑으로 떨어지는 것으로 읽힌다.
    ///
    /// 사라진 뒤에 피해를 알린다. 느닷없이 아프면 무엇 때문인지 알 수 없다.
    /// 나중에 공중에서 허덕이는 그림이 생기면 이 사이에 끼우면 된다.
    /// </summary>
    public IEnumerator FallThroughFloor()
    {
        // 달려서 밟았을 수 있다. 그대로 두면 떨어지는 중에도 걸음이 이어진다.
        StopRun(RunStopReason.SteppedOnObject);   // 멈춘 뒤에 잠근다(WarpTo 주석).
        SetMode(Mode.Acting);

        if (!GameManager.FastForward && FallSeconds > 0f)
        {
            for (float t = 0f; t < FallSeconds; t += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(t / FallSeconds);
                childTransform.localScale = spriteHomeScale * (1f - progress);
                yield return null;
            }
        }

        childTransform.gameObject.SetActive(false);
    }

    /// <summary>
    /// 떨어지는 연출을 되돌린다.
    ///
    /// 층을 넘어가면 플레이어가 새로 만들어져서 저절로 제 모습이 되지만,
    /// <b>떨어진 피해로 죽으면 층이 넘어가지 않는다.</b> 그때 되돌리지 않으면
    /// 게임 오버 화면에 보이지 않는 캐릭터가 남는다.
    /// </summary>
    public void RestoreAfterFall()
    {
        childTransform.localScale = spriteHomeScale;
        childTransform.localPosition = spriteHomePosition;
        childTransform.gameObject.SetActive(true);
        SetMode(Mode.Normal);
    }

    private Enemy FindEnemyAt(Vector3 position)
    {
        foreach (Enemy enemy in GameManager.instance.enemies)
        {
            // 쓰러진 적이 아직 목록에 남아 있을 수 있다. 다른 순회는 전부 이렇게 거른다.
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (GridUtil.SameCell(enemy.transform.position, position))
                return enemy;
        }
        return null;
    }

    // ── 발밑에 무엇이 있는가 ────────────────────────────────────────────────
    //
    // 바닥 물건은 GroundObjectRegistry 가 '위치' 만 들고 있어서, 지금 밟고 선
    // 물건이 무엇인지는 알 수 없었다. 발밑 명령은 그 물건 자체가 있어야 하므로
    // 밟고 있는 동안 여기에 붙잡아 둔다. 물리 판정을 새로 돌리는 것보다,
    // 이미 들어오고 있는 접촉 정보를 쓰는 편이 어긋날 여지가 없다.

    /// <summary>지금 올라서 있는 아이템(또는 돈). 없으면 null.</summary>
    public GameObject ItemUnderFoot { get; private set; }

    /// <summary>지금 올라서 있는 함정. 없으면 null.</summary>
    public Trap TrapUnderFoot { get; private set; }

    /// <summary>
    /// 내 손으로 발밑에 놓은 것. 자리를 뜨기 전까지는 다시 줍지 않는다.
    ///
    /// 놓자마자 되줍는 일을 막는 장치다. 예전에는 GameManager.InteractionHandled
    /// 하나에 기대고 있었는데, 그 값은 적들이 턴을 마칠 때만 참이 된다.
    /// 적이 하나도 없는 층에서는 MoveEnemies 가 그 지점까지 가지 않고 빠져나가므로
    /// 값이 거짓으로 남고, 놓은 물건을 그 프레임에 도로 집어 들었다.
    ///
    /// 턴이 어떻게 흘렀는지와 무관하게 "이건 내가 방금 놓은 것" 이라고
    /// 못 박아 두는 편이 확실하다.
    /// </summary>
    private GameObject placedUnderFoot;

    /// <summary>발밑에 물건을 놓았다고 알린다. 자리를 뜰 때까지 줍지 않는다.</summary>
    public void KeepUnderFootWithoutTaking(GameObject item)
    {
        placedUnderFoot = item;
        ItemUnderFoot = item;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RememberUnderFoot(other);

        // 한 번 밟은 것을 매 프레임 다시 처리하지 않도록 막는다.
        if (GameManager.InteractionHandled) return;

        switch (other.tag)
        {
            case "Exit":
                GameManager.InteractionHandled = true;
                StartCoroutine(GameManager.instance.PromptDescend());
                break;

            case "Coin":
                GameManager.InteractionHandled = true;

                if (other.gameObject == placedUnderFoot) break;

                if (steppingWithoutTaking) StandOnWithoutTaking(other.gameObject);
                else PickUpCoin(other.gameObject);
                break;

            case "Item":
                GameManager.InteractionHandled = true;

                // 방금 내가 놓은 것이면 집지 않는다.
                if (other.gameObject == placedUnderFoot) break;

                if (steppingWithoutTaking || ShopGood.IsStock(other.gameObject)) StandOnWithoutTaking(other.gameObject);
                else GameManager.instance.PickUpItem(other.gameObject);
                break;

            case "Trap":
                GameManager.InteractionHandled = true;
                // 달려서 지나가도 함정은 걸린다. 달리기로 함정을 피할 수 있으면
                // 위험한 방을 그냥 뛰어서 건너는 것이 언제나 최선이 되어 버린다.
                StopRun(RunStopReason.SteppedOnObject);
                TrapUnderFoot = other.GetComponent<Trap>();
                if (TrapUnderFoot != null) TrapUnderFoot.TryTrigger();
                break;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 자리를 떴으니 "내가 놓은 것" 이라는 표시도 푼다.
        // 다시 밟으면 그때는 평소대로 주울 수 있어야 한다.
        if (placedUnderFoot == other.gameObject) placedUnderFoot = null;

        if (ItemUnderFoot == other.gameObject) ItemUnderFoot = null;
        if (TrapUnderFoot != null && TrapUnderFoot.gameObject == other.gameObject)
            TrapUnderFoot = null;
    }

    /// <summary>밟고 있는 것을 기억해 둔다. 처리 여부와 상관없이 매 프레임 갱신한다.</summary>
    private void RememberUnderFoot(Collider2D other)
    {
        switch (other.tag)
        {
            case "Item":
            case "Coin":
                // 이미 주워서 꺼진 물건은 발밑에 없는 것으로 본다.
                ItemUnderFoot = other.gameObject.activeInHierarchy ? other.gameObject : null;
                break;

            case "Trap":
                TrapUnderFoot = other.GetComponent<Trap>();
                break;
        }
    }

    /// <summary>
    /// 줍지 않고 그 위에 올라서기만 한다.
    ///
    /// 달려서 지나온 물건은 집지 않는다. 주우면 사라지는 것(돈·화살 등)을
    /// 바닥에 남겨 둔 채 지나갈 방법이 필요하기 때문이다.
    /// 걸어서 밟으면 평소대로 줍는다.
    /// </summary>
    private void StandOnWithoutTaking(GameObject item)
    {
        string itemName = ItemInstance.FullNameOf(item);
        GameManager.instance.ShowMessage(GameMessage.SteppedOnItem, itemName);
    }

    private void PickUpCoin(GameObject coin)
    {
        // 바닥에 놓여 있는 동안 이미 정해져 있던 액수를 그대로 받는다.
        // 여기서 다시 뽑으면 발밑에서 본 액수와 어긋난다.
        int amount = GoldPile.AmountOf(coin);
        GameManager.Stats.AddGold(amount);
        GameManager.ReportRecord(RecordStat.MostGold, GameManager.Stats.Gold);
        GameManager.Ground.Remove(transform.position);

        GameManager.instance.ShowMessage(GameMessage.GoldPickedUp, amount: amount);
        coin.SetActive(false);
    }

    /// <summary>
    /// 때린 쪽으로 몸을 돌린다.
    ///
    /// 조준 중이어도 돌린다. 겨누고 있던 곳보다 방금 나를 때린 쪽이 급하기 때문이다.
    /// </summary>
    private void FaceTowardAttacker(Vector2 attackerPosition)
    {
        Vector2 offset = attackerPosition - (Vector2)transform.position;

        // 여덟 방향 중 하나로 맞춘다. 근접 공격이라 한 칸 안이지만,
        // 나중에 멀리서 때리는 수단이 생겨도 방향만 남도록 잘라 둔다.
        var direction = new Vector2(
            Mathf.Clamp(Mathf.Round(offset.x), -1f, 1f),
            Mathf.Clamp(Mathf.Round(offset.y), -1f, 1f));

        if (direction == Vector2.zero) return;

        GameManager.LookDirection = direction;
        FaceDirection(direction);
    }

    /// <summary>적에게 맞았을 때.</summary>
    /// <param name="dealsCritical">
    /// 이 적이 치명타를 내는 적인가(Monster 시트의 critical). 확률은 여기서 정한다.
    /// </param>
    public void Damaged(int enemyAttack, Vector2 attackerPosition, bool dealsCritical = false)
    {
        if (Random.value >= HitChance)
        {
            // 빗나갔으면 돌아보지 않는다. 스치기만 한 쪽으로 몸이 돌아가면,
            // 정작 노리고 있던 방향을 매 턴 잃게 된다.
            GameManager.instance.ShowMessage(GameMessage.AttackMissed);
            return;
        }

        // 맞았으면 때린 쪽을 돌아본다. 곧바로 되받아치거나 던질 수 있어야 하는데,
        // 등 뒤에서 맞고도 앞을 본 채라면 방향을 다시 잡느라 한 턴을 더 쓴다.
        FaceTowardAttacker(attackerPosition);

        int randomFactor = Random.Range(-3, 4);
        // 장비가 아직 만들어지지 않았을 수 있다(GameManager.DefensePower 와 같은 대비).
        Equipment gear = GameManager.Gear;
        int damage = GameManager.Stats.CalculateIncomingDamage(
            enemyAttack, randomFactor, gear != null ? gear.DefenseBonus : 0);

        // 치명타를 내는 적은 정해져 있다(Monster 시트의 critical 이 1 인 적).
        // 모든 적이 낸다면 맞는 쪽에서는 피해 폭이 넓은 것으로만 보여서,
        // "저 녀석은 조심해야 한다" 가 성립하지 않는다.
        //
        // 치명타를 무효로 만드는 장비가 있으면 평범한 공격이 된다.
        bool critical = dealsCritical && Random.Range(0, 100) < IncomingCriticalPercent;
        if (critical && gear != null && gear.Has(EquipAbility.NegateCritical))
            critical = false;

        if (critical)
            damage = (int)System.Math.Round(damage * IncomingCriticalMultiplier);

        // 맞는 소리도 싸움이다.
        GameManager.ReportNoise(transform.position);

        GameManager.Stats.TakeDamage(damage);
        GameManager.ReportRecord(RecordStat.BiggestHitTaken, damage);

        GameManager.instance.ShowMessage(
            critical ? GameMessage.PlayerCriticallyDamaged : GameMessage.PlayerDamaged,
            amount: damage);
        GameManager.instance.GameOver();
    }
}
