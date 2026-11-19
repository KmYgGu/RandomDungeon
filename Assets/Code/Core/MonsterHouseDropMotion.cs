using UnityEngine;

/// <summary>높은 곳에서 가속 낙하하고 착지 후 짧게 반동을 준다. 좌표/크기는 표시 전용.</summary>
public static class MonsterHouseDropMotion
{
    public const float Height = 6f;
    public const float FallSeconds = 0.14f;
    public const float LandingSeconds = 0.08f;

    public static float HeightAt(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / FallSeconds);
        return Height * (1f - t * t);
    }

    public static Vector2 LandingScale(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / LandingSeconds);
        return Vector2.Lerp(new Vector2(1.18f, 0.78f), Vector2.one, 1f - (1f - t) * (1f - t));
    }

    public static float BounceAt(float elapsed)
    {
        float t = Mathf.Clamp01(elapsed / LandingSeconds);
        return t >= 1f ? 0f : 0.10f * Mathf.Sin(t * Mathf.PI);
    }
}
