using UnityEngine;

/// <summary>
/// 게임이 읽는 유일한 입력 창구. 어떤 키를 눌렀는지가 아니라
/// "무엇을 하려는지"를 알려 준다.
///
/// 모바일이 목표라서 조작은 방향 + 버튼 두 개로 끝나야 한다.
/// 지금은 키보드가 이 값들을 채우지만, 나중에 화면 위 가상 버튼이 붙어도
/// 이 클래스만 바뀌고 Player 나 UI 코드는 그대로다.
///
///   A             : 공격 / 결정
///   B 살짝        : 조준 모드 켜기·끄기
///   B 누른 채 방향 : 달리기
///   A + B         : 제자리 대기
///   B (창 열림)   : 취소
///
/// 시간 임계값이 하나도 없다는 점이 중요하다. 조준과 달리기는 '얼마나 오래
/// 눌렀나' 가 아니라 '누른 동안 방향을 밀었나' 로 갈린다. 대기는 '다른 버튼이
/// 이미 눌려 있나' 로 갈린다. 그래서 손버릇을 타지 않는다.
/// </summary>
public static class GameInput
{
    private static readonly ButtonPress action = new ButtonPress();
    private static readonly ButtonPress assist = new ButtonPress();

    /// <summary>A 를 누른 순간.</summary>
    public static bool ActionPressed { get { EnsurePolled(); return action.Pressed; } }

    /// <summary>A 가 눌려 있는가.</summary>
    public static bool ActionHeld { get { EnsurePolled(); return action.Held; } }

    /// <summary>B 를 누른 순간. 취소처럼 즉시 반응해야 하는 곳에 쓴다.</summary>
    public static bool AssistPressed { get { EnsurePolled(); return assist.Pressed; } }

    /// <summary>B 가 눌려 있는가. 달리기 판정에 쓴다.</summary>
    public static bool AssistHeld { get { EnsurePolled(); return assist.Held; } }

    /// <summary>B 를 아무 일에도 쓰지 않은 채 뗀 순간. 조준 토글이다.</summary>
    public static bool AssistTapped { get { EnsurePolled(); return assist.Tapped; } }

    /// <summary>
    /// 이 시간을 넘게 누르고 있으면 '길게 누름' 으로 본다.
    ///
    /// 이 프로젝트에서 시간으로 가르는 판정은 여기 하나뿐이다(ButtonPress 주석 참고).
    /// 조준 모드에서 B 가 짧게는 '다음 적', 길게는 '조준 끝' 이라 시간 말고는
    /// 나눌 단서가 없기 때문이다. 짧게 누름이 실수로 길게로 넘어가지 않도록
    /// 넉넉하게 잡았다.
    /// </summary>
    public const float LongPressSeconds = 0.35f;

    /// <summary>B 를 기준 시간 넘게 누르고 있는가. 누르고 있는 동안 계속 참이다.</summary>
    public static bool AssistHeldLong
    {
        get { EnsurePolled(); return assist.HeldSeconds >= LongPressSeconds; }
    }

    /// <summary>
    /// 두 버튼을 함께 눌렀는가(제자리 대기).
    ///
    /// 사람이 두 버튼을 정확히 같은 순간에 누르는 것은 불가능하므로,
    /// "한쪽을 누를 때 다른 쪽이 이미 눌려 있는가" 로 본다.
    /// 덕분에 A 단독 공격에 지연을 주지 않으면서도 동시 입력을 잡아낼 수 있다.
    /// (같은 프레임에 둘 다 눌린 경우도 Held 가 함께 참이라 잡힌다.)
    /// </summary>
    public static bool WaitRequested
    {
        get
        {
            EnsurePolled();
            return (action.Pressed && assist.Held) || (assist.Pressed && action.Held);
        }
    }

    /// <summary>메뉴 커서 이동. 위 -1, 아래 +1, 없으면 0.</summary>
    public static int MenuStep { get { EnsurePolled(); return menuStep; } }

    /// <summary>
    /// 창을 좌우로 넘기기. 왼쪽 -1, 오른쪽 +1, 없으면 0.
    /// 가방과 발밑을 오갈 때 쓴다.
    /// </summary>
    public static int MenuSwitch { get { EnsurePolled(); return menuSwitch; } }

    // ── PC 테스트용. 모바일에는 없는 입력이다 ──────────────────────────────

    /// <summary>조준 단축키. B 살짝 누르기와 같다.</summary>
    public static bool AimShortcutPressed { get { EnsurePolled(); return aimShortcut; } }

    /// <summary>가방 열기. 최종적으로는 UI 를 눌러서 연다.</summary>
    public static bool BagShortcutPressed { get { EnsurePolled(); return bagShortcut; } }

    /// <summary>캐릭터 상태창 열기/닫기.</summary>
    public static bool StatusPressed { get { EnsurePolled(); return status; } }

    /// <summary>개발용 층 이동. 나중에 뺀다.</summary>
    public static bool SkipFloorPressed { get { EnsurePolled(); return skipFloor; } }

    // ── 내부 ───────────────────────────────────────────────────────────────

    private static int polledFrame = -1;
    private static int menuStep;
    private static int previousVertical;
    private static int menuSwitch;
    private static int previousHorizontal;
    private static bool aimShortcut;
    private static bool bagShortcut;
    private static bool status;
    private static bool skipFloor;

    /// <summary>이번 A 입력을 이미 처리했다고 알린다.</summary>
    public static void ConsumeAction()
    {
        EnsurePolled();
        action.Consume();
    }

    /// <summary>이번 B 입력을 이미 처리했다고 알린다.</summary>
    public static void ConsumeAssist()
    {
        EnsurePolled();
        assist.Consume();
    }

    /// <summary>층을 넘어가거나 다시 시작할 때 눌린 상태를 지운다.</summary>
    public static void Reset()
    {
        action.Reset();
        assist.Reset();
        previousVertical = 0;
        previousHorizontal = 0;
        polledFrame = -1;
    }

    /// <summary>
    /// 프레임마다 한 번만 읽는다.
    ///
    /// 누가 먼저 읽든 같은 값을 보게 하려고 '처음 읽을 때' 갱신한다.
    /// 특정 컴포넌트의 Update 순서에 의존하면, 실행 순서가 바뀔 때
    /// 한 프레임씩 밀리는 문제가 생긴다(카메라에서 겪었던 것과 같은 종류).
    /// </summary>
    private static void EnsurePolled()
    {
        if (polledFrame == Time.frameCount) return;
        polledFrame = Time.frameCount;
        Poll();
    }

    private static void Poll()
    {
        action.Update(
            Input.GetKeyDown(InputKeys.Action),
            Input.GetKey(InputKeys.Action),
            Input.GetKeyUp(InputKeys.Action),
            Time.deltaTime);

        assist.Update(
            Input.GetKeyDown(InputKeys.Assist),
            Input.GetKey(InputKeys.Assist),
            Input.GetKeyUp(InputKeys.Assist),
            Time.deltaTime);

        aimShortcut = Input.GetKeyDown(InputKeys.AimShortcut);
        bagShortcut = Input.GetKeyDown(InputKeys.BagShortcut);
        status = Input.GetKeyDown(InputKeys.Status);
        skipFloor = Input.GetKeyDown(InputKeys.SkipToNextFloor);

        // 메뉴 커서는 누른 순간에만 한 칸 움직인다.
        int vertical = (int)Input.GetAxisRaw("Vertical");
        menuStep = (vertical != 0 && vertical != previousVertical) ? -vertical : 0;
        previousVertical = vertical;

        // 좌우도 같은 방식으로 읽는다. 누르고 있는 동안 창이 계속 넘어가면 안 된다.
        int horizontal = (int)Input.GetAxisRaw("Horizontal");
        menuSwitch = (horizontal != 0 && horizontal != previousHorizontal) ? horizontal : 0;
        previousHorizontal = horizontal;
    }
}
