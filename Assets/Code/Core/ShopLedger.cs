using System;
using System.Collections.Generic;

/// <summary>
/// 상점 한 곳의 장부 — 어느 물건이 상인 것이고, 손님이 무엇을 들고 갔으며, 얼마를 빚졌는가.
///
/// 물건은 번호(id)로만 안다. 그 번호가 무엇을 가리키는지는 부르는 쪽(ShopRoom)의
/// 일이라, 이 클래스는 Unity 없이 그대로 확인할 수 있다.
///
/// ── 써 버린 물건의 값은 남는다 ────────────────────────────────────
/// 집어 든 물건을 먹거나 방 밖으로 던지면 물건은 사라져도 <b>값은 장부에 남는다</b>
/// (<see cref="Use"/>). 돌려놓는 것으로는 지워지지 않고 치러야만 없어진다.
/// 그러지 않으면 "집어서 먹고 빈손으로 나간다" 가 공짜가 된다.
///
/// ── 적대는 되돌릴 수 없다 ─────────────────────────────────────────
/// 한 번 돌아선 상인은 그 층이 끝날 때까지 적이다. 값을 치를 길도 함께 닫힌다.
/// </summary>
public sealed class ShopLedger
{
    /// <summary>상인의 물건과 그 값.</summary>
    private readonly Dictionary<int, int> stock = new Dictionary<int, int>();

    /// <summary>손님이 값을 치르지 않고 들고 있는 것.</summary>
    private readonly HashSet<int> taken = new HashSet<int>();

    /// <summary>그중에 이미 써 버려 돌려놓을 수 없는 것.</summary>
    private readonly HashSet<int> used = new HashSet<int>();

    public bool Hostile { get; private set; }

    /// <summary>
    /// 들고 있는 것들의 번호. <b>사본이다</b> — 도는 동안 장부를 고쳐도
    /// (하나씩 돌려놓는 것처럼) 순회가 깨지지 않는다.
    /// </summary>
    public List<int> Taken => new List<int>(taken);

    /// <summary>치러야 할 값의 합. int 를 넘으면 int 의 끝에서 멈춘다.</summary>
    public int Debt => (int)Math.Min(int.MaxValue, DebtExact());

    /// <summary>
    /// 줄이지 않은 합. 치를 수 있는지는 이쪽으로 견줘야 한다 — 줄인 값으로 견주면
    /// 합이 int 를 넘는 순간 넘친 만큼이 깎여 헐값에 팔린다.
    /// </summary>
    private long DebtExact()
    {
        long sum = 0;

        foreach (int id in taken)
        {
            // 들고 있는 것은 반드시 상인의 물건이지만(Take 가 그렇게 받는다),
            // 두 목록을 함께 고치는 길이 늘면 어긋날 수 있다. 어긋났을 때 턴
            // 한가운데서 예외로 멈추는 것보다 그 물건을 0 으로 세는 편이 낫다.
            if (stock.TryGetValue(id, out int price)) sum += price;
        }

        return sum;
    }

    /// <summary>값을 치르기 전에는 내보내지 않는가. 돌아선 뒤에는 막지 않고 쫓는다.</summary>
    public bool BlocksExit => !Hostile && Debt > 0;

    /// <summary>상인의 물건으로 올린다. 이미 올라 있으면 값을 바꾸지 않는다.</summary>
    public void Register(int id, int price)
    {
        if (!stock.ContainsKey(id)) stock[id] = Math.Max(1, price);
    }

    public bool Owns(int id) => stock.ContainsKey(id);

    public int Price(int id) => stock.TryGetValue(id, out int price) ? price : 0;

    /// <summary>손님이 집어 들었다. 상인 것이 아니면 아무 일도 없다.</summary>
    public void Take(int id)
    {
        if (Owns(id)) taken.Add(id);
    }

    /// <summary>써 버렸다(먹었거나 방 밖으로 나갔다). 돌려놓아도 값이 남는다.</summary>
    public void Use(int id)
    {
        if (!Owns(id)) return;

        Take(id);
        used.Add(id);
    }

    public bool WasUsed(int id) => used.Contains(id);

    /// <summary>제자리에 돌려놓았다. 써 버린 것은 돌려놓을 수 없다.</summary>
    public void Return(int id)
    {
        if (!used.Contains(id)) taken.Remove(id);
    }

    /// <summary>
    /// 값을 치른다. 치렀으면 들고 있던 것들이 손님 것이 되고 paid 에 낸 값이 담긴다.
    /// 돈이 모자라거나 이미 돌아선 뒤면 아무것도 바뀌지 않는다.
    /// </summary>
    public bool Purchase(int gold, out int paid)
    {
        paid = Debt;

        if (Hostile || paid <= 0 || gold < DebtExact())
        {
            paid = 0;
            return false;
        }

        foreach (int id in taken)
        {
            stock.Remove(id);
            used.Remove(id);
        }

        taken.Clear();
        return true;
    }

    /// <summary>
    /// 손님이 어디 있는지 알려 준다. <b>빚을 진 채 방 밖에 있으면 도둑이다.</b>
    /// 그 순간 돌아섰으면 true.
    /// </summary>
    public bool Leave(bool inside)
    {
        if (inside || Debt == 0) return false;

        Hostile = true;
        return true;
    }

    /// <summary>
    /// 상인이 멀리서 날아온 것에 맞았다. 적이 쏜 것이면 손님 탓이 아니라 참는다.
    /// </summary>
    public void RangedHit(bool fromEnemy)
    {
        if (!fromEnemy) Hostile = true;
    }

    /// <summary>이유를 가리지 않고 돌아선다(써 버린 것을 못 돌려놓은 채 거절했을 때 등).</summary>
    public void Steal() => Hostile = true;

    /// <summary>상인이 사 주는 값. 시세의 절반이다.</summary>
    public static int BuyOffer(int marketPrice) => Math.Max(0, marketPrice) / 2;
}
