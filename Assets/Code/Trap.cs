using UnityEngine;

/// <summary>
/// 바닥에 놓인 함정 하나.
///
/// 지금까지 함정은 프리팹만 배치되고 아무 일도 하지 않았다(스크립트가 붙어 있지
/// 않았다). 발밑 명령의 "다시 밟아 작동을 시도한다" 가 성립하려면 먼저 평소에
/// 밟는 것부터 있어야 해서, 밟기와 확률 판정을 여기에 만들었다.
///
/// ── 아직 정해지지 않은 것 ─────────────────────────────────────────
/// 함정이 작동했을 때 무슨 일이 일어나는지는 종류마다 다르고, 아직 정해진 바가
/// 없다. 그래서 지금은 이름만 알리고 끝난다. Activate() 안을 채우면 되며,
/// 밟기 · 발밑 · 던진 물건에 맞았을 때가 전부 이 한 곳을 지나가므로
/// 효과를 여기 한 번만 쓰면 모든 경로에 함께 적용된다.
/// </summary>
public class Trap : MonoBehaviour
{
    /// <summary>
    /// 밟았을 때 실제로 작동할 확률.
    ///
    /// 늘 작동하지 않는 것이 중요하다. 함정을 밟는 편이 이득인 상황이 있고
    /// (발밑 명령이 그래서 있다), 반대로 밟고도 무사한 운도 있어야
    /// 밟을지 말지가 판단거리가 된다.
    /// </summary>
    [Range(0f, 1f)]
    public float triggerChance = 0.5f;

    /// <summary>발밑에서 보여 줄 이름.</summary>
    public string DisplayName => gameObject.name.Replace("(Clone)", string.Empty);

    /// <summary>
    /// 작동을 시도한다. 실제로 작동했으면 true.
    ///
    /// 밟아서 지나갈 때든, 발밑 명령으로 일부러 다시 밟든 같은 확률을 쓴다.
    /// 일부러 밟는 쪽이 더 잘 걸리거나 덜 걸리면, 어느 쪽이 이득인지를
    /// 플레이어가 계산할 수 없게 된다.
    /// </summary>
    public bool TryTrigger()
    {
        if (Random.value > triggerChance)
        {
            GameManager.instance.ShowMessage(GameMessage.TrapDidNothing, DisplayName);
            return false;
        }

        Activate();
        return true;
    }

    /// <summary>
    /// 함정이 실제로 하는 일.
    ///
    /// 종류별 효과가 정해지면 여기서 갈래를 나눈다. 지금은 작동했다는 사실만 알린다.
    /// </summary>
    private void Activate()
    {
        GameManager.instance.ShowMessage(GameMessage.TrapTriggered, DisplayName);
    }
}
