/// <summary>
/// 만복도가 줄어들 때 어느 문구를 띄울지 정한다.
///
/// 배고픔은 다섯 턴에 1씩 줄어드는 느린 변화라, 숫자를 보고 있지 않으면
/// 위험해진 줄 모른 채 지나간다. 그래서 넘어가는 순간마다 한 번씩 알린다.
///
/// ── 왜 '넘어가는 순간' 인가 ────────────────────────────────────────
/// 지금 값만 보고 "20% 이하면 알린다" 로 만들면 그 뒤로 매 턴 같은 말을 하게 되고,
/// 정작 더 위험해진 순간이 그 반복에 묻힌다. 이전 값과 지금 값을 함께 받아
/// 경계를 넘어선 그 한 번만 알린다.
///
/// 한 번에 여러 단계를 건너뛰어도(먹은 것을 잃는 효과 같은 것이 생기면)
/// 가장 위험한 단계 하나만 알린다.
/// </summary>
public static class HungerWarning
{
    /// <summary>알릴 것이 없으면 None.</summary>
    public enum Level
    {
        None,
        /// <summary>20% 이하로 내려갔다.</summary>
        Rumble,
        /// <summary>10% 이하로 내려갔다.</summary>
        Dizzy,
        /// <summary>바닥까지 두 번 남았다.</summary>
        Limit,
        /// <summary>바닥까지 한 번 남았다.</summary>
        MustEat,
        /// <summary>바닥났다.</summary>
        Starved,
    }

    /// <summary>배에서 소리가 나기 시작하는 지점(백분율).</summary>
    public const int RumblePercent = 20;

    /// <summary>눈앞이 아찔해지는 지점(백분율).</summary>
    public const int DizzyPercent = 10;

    /// <summary>
    /// 만복도가 previous 에서 current 로 바뀌었을 때 띄울 문구의 단계.
    ///
    /// 위험한 쪽부터 본다. 한 번에 20에서 0 까지 떨어졌다면 "꼬르륵" 이 아니라
    /// "아사해 버린다" 가 나와야 하기 때문이다.
    /// </summary>
    public static Level For(int previous, int current, int maxHunger)
    {
        // 늘어난 경우(먹었다)에는 아무 말도 하지 않는다.
        if (current >= previous) return Level.None;

        if (CrossedDownTo(previous, current, 0)) return Level.Starved;
        if (CrossedDownTo(previous, current, 1)) return Level.MustEat;
        if (CrossedDownTo(previous, current, 2)) return Level.Limit;

        if (CrossedDownTo(previous, current, PercentOf(maxHunger, DizzyPercent)))
            return Level.Dizzy;

        if (CrossedDownTo(previous, current, PercentOf(maxHunger, RumblePercent)))
            return Level.Rumble;

        return Level.None;
    }

    /// <summary>이번 변화로 threshold 를 처음 밟았거나 지나쳤는가.</summary>
    private static bool CrossedDownTo(int previous, int current, int threshold)
        => previous > threshold && current <= threshold;

    /// <summary>최대치의 몇 퍼센트. 최대치가 늘어나도 경고 지점이 함께 따라온다.</summary>
    private static int PercentOf(int maxHunger, int percent) => maxHunger * percent / 100;

    /// <summary>이 단계에 해당하는 문구.</summary>
    public static GameMessage MessageOf(Level level)
    {
        switch (level)
        {
            case Level.Rumble: return GameMessage.HungerRumble;
            case Level.Dizzy: return GameMessage.HungerDizzy;
            case Level.Limit: return GameMessage.HungerLimit;
            case Level.MustEat: return GameMessage.HungerMustEat;
            default: return GameMessage.HungerStarved;
        }
    }
}
