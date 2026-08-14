/// <summary>
/// 버튼 하나의 눌림 상태를 추적한다.
///
/// Unity 입력에서 떼어 놓았기 때문에 이 판정만 따로 검증할 수 있고,
/// 나중에 화면 위 가상 버튼이 붙어도 그대로 쓸 수 있다.
///
/// 판정의 기본은 시간이 아니다. "짧게 눌렀나" 를 시간으로 재는 대신
/// "누른 동안 다른 일에 쓰였나" 로 판단한다.
/// B 를 누른 채 방향키를 밀면 달리기에 쓰인 것이고(Consume),
/// 아무 일에도 쓰이지 않은 채 손을 떼면 그것이 Tapped 다.
/// 덕분에 "얼마나 빨리 떼야 하는가" 같은 감각적인 기준이 사라진다.
///
/// ── 딱 한 가지 예외: HeldSeconds ──────────────────────────────────
/// 조준 모드에서는 같은 버튼(B)이 두 가지 일을 한다.
///   짧게 누름 - 다음 적으로 조준을 옮긴다
///   길게 누름 - 조준을 끝낸다
/// 이 둘은 '무엇에 쓰였나' 로는 나눌 수 없다. 둘 다 그 버튼만 쓰는 동작이라
/// 구분할 단서가 누르고 있던 시간밖에 없다. 그래서 여기서만 시간을 잰다.
///
/// 재기만 하고 기준값은 두지 않는다. 얼마부터 '길게' 인지는 쓰는 쪽(GameInput)이
/// 정한다. 그래야 이 클래스는 여전히 "무슨 일이 있었나" 만 알려 주는 자리에 남는다.
/// </summary>
public class ButtonPress
{
    /// <summary>누른 순간(한 프레임).</summary>
    public bool Pressed { get; private set; }

    /// <summary>지금 눌려 있는가.</summary>
    public bool Held { get; private set; }

    /// <summary>
    /// 다른 동작에 쓰이지 않은 채 손을 뗀 순간(한 프레임).
    /// 누르고 있던 시간과는 무관하다.
    /// </summary>
    public bool Tapped { get; private set; }

    /// <summary>
    /// 지금 눌림이 몇 초째 이어지고 있는가. 떼면 0 으로 돌아간다.
    /// 이 값을 어디서부터 '길게' 로 볼지는 쓰는 쪽이 정한다.
    /// </summary>
    public float HeldSeconds { get; private set; }

    /// <summary>
    /// 지금 눌려 있고, <b>아직 다 쓰이지 않았는가.</b>
    ///
    /// Held 와 다른 점은 Spend 를 거쳤는지다. "누르고 있는 동안 계속 먹는" 동작
    /// (달리기)에서, 한 번 쓰고 나면 손을 뗐다 다시 눌러야 하는 경우에 쓴다.
    /// </summary>
    public bool HeldAvailable => Held && !spent;

    /// <summary>이번 눌림이 아직 아무 일에도 쓰이지 않았다.</summary>
    private bool unused;

    /// <summary>이번 눌림을 끝까지 다 썼다. 손을 떼기 전에는 다시 쓰이지 않는다.</summary>
    private bool spent;

    public void Update(bool down, bool held, bool up, float deltaTime = 0f)
    {
        Pressed = down;
        Held = held;
        Tapped = false;

        if (down)
        {
            unused = true;
            spent = false;
            HeldSeconds = 0f;
        }
        else if (held)
        {
            HeldSeconds += deltaTime;
        }

        if (up)
        {
            if (unused) Tapped = true;
            unused = false;
            spent = false;
            HeldSeconds = 0f;
        }
    }

    /// <summary>
    /// 이번 눌림을 <b>끝까지 다 썼다.</b> 손을 떼기 전에는 HeldAvailable 이 false 다.
    ///
    /// Consume 과 나눠 둔 이유가 있다. Consume 은 "이 눌림이 조준으로 새지 않게"
    /// 표시만 하는 것이라 누르고 있는 동안 달리기는 계속 먹는다. 자리 바꾸기처럼
    /// <b>한 번 하고 나면 손을 떼야 하는</b> 동작은 그것만으로는 표현할 수 없다.
    /// </summary>
    public void Spend()
    {
        Consume();
        spent = true;
    }

    /// <summary>
    /// 이번 눌림을 이미 어떤 동작에 썼다고 알린다.
    ///
    /// 이걸 해 두지 않으면, 달리기를 시작한 뒤 손을 뗄 때 Tapped 가 또 나와서
    /// 조준까지 켜져 버린다.
    /// </summary>
    public void Consume()
    {
        unused = false;
        Pressed = false;
        Tapped = false;
    }

    public void Reset()
    {
        unused = false;
        Pressed = Tapped = Held = false;
        HeldSeconds = 0f;
    }
}
