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

    /// <summary>
    /// 이 함정이 어느 종류인가. 이름·문구·몸짓이 전부 여기서 나온다.
    ///
    /// 예전에는 GameObject 이름을 그대로 썼는데, 프리팹 이름이 trap_0 … trap_5
    /// 라서 메시지에 "trap_0이(가) 작동했다!" 가 떴다. 이름을 신원으로 삼으면
    /// 프리팹 이름을 바꾸는 순간 조용히 어긋나기도 한다.
    /// </summary>
    public TrapDescription description;

    private void Awake()
    {
        // 비어 있으면 이름도 문구도 나오지 않는다. 조용히 넘어가면
        // "왜 아무 말이 없지" 를 함정 코드에서 찾게 되므로 여기서 알린다.
        if (description == null)
        {
            Debug.LogWarning($"{name}: 함정 정의(description)가 비어 있습니다.", this);
            return;
        }

        SpriteApplier.Apply(gameObject, description.sprite);
    }

    /// <summary>
    /// 발밑에서 보여 줄 이름.
    ///
    /// 정의가 아직 연결되지 않은 프리팹이 있을 수 있어 GameObject 이름으로
    /// 물러선다. 전부 연결하고 나면 이 뒷길은 지워도 된다.
    /// </summary>
    public string DisplayName => description != null
        ? description.DisplayName
        : gameObject.name.Replace("(Clone)", string.Empty);

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
    /// 뜨는 글은 셋이다 —
    ///   1. "…이(가) 작동했다!"   이름으로 만든다
    ///   2. 시트의 문구            "어디선가 독침이 날라왔다!"
    ///   3. 효과의 결과            "힘이 감소했다."  ← 아직 없다
    ///
    /// 1과 2는 잇달아 뜨면 앞의 것이 곧바로 덮이므로 한 상자에 두 줄로 담는다.
    /// 시트에 문구가 없는 함정(워프·날리기·회전)은 1번만 뜬다.
    ///
    /// 3번은 효과를 만들 때 이 뒤에 붙인다. 밟기 · 발밑 · 던진 물건이 전부
    /// 이 한 곳을 지나가므로 효과를 여기 한 번만 쓰면 모든 경로에 함께 적용된다.
    /// </summary>
    private void Activate()
    {
        string headline = MessageCatalog.Format(GameMessage.TrapTriggered, DisplayName);
        string detail = description != null ? description.Message : string.Empty;

        GameManager.instance.ShowText(
            detail.Length > 0 ? $"{headline}\n{detail}" : headline);

        // 효과는 GameManager 가 돌린다. 이 오브젝트는 층을 넘거나 사라질 수 있어서,
        // 여기서 코루틴을 시작하면 도중에 함께 죽어 효과가 반만 적용된다.
        GameManager.instance.ApplyTrapEffect(
            description != null ? description.TrapId : string.Empty,
            description != null ? description.Animation : TrapAnimation.None);
    }
}
