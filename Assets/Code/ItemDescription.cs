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
}
