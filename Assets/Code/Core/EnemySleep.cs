/// <summary>모든 몬스터의 초기 수면과 기상 조건. 턴 판단 시에만 근접 기상 확률을 굴린다.</summary>
public sealed class EnemySleep
{
    public const float SpawnSleepChance = 0.30f;
    public const float ProximitySleeperChance = 0.50f;
    public const float NearbyWakeChance = 0.25f;
    public const int NearbySteps = 2;

    public bool IsSleeping { get; private set; }
    public bool WakesOnProximity { get; private set; }

    public void OnSpawn(float sleepRoll, float typeRoll)
    {
        IsSleeping = sleepRoll < SpawnSleepChance;
        WakesOnProximity = IsSleeping && typeRoll < ProximitySleeperChance;
    }

    /// <summary>몬스터 하우스 경보는 깊은 수면도 무조건 깨운다.</summary>
    public void Wake() => IsSleeping = false;

    public bool TryWakeNearby(int pathSteps, float roll)
    {
        if (!IsSleeping || !WakesOnProximity || pathSteps < 0 ||
            pathSteps > NearbySteps || roll >= NearbyWakeChance) return false;
        IsSleeping = false;
        return true;
    }

    public bool TryWakeOnDamage(int damage)
    {
        if (!IsSleeping || damage <= 0) return false;
        IsSleeping = false;
        return true;
    }
}
