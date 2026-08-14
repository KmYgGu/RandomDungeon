using System.Collections.Generic;

/// <summary>
/// 강화 수치(+n)를 정하고 이름에 붙이는 규칙.
///
/// 값 자체는 아이템 개체(<see cref="ItemInstance.enhancement"/>)가 들고 다니고,
/// 기본 성능은 종류(<see cref="ItemDescription.basePower"/>)가 갖는다.
/// 둘을 더하는 일은 <see cref="Equipment"/> 가 한다.
///
/// Unity 것을 쓰지 않으므로 Unity 없이 그대로 확인할 수 있다. 무작위를 여기서
/// 굴리지 않고 <b>굴린 결과를 받는</b> 것도 그 때문이다 — 안에서 Random 을
/// 부르면 이 규칙을 시험할 방법이 없어진다.
/// </summary>
public static class ItemEnhancement
{
    /// <summary>바닥에 놓일 때 붙을 수 있는 가장 큰 강화 수치. 0 ~ 이 값.</summary>
    public const int MaxOnSpawn = 2;

    /// <summary>
    /// 강화 수치별 가중치. <b>한 단계 올라갈 때마다 절반이 된다.</b>
    ///
    ///   +0  4  (약 57%)
    ///   +1  2  (약 29%)
    ///   +2  1  (약 14%)
    ///
    /// 셋을 같은 확률로 두면 바닥에 놓인 장비의 3분의 2가 강화품이 되어,
    /// +2 를 주워도 특별할 것이 없어진다. 강화는 드물어야 값이 있다.
    ///
    /// 표가 아니라 규칙(절반씩)으로 둔 것은 <see cref="MaxOnSpawn"/> 을 올렸을 때
    /// 저절로 이어지게 하기 위해서다. 표로 적어 두면 최대치만 올리고 표를 안
    /// 고쳤을 때 새 수치가 영영 안 나오는데, 에러는 나지 않는다.
    /// </summary>
    public static List<float> SpawnWeights()
    {
        var weights = new List<float>(MaxOnSpawn + 1);
        for (int level = 0; level <= MaxOnSpawn; level++)
            weights.Add(1 << (MaxOnSpawn - level));

        return weights;
    }

    /// <summary>
    /// 바닥에 새로 놓는 물건에 붙일 강화 수치.
    ///
    /// <b>장비가 아니면 언제나 0 이다.</b> 사과에 +2 가 붙어도 더해질 성능이
    /// 없어 아무 일도 일어나지 않는데, 이름에는 "사과+2" 가 뜬다.
    /// 여기서 막지 않으면 그것을 아이템 표시 쪽에서 다시 걸러야 한다.
    /// </summary>
    /// <param name="definition">시트에서 찾은 줄. 표에 없으면 null.</param>
    /// <param name="roll">0 이상 1 이하의 무작위 수.</param>
    public static int OnSpawn(ItemDefinition definition, float roll)
    {
        if (definition == null || !definition.IsEquipment) return 0;

        List<float> weights = SpawnWeights();

        // 가중치로 고르는 방법은 아이템 종류를 뽑을 때와 같은 것을 쓴다.
        int picked = WeightedPick.Choose(weights, roll * WeightedPick.Total(weights));

        return picked >= 0 ? picked : 0;
    }

    /// <summary>
    /// 이름 뒤에 붙일 "+n". 0 이면 빈 문자열이다.
    ///
    /// 0 까지 "+0" 으로 붙이면 강화되지 않은 것이 대부분인 초반에 이름이
    /// 전부 지저분해지고, 정작 눈에 띄어야 할 +1 · +2 가 묻힌다.
    /// </summary>
    public static string Suffix(int enhancement)
        => enhancement > 0 ? "+" + enhancement : string.Empty;
}
