using UnityEngine;

/// <summary>
/// 아이템 한 종류의 정의 애셋. 사람이 채우는 것은 <b>번호와 그림뿐</b>이다.
///
/// 이름·설명·가격·갈래·장비 자리·성능은 여기에 적지 않고 엑셀(item 시트)에서
/// 읽는다. 애셋에 사본을 두면 시트를 고쳤을 때 조용히 어긋난다 —
/// TrapDescription · MonsterDescription 과 같은 이유다.
///
/// 그래서 아래 값들은 필드가 아니라 프로퍼티다. 인스펙터에 보이지 않으므로
/// ItemDescriptionEditor 가 읽기 전용으로 그려 준다.
///
/// ── 이름은 더 이상 신원이 아니다 ──────────────────────────────────
/// 예전에는 itemName 이 조회 키였고, 그것이 프리팹 이름과도 같아야 했다.
/// 실제로 시트에는 "힘의구술", 프리팹에는 "힘의구슬" 로 한 글자 어긋나 있었다.
/// 지금 조회는 전부 번호로 하고, 이름은 화면에 보여 주는 데만 쓴다.
/// 프리팹 이름은 무엇이든 상관없다.
/// </summary>
[CreateAssetMenu(fileName = "NewItemDescription", menuName = "Item/Description")]
public class ItemDescription : ScriptableObject
{
    /// <summary>item 시트의 id 열. 이 번호로 시트에서 나머지를 찾아온다.</summary>
    public int id;

    /// <summary>가방·상태창·바닥에 쓰이는 그림.</summary>
    public Sprite sprites;

    /// <summary>시트에서 찾은 줄. 번호가 표에 없으면 null.</summary>
    public ItemDefinition Definition => ItemCatalog.Find(id);

    /// <summary>
    /// 화면에 보이는 이름.
    ///
    /// 표에서 못 찾으면 번호를 그대로 보여 준다. 빈칸으로 두면 "을(를) 주웠다."
    /// 처럼 문장만 남아 무엇이 잘못됐는지 알 수 없다.
    /// </summary>
    public string itemName => Definition?.Name ?? $"?{id}";

    /// <summary>'확인한다' 로 보는 설명.</summary>
    public string description => Definition?.Description ?? string.Empty;

    public int SellPrice => Definition?.SellPrice ?? 0;

    public ItemKind itemType => Definition?.Kind ?? ItemKind.Expendables;

    /// <summary>어느 슬롯에 끼우는가. None 이면 장비가 아니다.</summary>
    public EquipmentSlot equipSlot => Definition?.Slot ?? EquipmentSlot.None;

    /// <summary>
    /// 강화하지 않은 상태의 기본 성능.
    /// 무기면 공격력에, 방어구면 방어력에 더해진다.
    /// </summary>
    public int basePower => Definition?.BasePower ?? 0;

    /// <summary>장비가 주는 특수 능력.</summary>
    public EquipAbility ability => Definition?.Ability ?? EquipAbility.None;

    /// <summary>그 능력이 쓰는 숫자.</summary>
    public int abilityValue => Definition?.AbilityValue ?? 0;

    /// <summary>합성할 때 베이스로만 쓸 수 있는가.</summary>
    public bool baseOnly => Definition?.BaseOnly ?? false;

    /// <summary>실제로 장비로 쓸 수 있는가.</summary>
    public bool IsEquipment => equipSlot != EquipmentSlot.None;
}
