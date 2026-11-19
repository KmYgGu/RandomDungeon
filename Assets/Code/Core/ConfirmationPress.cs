/// <summary>
/// 물음에 답하는 '결정' 입력을 가려 받는다 — <b>손을 한 번 뗀 뒤에 새로 누른 것만</b> 답이다.
///
/// 물음은 대개 결정 버튼으로 연다(말을 걸거나, 발밑의 것을 쓰거나). 그 눌림을
/// 그대로 답으로 읽으면 고를 틈도 없이 '예' 가 되어, 묻자마자 사고 묻자마자 팔린다.
/// 누른 채로 있는 동안에도, 떼는 순간에도 답이 되지 않는다.
///
/// 물음 하나에 하나씩 새로 만들어 쓴다. 지난 물음에서 뗀 것이 다음 물음까지
/// 넘어오면 안 되기 때문이다.
/// </summary>
public sealed class ConfirmationPress
{
    /// <summary>이 물음이 뜬 뒤로 손을 뗀 적이 있는가.</summary>
    private bool released;

    /// <param name="held">지금 눌려 있는가.</param>
    /// <param name="pressed">이번 프레임에 새로 눌렸는가.</param>
    public bool Accept(bool held, bool pressed)
    {
        if (!held && !pressed)
        {
            released = true;
            return false;
        }

        return released && pressed;
    }
}
