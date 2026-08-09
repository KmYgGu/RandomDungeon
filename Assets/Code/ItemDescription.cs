using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewItemDescription", menuName = "Item/Description")]
public class ItemDescription : ScriptableObject
{
    public Sprite sprites;

    public string itemName;
    [TextArea]
    public string description;

    public int SellPrice;

    public enum ItemType
    {
        Food,
        Expendables,
        Equipment,
        Special
    }
    // Enum을 필드로 선언하여 Inspector에 표시
    public ItemType itemType;

    [Header("장비일 때만 쓰는 값")]

    /// <summary>어느 슬롯에 끼우는가. None 이면 장비가 아니다.</summary>
    public EquipmentSlot equipSlot = EquipmentSlot.None;

    /// <summary>
    /// 강화하지 않은 상태의 기본 성능.
    /// 무기면 공격력에, 방어구면 방어력에 더해진다.
    /// </summary>
    public int basePower;

    /// <summary>실제로 장비로 쓸 수 있는가.</summary>
    public bool IsEquipment => equipSlot != EquipmentSlot.None;
}
