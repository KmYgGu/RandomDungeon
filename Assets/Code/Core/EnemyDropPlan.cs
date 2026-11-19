/// <summary>개체당 죽음 처리/드롭 추첨은 한 번. 몬스터의 드롭 등급은 아이템 종류 가중치와 별개다.</summary>
public sealed class EnemyDropPlan
{
    private bool resolved;

    public static float Chance(ItemDropRate rate)
    {
        switch (rate)
        {
            case ItemDropRate.High: return 0.30f;
            case ItemDropRate.Low: return 0.05f;
            default: return 0.15f;
        }
    }

    public bool TryResolveDeath(int remainingHp, ItemDropRate rate, float roll, out bool drop)
    {
        drop = false;
        if (remainingHp > 0 || resolved) return false;
        resolved = true;
        drop = roll < Chance(rate);
        return true;
    }
}
