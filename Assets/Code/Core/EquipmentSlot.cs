/// <summary>
/// 장비를 끼울 수 있는 자리. 상태창의 세 슬롯과 그대로 대응한다.
///
/// None 이 기본값인 이유: ItemDescription 애셋에 이 필드를 새로 추가하면
/// 기존 아이템들은 자동으로 0 이 된다. 그때 실수로 무기가 되어 버리면 곤란하다.
/// </summary>
public enum EquipmentSlot
{
    /// <summary>장비가 아니다.</summary>
    None = 0,
    Weapon = 1,
    Armor = 2,
    Accessory = 3,
}
