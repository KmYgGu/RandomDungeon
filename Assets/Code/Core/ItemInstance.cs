using UnityEngine;

/// <summary>
/// 바닥에 떨어져 있거나 가방에 든 아이템 '한 개' 의 개별 정보.
///
/// 두 가지를 들고 다닌다 —
///   description : 어느 종류인가. 종류마다 하나뿐인 설명서를 가리킨다.
///   enhancement : 이 한 개만의 값. "이 검은 +3, 저 검은 +0"
///
/// ── 왜 오브젝트가 정의를 들고 다니는가 ────────────────────────────
/// 예전에는 GameObject 이름이 곧 신원이었다(`groundItem.name`). 그래서
/// 프리팹 이름 · 애셋의 itemName · ItemEffects 의 키 <b>세 곳이 글자까지
/// 같아야</b> 했고, 실제로 시트의 "힘의구술" 과 프리팹의 "힘의구슬" 이
/// 한 글자 어긋나 있었다. 어긋나도 에러는 나지 않고 그냥 안 먹는다.
///
/// 지금은 오브젝트가 자기 정의를 직접 가리킨다. 프리팹 이름은 무엇이든
/// 상관없고, 이름은 화면에 보여 주는 데만 쓴다.
/// </summary>
public class ItemInstance : MonoBehaviour
{
    /// <summary>이 아이템이 어느 종류인가. 프리팹에서 연결한다.</summary>
    public ItemDescription description;

    /// <summary>강화 수치(+n). 무기면 공격력, 방어구면 방어력에 그대로 더해진다.</summary>
    public int enhancement;

    /// <summary>
    /// 끈적끈적한가. 원작의 저주를 강하게 만든 것이다.
    ///
    ///   소모품이면  — <b>쓸 수 없다.</b> 버리거나 던지는 것은 된다.
    ///   장비면      — 차는 것은 되지만 <b>한 번 차면 벗을 수 없다.</b>
    ///                 벗을 수 없으니 그동안은 버리지도 던지지도 교환하지도 못한다.
    ///
    /// 표식(MarkBook)과 달리 <b>이 물건 하나</b>의 성질이라 여기에 둔다.
    /// 같은 사과라도 하나는 멀쩡하고 하나는 끈적할 수 있다.
    /// </summary>
    public bool sticky;

    private void Awake()
    {
        if (description == null)
        {
            // 비어 있으면 이름도 효과도 나오지 않는다. 조용히 넘어가면
            // "왜 못 먹지" 를 아이템 코드에서 찾게 되므로 여기서 알린다.
            Debug.LogWarning($"{name}: 아이템 정의(description)가 비어 있습니다.", this);
            return;
        }

        SpriteApplier.Apply(gameObject, description.sprites);
    }

    /// <summary>컴포넌트가 없거나 정의가 비어 있으면 null.</summary>
    public static ItemDescription DescriptionOf(GameObject item)
    {
        if (item == null) return null;

        ItemInstance instance = item.GetComponent<ItemInstance>();
        return instance != null ? instance.description : null;
    }

    /// <summary>
    /// 조회에 쓰는 번호. 정의가 없으면 0 이며, 어떤 표에도 걸리지 않는다.
    /// </summary>
    public static int IdOf(GameObject item)
    {
        ItemDescription description = DescriptionOf(item);
        return description != null ? description.id : 0;
    }

    /// <summary>
    /// 화면에 보여 줄 이름.
    ///
    /// 정의가 아직 연결되지 않은 프리팹이 있을 수 있어 GameObject 이름으로
    /// 물러선다. 전부 연결하고 나면 이 뒷길은 지워도 된다.
    /// </summary>
    public static string DisplayNameOf(GameObject item)
    {
        if (item == null) return string.Empty;

        ItemDescription description = DescriptionOf(item);
        return description != null
            ? description.itemName
            : item.name.Replace("(Clone)", string.Empty);
    }

    /// <summary>
    /// 강화 수치까지 붙인 이름("나무몽둥이+2"). 강화가 0 이면 이름 그대로다.
    ///
    /// <b>화면에 물건 이름을 내보이는 곳은 전부 이것을 쓴다</b> — 가방 목록,
    /// 발밑 화면, 주웠을 때의 알림. 같은 이름의 장비라도 개체마다 값이 다른데,
    /// 어느 한 곳만 이름을 그대로 쓰면 거기서는 +2 와 +0 이 구별되지 않는다.
    ///
    /// 정리(<see cref="Inventory.SortByName"/>)는 여기가 아니라 DisplayNameOf 로
    /// 센다. 강화 수치까지 넣어 견주면 나무몽둥이+2 와 나무몽둥이가 떨어져 놓인다.
    /// </summary>
    public static string FullNameOf(GameObject item)
        => DisplayNameOf(item) + ItemEnhancement.Suffix(EnhancementOf(item)) + ShopGood.PriceLabel(item);

    /// <summary>
    /// 이름 앞에 붙일 표식 글자. 적어 둔 것이 없으면 빈 문자열.
    ///
    /// 표식은 이 물건 하나가 아니라 <b>종류</b>에 붙어 있으므로(MarkBook),
    /// 번호로 물어본다. 그래서 던지든 바닥에 두든 같은 표식이 따라다닌다.
    /// </summary>
    public static string MarkPrefixOf(GameObject item)
    {
        // 장착 중 · 끈적끈적이 앞에 온다. 게임이 붙인 사실이 내가 적어 둔 메모보다 먼저다.
        // 셋이 함께 붙을 수 있다("E※●나무몽둥이+2").
        string equipped = IsEquipped(item) ? ItemMark.EquippedPrefix() : string.Empty;
        string sticky = IsSticky(item) ? ItemMark.StickyPrefix() : string.Empty;

        return equipped + sticky + GameManager.Marks.PrefixOf(IdOf(item));
    }

    /// <summary>
    /// 화면 목록에 그릴 이름. <b>표식 + 갈래 색을 입힌 이름</b>이다.
    ///
    /// 색이 붙으므로 <b>메시지에는 쓰지 않는다</b>(그쪽은 FullNameOf). 알림 창까지
    /// 색을 입히면 문구 안에서 물건 이름만 튀어 읽는 흐름이 끊긴다.
    /// </summary>
    public static string ListNameOf(GameObject item)
        => MarkPrefixOf(item) + ItemMark.SlotColored(FullNameOf(item), SlotOf(item));

    /// <summary>
    /// 이 물건이 들어가는 자리. 장비가 아니면 None.
    ///
    /// 차고 있는지와는 상관없다 — 가방 안의 여벌 무기도 Weapon 이다.
    /// </summary>
    public static EquipmentSlot SlotOf(GameObject item)
    {
        ItemDescription description = DescriptionOf(item);
        return description != null ? description.equipSlot : EquipmentSlot.None;
    }

    /// <summary>
    /// <b>지금 차고 있는</b> 자리. 차고 있지 않으면 None.
    ///
    /// 가방을 정리할 때 차고 있는 것을 앞으로 보내는 데 쓴다(ItemOrder.RankOf).
    /// </summary>
    public static EquipmentSlot EquippedSlotOf(GameObject item)
        => IsEquipped(item) ? SlotOf(item) : EquipmentSlot.None;

    /// <summary>
    /// 지금 몸에 걸치고 있는 것인가.
    ///
    /// 바닥의 물건은 걸칠 수 없으므로 언제나 아니고, 그래서 발밑 화면에는
    /// 이 표시가 나오지 않는다. 판이 시작되기 전에는 장비 자체가 없다.
    /// </summary>
    private static bool IsEquipped(GameObject item)
        => item != null && GameManager.Gear != null && GameManager.Gear.IsEquipped(item);

    /// <summary>끈적끈적한가. 컴포넌트가 없으면 아니다.</summary>
    public static bool IsSticky(GameObject item)
    {
        if (item == null) return false;

        ItemInstance instance = item.GetComponent<ItemInstance>();
        return instance != null && instance.sticky;
    }

    /// <summary>끈적끈적하게 만들거나 되돌린다.</summary>
    public static void SetSticky(GameObject item, bool value)
    {
        if (item == null) return;

        ItemInstance instance = item.GetComponent<ItemInstance>();
        if (instance != null) instance.sticky = value;
    }

    /// <summary>컴포넌트가 없으면 0.</summary>
    public static int EnhancementOf(GameObject item)
    {
        if (item == null) return 0;

        ItemInstance instance = item.GetComponent<ItemInstance>();
        return instance != null ? instance.enhancement : 0;
    }
}
