using UnityEngine;

/// <summary>
/// 지금 몸에 걸치고 있는 것들. 슬롯마다 하나씩만 낄 수 있다.
///
/// 장비한 물건은 가방에서 빼지 않는다. 가방에 그대로 두고 "이것을 끼고 있다" 는
/// 표시만 한다. 시리즈의 방식이기도 하고, 장비를 벗을 곳을 따로 만들 필요가 없다.
///
/// 강화 수치는 아이템 개체(ItemInstance)가 갖고 있고, 기본 성능은 종류
/// (ItemDescription)가 갖는다. 최종 성능은 그 둘의 합이다.
/// </summary>
public class Equipment
{
    // 아이템의 성능은 오브젝트가 직접 들고 다니는 정의(ItemInstance.description)
    // 에서 읽는다. 예전에는 이름으로 찾아 주는 색인(ItemCatalog)을 받아 두었는데,
    // 오브젝트가 자기 정의를 가리키게 되면서 그 다리가 필요 없어졌다.
    private GameObject weapon;
    private GameObject armor;
    private GameObject accessory;

    public GameObject Weapon => weapon;
    public GameObject Armor => armor;
    public GameObject Accessory => accessory;

    /// <summary>무기가 더해 주는 공격력(기본 성능 + 강화).</summary>
    public int AttackBonus => PowerOf(weapon);

    /// <summary>방어구가 더해 주는 방어력(기본 성능 + 강화).</summary>
    public int DefenseBonus => PowerOf(armor);

    /// <summary>
    /// 장신구의 수치. 아직 전투에 반영되는 곳은 없고 상태창 표시용이다.
    /// 무슨 효과를 줄지 정해지면 여기서부터 이어 붙이면 된다.
    /// </summary>
    public int AccessoryPower => PowerOf(accessory);

    /// <summary>
    /// 지금 차고 있는 것들이 가진 이 능력의 합.
    ///
    /// 세 자리를 모두 더한다. 같은 능력을 무기와 장신구가 함께 가지면 둘 다 먹는다 —
    /// 갖춰 입는 보람이 있어야 하고, "둘 중 하나만 먹는다" 는 규칙은 어느 쪽이
    /// 먹히는지 화면에서 알 수 없어 설명하기도 어렵다.
    /// </summary>
    public int AbilityValue(EquipAbility ability)
        => ValueOf(weapon, ability) + ValueOf(armor, ability) + ValueOf(accessory, ability);

    /// <summary>그 능력을 가진 장비를 하나라도 차고 있는가. 숫자를 쓰지 않는 능력에 쓴다.</summary>
    public bool Has(EquipAbility ability)
        => HasOn(weapon, ability) || HasOn(armor, ability) || HasOn(accessory, ability);

    private static bool HasOn(GameObject item, EquipAbility ability)
    {
        if (item == null || ability == EquipAbility.None) return false;

        ItemDescription description = ItemInstance.DescriptionOf(item);
        return description != null && description.ability == ability;
    }

    private static int ValueOf(GameObject item, EquipAbility ability)
        => HasOn(item, ability) ? ItemInstance.DescriptionOf(item).abilityValue : 0;

    public GameObject Get(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return weapon;
            case EquipmentSlot.Armor: return armor;
            case EquipmentSlot.Accessory: return accessory;
            default: return null;
        }
    }

    /// <summary>이 아이템이 들어갈 슬롯. 장비가 아니면 None.</summary>
    public EquipmentSlot SlotOf(GameObject item)
    {
        if (item == null) return EquipmentSlot.None;

        ItemDescription description = ItemInstance.DescriptionOf(item);
        return description != null ? description.equipSlot : EquipmentSlot.None;
    }

    public bool IsEquipped(GameObject item)
    {
        return item != null && (item == weapon || item == armor || item == accessory);
    }

    /// <summary>
    /// 장비한다. 같은 슬롯에 이미 뭔가 있으면 그것과 바꿔 낀다.
    /// 장비가 아니면 아무 일도 하지 않고 false.
    /// </summary>
    public bool TryEquip(GameObject item)
    {
        EquipmentSlot slot = SlotOf(item);
        if (slot == EquipmentSlot.None) return false;

        Set(slot, item);
        return true;
    }

    /// <summary>벗는다.</summary>
    public void Unequip(EquipmentSlot slot) => Set(slot, null);

    /// <summary>
    /// 이 아이템을 장비 중이었다면 벗긴다.
    /// 가방에서 빠져나갈 때(먹기·놓기·던지기) 반드시 불러야 한다.
    /// 그러지 않으면 손에 없는 물건의 공격력이 계속 붙어 있게 된다.
    /// </summary>
    public void UnequipIfWearing(GameObject item)
    {
        if (item == null) return;

        if (item == weapon) weapon = null;
        if (item == armor) armor = null;
        if (item == accessory) accessory = null;
    }

    /// <summary>층을 넘거나 새 게임을 시작할 때.</summary>
    public void Clear()
    {
        weapon = armor = accessory = null;
    }

    private void Set(EquipmentSlot slot, GameObject item)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: weapon = item; break;
            case EquipmentSlot.Armor: armor = item; break;
            case EquipmentSlot.Accessory: accessory = item; break;
        }
    }

    /// <summary>기본 성능 + 강화 수치.</summary>
    private int PowerOf(GameObject item)
    {
        if (item == null) return 0;

        ItemDescription description = ItemInstance.DescriptionOf(item);
        int basePower = description != null ? description.basePower : 0;

        return basePower + ItemInstance.EnhancementOf(item);
    }
}
