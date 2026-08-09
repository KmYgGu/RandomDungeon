using UnityEngine;

/// <summary>
/// 모바일 버튼을 키보드 어느 키에 대응시킬지 적어 둔 표.
///
/// 게임 코드는 이 표를 직접 읽지 않는다. GameInput 만이 여기를 본다.
/// 나중에 화면 위 가상 버튼이 붙으면 이 파일은 'PC 테스트용 대응표' 로만 남는다.
///
/// 최종 조작 체계는 방향 + 버튼 두 개다.
///   Action(A) : 공격 / 결정
///   Assist(B) : 방향과 함께 → 달리기 · 짧게 → 대기 · 길게 → 조준 · 창에서는 취소
/// </summary>
public static class InputKeys
{
    /// <summary>모바일 A 버튼.</summary>
    public const KeyCode Action = KeyCode.A;

    /// <summary>모바일 B 버튼.</summary>
    public const KeyCode Assist = KeyCode.S;

    /// <summary>
    /// PC 전용 조준 단축키. 모바일에는 없다(B 를 길게 눌러 조준한다).
    /// 테스트할 때 길게 누르기를 기다리지 않아도 되도록 남겨 둔다.
    /// </summary>
    public const KeyCode AimShortcut = KeyCode.X;

    /// <summary>
    /// 캐릭터 상태창 열기.
    /// 모바일에서는 캐릭터 일러스트 UI 를 눌러 여는 자리다.
    /// </summary>
    public const KeyCode Status = KeyCode.Space;

    /// <summary>
    /// PC 임시: 가방 열기.
    ///
    /// 최종적으로는 체력/인벤토리 UI 를 눌러서 여는 것이라 버튼을 차지하지 않는다.
    /// 그 UI 가 생기기 전까지 테스트할 수 있도록 임시로 둔 키다.
    /// </summary>
    public const KeyCode BagShortcut = KeyCode.I;

    /// <summary>
    /// 개발용: 계단을 찾지 않고 즉시 다음 층으로.
    /// 씬을 다시 불러오면 GameManager 가 층을 하나 올리고 새 던전을 만든다.
    /// </summary>
    public const KeyCode SkipToNextFloor = KeyCode.C;

    /// <summary>
    /// PC 전용: 누르고 있으면 대각선 입력만 받는다.
    /// 모바일 가상 스틱은 여덟 방향을 그대로 주므로 필요 없다.
    /// </summary>
    public const KeyCode DiagonalLock = KeyCode.LeftShift;
}
