using System;
using System.Collections.Generic;

/// <summary>
/// 아이템을 적에게 던져 맞혔을 때 무슨 일이 일어나는가.
///
/// "먹으면 무슨 일이 일어나는가" 와는 별개다. 같은 아이템이라도
/// 플레이어가 먹을 때와 적이 맞을 때의 의미가 다르기 때문이다.
/// (사과는 플레이어의 배를 채우지만, 적에게는 그냥 날아온 물체다.)
/// </summary>
public enum ThrownEffect
{
    /// <summary>전투·생존과 무관한 아이템. 맞으면 힘에 비례한 피해를 준다.</summary>
    None,
    /// <summary>체력을 회복시킨다. 적에게 던지면 적을 회복시켜 버린다.</summary>
    HealHp,
    /// <summary>능력치를 올린다. 적에게 던지면 적이 강해진다.</summary>
    RaiseAttack,
}

/// <summary>
/// "이 아이템을 쓰면 / 던지면 무슨 일이 일어나는가" 를 모아 둔 표.
///
/// 예전에는 PerformAction 안에 switch(itemName) 형태로 UI 조작 코드와 뒤섞여 있었다.
/// 아이템을 하나 추가하려면 인벤토리 UI 코드를 열어야 했다(OCP 위반).
/// 이제 아이템 추가 = 아래 표에 한 줄 추가.
/// </summary>
public static class ItemEffects
{
    /// <summary>플레이어가 직접 썼을 때의 효과. 스탯을 바꾸고 띄울 문구를 돌려준다.</summary>
    private delegate GameMessage UseOnPlayer(PlayerStats stats);

    /// <summary>아이템 하나에 대해 '쓸 때' 와 '던질 때' 를 함께 적어 둔다.</summary>
    private readonly struct Entry
    {
        public readonly UseOnPlayer OnUse;
        public readonly ThrownEffect Thrown;
        public readonly int ThrownAmount;

        public Entry(UseOnPlayer onUse, ThrownEffect thrown, int thrownAmount)
        {
            OnUse = onUse;
            Thrown = thrown;
            ThrownAmount = thrownAmount;
        }
    }

    /// <summary>
    /// 아이템 번호 → 효과. item 시트의 id 열과 짝이다.
    ///
    /// 예전에는 이름("사과")이 키였다. 그러면 표에서 이름을 다듬는 순간
    /// 효과가 조용히 사라진다 — 실제로 시트의 "힘의구술" 과 여기 있던
    /// "힘의구슬" 이 한 글자 어긋나 있었다. 번호는 사람이 읽는 글이 아니라
    /// 바뀔 일이 없다.
    ///
    /// 옆의 주석은 사람이 읽으라고 붙인 것이다. 코드는 번호만 본다.
    /// </summary>
    private static readonly Dictionary<int, Entry> table =
        new Dictionary<int, Entry>
        {
            [20001] = new Entry(   // 사과
                stats =>
                {
                    // 배가 이미 부르면 대신 위장이 넓어진다.
                    if (stats.IsHungerFull)
                    {
                        stats.RaiseMaxHunger(5);
                        return GameMessage.AteFoodStomachGrew;
                    }
                    stats.Feed(30);
                    return GameMessage.AteFoodFilled;
                },
                // 만복도는 적에게 의미가 없다 → 그냥 맞은 만큼 아프다.
                ThrownEffect.None, 0),

            [30001] = new Entry(   // 회복씨앗
                stats =>
                {
                    stats.Feed(5);
                    // 체력이 이미 가득 차 있으면 최대 체력이 오른다.
                    if (stats.IsHpFull)
                    {
                        stats.RaiseMaxHp(1);
                        return GameMessage.AteHealMaxHpUp;
                    }
                    stats.Heal(10);
                    return GameMessage.AteHealRecovered;
                },
                // 적에게 던지면 적을 치료해 준다. 던질 아이템을 잘 고르라는 뜻이다.
                ThrownEffect.HealHp, 10),

            [10001] = new Entry(   // 기력회복약
                stats =>
                {
                    stats.Feed(5);
                    stats.RestoreStrength(1);
                    return GameMessage.DrankStrengthRestored;
                },
                // 기력을 되돌리는 약 → 적에게는 힘이 붙는 것으로 나타난다.
                ThrownEffect.RaiseAttack, 1),

            [40001] = new Entry(   // 힘의구슬
                stats =>
                {
                    stats.RaiseMaxStrength(1);
                    return GameMessage.UsedMaxStrengthUp;
                },
                ThrownEffect.RaiseAttack, 2),
        };

    /// <summary>
    /// 아이템을 사용한다. 표에 없는 아이템이면 아무 일도 일어나지 않고 false.
    /// </summary>
    public static bool TryUse(int itemId, PlayerStats stats, out GameMessage message)
    {
        message = default;
        if (stats == null) return false;
        if (!table.TryGetValue(itemId, out Entry entry)) return false;

        message = entry.OnUse(stats);
        return true;
    }

    /// <summary>
    /// 적에게 맞혔을 때의 효과. 표에 없는 아이템은 전투와 무관한 것으로 본다.
    /// </summary>
    public static ThrownEffect GetThrownEffect(int itemId, out int amount)
    {
        amount = 0;
        if (!table.TryGetValue(itemId, out Entry entry)) return ThrownEffect.None;

        amount = entry.ThrownAmount;
        return entry.Thrown;
    }

    public static bool IsUsable(int itemId) => table.ContainsKey(itemId);
}
