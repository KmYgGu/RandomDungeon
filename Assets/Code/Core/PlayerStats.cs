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

    /// <summary>
    /// 힘이 깎인다(독침 같은 것). 실제로 깎인 양을 돌려준다.
    ///
    /// 0 밑으로는 내려가지 않는다. 힘은 공격력 배수와 던지는 거리에 쓰이는데,
    /// 음수가 되면 그 계산들이 뜻을 잃는다. 최대치는 건드리지 않으므로
    /// 기력회복약으로 되돌릴 수 있다.
    /// </summary>
    public int LoseStrength(int amount)
    {
        if (amount <= 0) return 0;

        int before = Strength;
        Strength = Mathf.Max(Strength - amount, 0);
        return before - Strength;
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
    /// 한 턴에 차오르는 양. 힘이 셀수록 많다. <b>1 보다 작을 수 있다.</b>
    ///
    /// ── 여기서 버리면 안 된다 ─────────────────────────────────────────
    /// 한때 <c>(int)</c> 로 잘라서 돌려주었다. 그런데 시작 상태(최대 체력 20,
    /// 힘 8)의 회복량이 하필 <b>정확히 1.0</b> 이라, 독침에 힘이 1만 깎여도
    /// 0.875 가 되어 통째로 버려졌다 — <b>회복이 아예 멈췄다.</b>
    ///
    /// 게다가 레벨이 올라 최대 체력이 23 이 되면 저절로 다시 1 을 넘겨서,
    /// "한동안 안 되다가 언젠가 되는" 것처럼 보였다. 에러는 나지 않는다.
    ///
    /// 실제로 몇 턴에 1 이 차는지는 RecoverNaturally 가 쌓아서 정한다.
    /// </summary>
    public float NaturalRecoveryAmount()
    {
        float recoveryRate = (Strength / (float)InitialStrength) * 0.05f;
        return MaxHp * recoveryRate;
    }

    /// <summary>
    /// 아직 체력 1 이 되지 못하고 남아 있는 회복분.
    ///
    /// 힘이 깎이면 한 턴에 1 이 안 차므로(0.875 같은 값) 남는 몫을 여기 모아 두었다가
    /// 1 이 되는 턴에 한꺼번에 올린다. 판이 바뀌면 PlayerStats 를 새로 만들므로
    /// 저절로 비워진다(GameManager.ResetRun).
    /// </summary>
    private float recoveryCarry;

    /// <summary>
    /// 자연 회복을 한 턴만큼 진행한다. 실제로 오른 체력(정수)을 돌려준다.
    ///
    /// ── 왜 체력을 소수로 두지 않는가 ──────────────────────────────────
    /// 소수를 체력에 그대로 더하면 체력이 15.875 같은 값이 된다. 그러면 그리는
    /// 쪽마다 어떻게 보여 줄지를 따로 정하게 되는데, 실제로 <b>게이지 바는 소수
    /// 그대로, 숫자는 내림</b>으로 갈려서 같은 값이 서로 다르게 보였다 — 바만
    /// 움직이고 숫자는 그대로 있는 것처럼 보인다.
    ///
    /// 체력은 정수로 두고 <b>덜 찬 몫만 안에서 쌓는다.</b> 그러면 화면 두 곳이
    /// 언제나 같은 값을 읽고, 회복 속도는 그대로 힘에 비례한다 —
    /// 힘 7 이면 두 턴에 한 번, 힘 1 이면 여덟 턴에 한 번 오른다.
    /// </summary>
    public int RecoverNaturally()
    {
        if (IsHpFull)
        {
            // 가득 찼으면 모아 둔 몫도 버린다. 들고 있으면 다음에 다치자마자
            // 지난번에 쉬어 둔 만큼이 곧바로 붙는다.
            recoveryCarry = 0f;
            return 0;
        }

        recoveryCarry += NaturalRecoveryAmount();

        int whole = Mathf.FloorToInt(recoveryCarry);
        if (whole <= 0) return 0;

        recoveryCarry -= whole;
        Heal(whole);
        return whole;
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
