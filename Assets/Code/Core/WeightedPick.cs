using System.Collections.Generic;

/// <summary>
/// 가중치를 보고 하나를 고른다.
///
/// ── 왜 따로 떼어 냈나 ─────────────────────────────────────────────
/// 예전에는 던전 생성 안에 이 계산이 들어 있었고, 프리팹 배열과 가중치 배열을
/// <b>번호로 짝지어</b> 썼다. 프리팹을 여덟 개로 늘렸는데 가중치가 다섯 개
/// 그대로였던 적이 있고, 그때 뒤의 세 개는 아무리 돌려도 나오지 않았다.
/// 반복문이 짧은 쪽에 맞춰 돌았기 때문인데, 에러는 나지 않는다.
///
/// 지금은 가중치가 아이템 정의(시트)에서 오므로 짝을 맞출 일이 없다.
/// 고르는 계산만 여기 남겨 두고 검사로 지킨다.
///
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class WeightedPick
{
    /// <summary>고를 것이 없을 때.</summary>
    public const int NoChoice = -1;

    /// <summary>가중치를 모두 더한다.</summary>
    public static float Total(IReadOnlyList<float> weights)
    {
        if (weights == null) return 0f;

        float total = 0f;
        foreach (float weight in weights)
        {
            if (weight > 0f) total += weight;
        }
        return total;
    }

    /// <summary>
    /// roll 이 가리키는 자리를 고른다. roll 은 0 이상 Total 미만이어야 한다.
    ///
    /// 가중치가 0 이하인 것은 건너뛴다 — "이 아이템은 바닥에 안 나온다" 를
    /// 0 으로 적을 수 있어야 하기 때문이다. 전부 0 이면 NoChoice.
    ///
    /// 반복문이 <b>가중치 개수만큼</b> 돈다. 짧은 쪽에 맞춰 돌면 뒤쪽이 영영
    /// 안 나오는데, 그것이 실제로 났던 버그다.
    /// </summary>
    public static int Choose(IReadOnlyList<float> weights, float roll)
    {
        if (weights == null || weights.Count == 0) return NoChoice;

        int last = NoChoice;

        for (int i = 0; i < weights.Count; i++)
        {
            float weight = weights[i];
            if (weight <= 0f) continue;

            last = i;
            if (roll < weight) return i;
            roll -= weight;
        }

        // 떠도는 소수점 오차로 마지막을 넘어설 수 있다. 그때는 마지막 것을 준다.
        return last;
    }
}
