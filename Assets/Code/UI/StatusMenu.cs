/// <summary>
/// 상태창을 열었을 때 고를 수 있는 것들. 순서가 화면에 보이는 순서다.
/// </summary>
public enum StatusMenu
{
    /// <summary>발밑의 무언가를 줍거나 쓴다.</summary>
    UnderFoot = 0,
    /// <summary>상태이상·버프를 한눈에 본다.</summary>
    Condition = 1,
    /// <summary>지금까지 만난 적과 아이템.</summary>
    Encyclopedia = 2,
    /// <summary>대화창에 지나간 메시지 이력.</summary>
    Log = 3,
    /// <summary>모험의 누적 기록.</summary>
    Records = 4,
    /// <summary>모험을 중단하고 메인 화면으로.</summary>
    Abort = 5,
}

/// <summary>
/// 메뉴 항목의 이름과 설명을 모아 둔 곳.
///
/// 이름과 설명이 한 자리에 있어야 둘이 어긋나지 않는다.
/// 설명의 줄바꿈은 일부러 손으로 넣었다 — 메시지 창이 두 줄까지만 편하게
/// 보여 주기 때문에, 어디서 끊을지를 문장 뜻에 맞춰 정해 두는 편이 낫다.
/// </summary>
public static class StatusMenuCatalog
{
    /// <summary>화면에 보이는 순서.</summary>
    public static readonly StatusMenu[] Order =
    {
        StatusMenu.UnderFoot,
        StatusMenu.Condition,
        StatusMenu.Encyclopedia,
        StatusMenu.Log,
        StatusMenu.Records,
        StatusMenu.Abort,
    };

    public static string LabelOf(StatusMenu menu)
    {
        switch (menu)
        {
            case StatusMenu.UnderFoot: return "발밑";
            case StatusMenu.Condition: return "상태";
            case StatusMenu.Encyclopedia: return "도감";
            case StatusMenu.Log: return "로그";
            case StatusMenu.Records: return "기록";
            case StatusMenu.Abort: return "중단";
            default: return string.Empty;
        }
    }

    public static string DescriptionOf(StatusMenu menu)
    {
        switch (menu)
        {
            case StatusMenu.UnderFoot:
                return "발밑의 무언가를 행동합니다.";
            case StatusMenu.Condition:
                return "현재 자신의 상태를 확인합니다.\n(상태이상, 버프, 디버프 등)";
            case StatusMenu.Encyclopedia:
                return "적이나 아이템을 확인합니다.";
            case StatusMenu.Log:
                return "대화창에 나타난 대화 이력을 확인합니다.";
            case StatusMenu.Records:
                return "자신의 다양한 활동의 흔적을 확인합니다.\n(최고 스코어, 죽은 횟수, 최대 턴 수 등)";
            case StatusMenu.Abort:
                return "현재 모험을 중지하고\n메인 화면으로 돌아갑니다.";
            default:
                return string.Empty;
        }
    }
}
