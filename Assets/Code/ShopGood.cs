using UnityEngine;

/// <summary>
/// 상점에 속한 물건이라는 표시. 아이템 오브젝트에 붙는다.
///
/// 표시는 물건이 팔린 뒤에도 오브젝트에 남는다. 그래서 "지금도 상인 것인가" 는
/// 이 컴포넌트가 있는지가 아니라 <see cref="ForSale"/> 로 묻는다 — 장부가 답한다.
/// </summary>
public sealed class ShopGood : MonoBehaviour
{
    public ShopRoom Owner { get; private set; }

    /// <summary>진열돼 있던 칸. 값을 안 치르고 돌려놓을 때 이리로 돌아간다.</summary>
    public Vector3Int Home { get; private set; }

    /// <summary>지금 상인의 물건인가(아직 값을 안 치렀는가).</summary>
    public bool ForSale => Owner != null && Owner.Ledger.Owns(GetInstanceID());

    public void Bind(ShopRoom owner, Vector3Int home, int price)
    {
        Owner = owner;
        Home = home;
        owner.Register(this, price);
    }

    /// <summary>손님 것이 되었다. 상점과의 인연을 끊는다.</summary>
    public void Bought() => Owner = null;

    /// <summary>이 아이템이 지금 상인의 물건인가. 표시가 없으면 아니다.</summary>
    public static bool IsStock(GameObject item)
    {
        ShopGood good = item != null ? item.GetComponent<ShopGood>() : null;
        return good != null && good.ForSale;
    }

    /// <summary>손님이 집어 들었다고 장부에 적는다. 상인 것이 아니면 아무 일도 없다.</summary>
    public static void Taken(GameObject item)
    {
        ShopGood good = item != null ? item.GetComponent<ShopGood>() : null;
        if (good != null && good.ForSale) good.Owner.Take(good);
    }

    /// <summary>이름 뒤에 붙이는 값 표시(" (120G)"). 상인 것이 아니면 빈 문자열.</summary>
    public static string PriceLabel(GameObject item)
    {
        ShopGood good = item != null ? item.GetComponent<ShopGood>() : null;

        return good != null && good.ForSale
            ? $" ({good.Owner.Ledger.Price(good.GetInstanceID())}G)"
            : string.Empty;
    }
}
