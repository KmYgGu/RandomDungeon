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
    // Enum�� �ʵ�� �����Ͽ� Inspector�� ǥ��
    public ItemType itemType;
}
