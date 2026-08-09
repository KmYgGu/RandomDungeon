using System.Collections.Generic;

/// <summary>
/// 아이템 이름으로 ItemDescription 을 찾아 주는 색인.
///
/// 예전에는 allItemDescriptions 배열을 foreach 로 훑는 코드가
/// GameManager 안에 세 군데 복붙되어 있었다(UpdateMenuCursor,
/// GetItemDescriptionByName, PerformAction). 같은 일을 하는 코드는 한 벌이면 된다.
/// 덤으로 선형 탐색이 사전 조회로 바뀐다.
/// </summary>
public class ItemCatalog
{
    private readonly Dictionary<string, ItemDescription> byName =
        new Dictionary<string, ItemDescription>();

    public ItemCatalog(IEnumerable<ItemDescription> descriptions)
    {
        if (descriptions == null) return;

        foreach (ItemDescription desc in descriptions)
        {
            if (desc == null || string.IsNullOrEmpty(desc.itemName)) continue;
            byName[desc.itemName] = desc;
        }
    }

    /// <summary>찾지 못하면 null.</summary>
    public ItemDescription Find(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        byName.TryGetValue(itemName, out ItemDescription desc);
        return desc;
    }

    public bool Contains(string itemName) => Find(itemName) != null;
}
