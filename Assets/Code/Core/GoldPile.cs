using UnityEngine;

/// <summary>
/// 바닥에 떨어져 있는 돈 한 무더기가 얼마인지.
///
/// 예전에는 액수가 없었다. 주울 때 그 자리에서 무작위로 뽑아 소지금에 더할 뿐이라,
/// 바닥에 놓인 동안에는 얼마짜리인지가 아예 정해져 있지 않았다.
/// 발밑에서 "골드(413)" 이라고 보여 주거나 던져서 액수만큼 아프게 하려면
/// 바닥에 있는 동안에도 액수가 정해져 있어야 한다.
///
/// 액수는 처음 궁금해진 순간에 정해서 붙여 둔다(AmountOf). 그래야 던전이
/// 동전을 뿌리는 자리든, 나중에 다른 경로로 생긴 동전이든 빠짐없이 값을 갖는다.
/// 한 번 정해지면 바뀌지 않으므로, 발밑에서 본 액수와 주웠을 때 들어오는 액수가
/// 어긋나는 일은 없다.
/// </summary>
public class GoldPile : MonoBehaviour
{
    /// <summary>한 무더기의 액수 범위.</summary>
    public const int MinAmount = 100;
    public const int MaxAmount = 500;

    /// <summary>던져서 맞혔을 때, 액수 몇 마다 1 아픈가.</summary>
    public const int DamageDivisor = 10;

    /// <summary>이 무더기의 액수. 0 이면 아직 정해지지 않은 것이다.</summary>
    public int amount;

    /// <summary>이것이 돈인가.</summary>
    public static bool IsGold(GameObject candidate)
        => candidate != null && candidate.CompareTag("Coin");

    /// <summary>
    /// 액수를 알려 준다. 아직 정해지지 않았으면 여기서 정한다.
    ///
    /// 값을 읽기만 하는 것처럼 보이지만 처음 한 번은 붙이는 일도 한다.
    /// 동전을 만드는 곳이 여럿이라, 만드는 쪽마다 "액수 붙이기" 를 기억하게 하는 것보다
    /// 묻는 곳 한 군데에서 보장하는 편이 빠뜨릴 여지가 없다.
    /// </summary>
    public static int AmountOf(GameObject coin)
    {
        if (coin == null) return 0;

        GoldPile pile = coin.GetComponent<GoldPile>();
        if (pile == null) pile = coin.AddComponent<GoldPile>();

        if (pile.amount <= 0)
            pile.amount = Random.Range(MinAmount, MaxAmount);

        return pile.amount;
    }

    /// <summary>
    /// 던져서 맞혔을 때의 피해. 액수를 10 으로 나눈 몫이고 방어력을 무시한다.
    ///
    /// 방어력을 무시하는 것이 핵심이다. 돈은 무기가 아니라서 세지는 않지만,
    /// 단단한 적에게도 액수만큼은 확실히 들어간다. 큰 무더기를 쓸지 아낄지가
    /// 그래서 판단거리가 된다.
    /// </summary>
    public static int DamageOf(int amount) => amount / DamageDivisor;

    /// <summary>화면에 보여 줄 이름. 얼마짜리인지 함께 보여 준다.</summary>
    public static string DisplayName(int amount) => $"골드({amount})";
}
