/// <summary>
/// 상점에 <b>들어서는 순간</b>을 알아챈다. 환영 인사를 한 번만 하려고 쓴다.
///
/// 머무는 동안에는 되풀이하지 않고, 나갔다 다시 들어오면 또 인사한다.
/// 돌아선 상인은 인사하지 않지만 드나든 것은 계속 따라가 둔다 — 그러지 않으면
/// 안에 있는 동안의 판정이 밀려 엉뚱한 때에 "들어왔다" 가 된다.
/// </summary>
public sealed class ShopVisit
{
    private bool wasInside;

    /// <summary>지금 안에 있는지 알려 준다. 이번에 막 들어섰고 상인이 우호적이면 true.</summary>
    public bool Observe(bool inside, bool friendly)
    {
        bool entered = inside && !wasInside;
        wasInside = inside;

        return entered && friendly;
    }
}
