using System.Text;

/// <summary>
/// 모험 하나가 끝났을 때의 점수.
///
/// ── 끝나기 전에는 알 수 없다 ──────────────────────────────────────
/// 점수는 <b>죽거나 던전을 클리어한 순간에만</b> 매겨진다. 도중에 들여다볼 수
/// 있으면 "지금 돌아가면 몇 점" 을 계산하며 노는 게임이 되어, 더 깊이 내려갈지
/// 말지가 도박이 아니라 산수가 된다. 그래서 이 값을 만드는 곳은 모험이 끝나는
/// 자리 하나뿐이고, 상태창의 '기록' 은 <b>지난 모험</b>의 점수만 보여 준다.
///
/// ── 셈법 ──────────────────────────────────────────────────────────
///   도달 층수  500 × (도달 층수 - 1)     1층에서 죽으면 0점이다
///   소지 금액  가진 돈 그대로            999,999 에서 끊는다
///   매각 가격  가방 속 모든 것의 판매가   장비도 가방 안에 있다(Equipment 주석)
///   클리어     클리어했을 때만 1,000,000
///
/// 층수에 가장 큰 몫을 주지 않은 것은 원작 그대로다. 깊이 내려가는 것만으로는
/// 점수가 크게 오르지 않고, 무엇을 <b>들고 돌아왔는가</b>가 함께 셈해진다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class RunScore
{
    /// <summary>층 하나를 내려갈 때마다 붙는 점수.</summary>
    public const int PerStage = 500;

    /// <summary>소지 금액이 점수에 반영되는 한도.</summary>
    public const int GoldCap = 999999;

    /// <summary>무사히 클리어했을 때 붙는 고정 점수.</summary>
    public const int ClearBonus = 1000000;

    /// <summary>도달 층수 몫. 1층에서 끝났으면 0.</summary>
    public int StagePoints { get; private set; }

    /// <summary>소지 금액 몫. 한도에서 끊긴 뒤의 값이다.</summary>
    public int GoldPoints { get; private set; }

    /// <summary>가방 속 물건들의 판매가 총합.</summary>
    public int ItemPoints { get; private set; }

    /// <summary>클리어 보너스. 클리어하지 못했으면 0.</summary>
    public int ClearPoints { get; private set; }

    /// <summary>어디까지 내려갔는가. 화면에 함께 보여 준다.</summary>
    public int Stage { get; private set; }

    /// <summary>무사히 돌아왔는가.</summary>
    public bool Cleared { get; private set; }

    public int Total => StagePoints + GoldPoints + ItemPoints + ClearPoints;

    /// <summary>
    /// 점수를 매긴다.
    /// </summary>
    /// <param name="stage">도달한 층. 1층에서 끝났으면 1.</param>
    /// <param name="gold">끝났을 때 가지고 있던 돈.</param>
    /// <param name="itemSellTotal">가방 속 모든 것의 판매가 총합.</param>
    /// <param name="cleared">무사히 클리어했는가.</param>
    public static RunScore Of(int stage, int gold, int itemSellTotal, bool cleared)
    {
        // 음수가 들어오면 점수를 <b>깎는다</b>. 그런 일이 생길 자리는 없지만,
        // 생겼을 때 조용히 총점이 줄어드는 것보다 0 으로 두는 편이 낫다.
        int safeStage = stage > 0 ? stage : 1;

        return new RunScore
        {
            Stage = safeStage,
            Cleared = cleared,
            StagePoints = PerStage * (safeStage - 1),
            GoldPoints = Clamp(gold, GoldCap),
            ItemPoints = Clamp(itemSellTotal, int.MaxValue),
            ClearPoints = cleared ? ClearBonus : 0,
        };
    }

    private static int Clamp(int value, int max)
    {
        if (value <= 0) return 0;
        return value > max ? max : value;
    }

    /// <summary>
    /// 끝 화면에 띄울 내역. 무엇으로 몇 점을 받았는지 한 줄씩 보여 준다.
    ///
    /// 총점만 보여 주면 다음 판에 무엇을 달리해야 할지 알 수 없다. 층수를 더
    /// 내려갈지, 돈을 더 챙길지, 비싼 것을 들고 돌아올지가 여기서 갈린다.
    /// </summary>
    public string BuildBreakdown()
    {
        var sb = new StringBuilder();

        sb.Append(Cleared ? "던전 클리어!" : $"{Stage}층에서 쓰러졌다").Append('\n').Append('\n');

        sb.Append("도달 층수 : ").Append(StagePoints).Append('\n');
        sb.Append("소지 금액 : ").Append(GoldPoints).Append('\n');
        sb.Append("소지품 매각 : ").Append(ItemPoints).Append('\n');

        // 클리어하지 못한 판에까지 '클리어 보너스 : 0' 을 적어 두면, 못 받은 것을
        // 매번 확인시키는 꼴이 된다. 받았을 때만 보여 준다.
        if (ClearPoints > 0) sb.Append("클리어 보너스 : ").Append(ClearPoints).Append('\n');

        sb.Append('\n').Append("총점 : ").Append(Total);

        return sb.ToString();
    }
}
