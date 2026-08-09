using UnityEngine;

/// <summary>
/// 플레이어의 모든 수치를 소유하는 클래스.
///
/// 이전에는 GameManager 안에 public static 필드 12개로 흩어져 있었다.
/// 그래서 어떤 코드든 GameManager.PlayerHp -= x 처럼 직접 대입할 수 있었고,
/// "0 아래로 내려가지 않게" / "최대치를 넘지 않게" 같은 규칙이
/// 호출하는 쪽마다 중복되거나 빠져 있었다.
///
/// 이제 값을 바꾸는 통로는 이 클래스의 메서드뿐이고(캡슐화),
/// 규칙은 전부 여기 한 곳에만 있다.
/// </summary>
public class PlayerStats
{
    // 새 게임을 시작할 때의 초기값
    public const float InitialMaxHp = 20f;
    public const int InitialAttack = 3;
    public const int InitialDefense = 1;
    public const int InitialStrength = 8;
    public const int InitialHunger = 100;
    public const int InitialExpToNextLevel = 10;

    public float MaxHp { get; private set; } = InitialMaxHp;
    public float Hp { get; private set; } = InitialMaxHp;
    public int Attack { get; private set; } = InitialAttack;
    public int Defense { get; private set; } = InitialDefense;
    public int Strength { get; private set; } = InitialStrength;
    public int MaxStrength { get; private set; } = InitialStrength;
    public int Hunger { get; private set; } = InitialHunger;
    public int MaxHunger { get; private set; } = InitialHunger;
    public int Level { get; private set; } = 1;
    public int Exp { get; private set; }
    public int ExpToNextLevel { get; private set; } = InitialExpToNextLevel;
    public int Gold { get; private set; }

    public bool IsDead => Hp <= 0;
    public bool IsHpFull => Hp >= MaxHp;
    public bool IsHungerFull => Hunger >= MaxHunger;
    public bool IsStarving => Hunger <= 0;

    /// <summary>힘이 공격력에 주는 배율. 힘이 기본치의 몇 배인가(최소 1배).</summary>
    public int StrengthMultiplier => Mathf.Max(Mathf.RoundToInt(Strength / (float)InitialStrength), 1);

    /// <summary>장비를 뺀, 맨몸의 최종 공격력.</summary>
    public int EffectiveAttack => TotalAttack(0);

    /// <summary>
    /// 무기까지 포함한 최종 공격력.
    /// 무기의 성능은 힘 배율을 함께 받는다 — 힘이 셀수록 좋은 무기가 더 잘 든다.
    /// </summary>
    public int TotalAttack(int weaponBonus) => (Attack + weaponBonus) * StrengthMultiplier;

    /// <summary>방어구까지 포함한 최종 방어력.</summary>
    public int TotalDefense(int armorBonus) => Defense + armorBonus;

    /// <summary>아이템을 던질 수 있는 거리. 힘이 0이어도 최소 1칸은 날아간다.</summary>
    public int ThrowDistance => Mathf.Max(Strength, 1);

    /// <summary>던진 물체가 몇 등분의 힘으로 꽂히는가. 클수록 던지기가 약해진다.</summary>
    private const int ThrowDamageDivisor = 2;

    /// <summary>
    /// 전투와 무관한 물건을 던져 맞혔을 때의 피해. 힘에 비례한다.
    /// 힘이 아무리 약해도 맞으면 아프므로 최소 1.
    /// </summary>
    public int ThrowDamage => Mathf.Max(Strength / ThrowDamageDivisor, 1);

    public void TakeDamage(int amount)
    {
        Hp = Mathf.Max(Hp - amount, 0f);
    }

    /// <summary>체력을 회복한다. 이미 가득 차 있었으면 false.</summary>
    public bool Heal(float amount)
    {
        if (IsHpFull) return false;
        Hp = Mathf.Min(Hp + amount, MaxHp);
        return true;
    }

    /// <summary>최대 체력만 올린다. 현재 체력은 그대로 두는 것이 기존 동작이다.</summary>
    public void RaiseMaxHp(float amount)
    {
        MaxHp += amount;
    }

    /// <summary>포만도를 회복한다. 이미 가득 차 있었으면 false.</summary>
    public bool Feed(int amount)
    {
        if (IsHungerFull) return false;
        Hunger = Mathf.Min(Hunger + amount, MaxHunger);
        return true;
    }

    public void RaiseMaxHunger(int amount)
    {
        MaxHunger += amount;
        Hunger = MaxHunger;
    }

    /// <summary>턴 경과에 따라 배가 고파진다.</summary>
    public void ConsumeHunger(int amount = 1)
    {
        Hunger = Mathf.Max(Hunger - amount, 0);
    }

    public void RestoreStrength(int amount)
    {
        // 주의: 예전 코드는 Math.Min(Playerstrength++, MaxPlayerstrength) 였는데
        // 후위 증가는 '증가하기 전 값'을 돌려주므로 힘이 실제로는 전혀 회복되지
        // 않는 버그였다. 여기서 바로잡았다.
        Strength = Mathf.Min(Strength + amount, MaxStrength);
    }

    public void RaiseMaxStrength(int amount)
    {
        MaxStrength += amount;
    }

    public void AddGold(int amount)
    {
        Gold += amount;
    }

    /// <summary>
    /// 자연 회복량. 힘이 셀수록 많이 회복한다.
    /// </summary>
    public int NaturalRecoveryAmount()
    {
        float recoveryRate = (Strength / (float)InitialStrength) * 0.05f;
        return (int)(MaxHp * recoveryRate);
    }

    /// <summary>
    /// 경험치를 더하고, 필요하면 레벨업까지 처리한다.
    /// 한 번이라도 레벨이 올랐으면 true.
    ///
    /// 예전에는 코루틴(LevelUpCheck)이었지만 대기하는 구간이
    /// WaitForSeconds(0) 하나뿐이라 사실상 동기 로직이었다.
    /// </summary>
    public bool AddExp(int amount)
    {
        Exp += amount;

        bool leveledUp = false;
        while (Exp >= ExpToNextLevel)
        {
            Exp -= ExpToNextLevel;
            ExpToNextLevel += ExpToNextLevel / 2;

            Level++;
            Attack++;
            Defense++;
            MaxHp++;
            leveledUp = true;
        }
        return leveledUp;
    }

    /// <summary>플레이어가 받는 실제 피해량을 계산한다(최소 1). 방어구 몫도 함께 뺀다.</summary>
    public int CalculateIncomingDamage(int enemyAttack, int randomFactor, int armorBonus = 0)
    {
        return Mathf.Max((enemyAttack - TotalDefense(armorBonus)) + randomFactor, 1);
    }
}
