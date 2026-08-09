/// <summary>지금 입력을 받을 권한을 가진 쪽.</summary>
public enum InputOwner
{
    /// <summary>평소. 플레이어 조작이 살아 있다.</summary>
    Player,
    /// <summary>가방 화면이 떠 있다.</summary>
    BagMenu,
    /// <summary>예/아니오 같은 선택창이 떠 있다.</summary>
    Prompt,
    /// <summary>캐릭터 상태창이 떠 있다.</summary>
    Status,
}

/// <summary>
/// 입력을 누가 받는지 정하는 유일한 기준.
///
/// 예전에는 GameManager.InputLockedByUi 라는 bool 하나였고,
/// Player.Update 안에서 그 검사를 어디에 두느냐에 따라 어떤 키는 막히고
/// 어떤 키는 새어 나갔다. 실제로 가방을 연 상태에서도 조준 모드 토글(X)이
/// 먹히는 문제가 그렇게 생겼다 — 토글 처리가 잠금 검사보다 위에 있었기 때문이다.
///
/// 이제는 "누가 주인인가" 를 한 곳에서 정하고, 주인이 아닌 쪽은
/// 입력을 아예 읽지 않는다. 검사 위치를 잘못 잡을 여지가 없다.
/// </summary>
public static class InputFocus
{
    public static InputOwner Owner { get; private set; } = InputOwner.Player;

    public static bool PlayerHasFocus => Owner == InputOwner.Player;

    /// <summary>UI 가 입력을 가져간다.</summary>
    public static void Give(InputOwner owner)
    {
        Owner = owner;
    }

    /// <summary>UI 가 닫혔다. 입력을 플레이어에게 돌려준다.</summary>
    public static void ReturnToPlayer()
    {
        Owner = InputOwner.Player;
    }

    /// <summary>층이 바뀌거나 게임을 다시 시작할 때 초기 상태로.</summary>
    public static void Reset()
    {
        Owner = InputOwner.Player;
    }
}
