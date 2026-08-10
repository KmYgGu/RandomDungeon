/// <summary>
/// 아이템의 갈래. 시트의 itemtype 열이 이것을 고른다.
///
/// 예전에는 ItemDescription 안의 중첩 enum 이었다. 시트가 이 값을 정하게 되면서
/// 시트를 읽는 쪽(ItemSheet)도 이것을 알아야 하는데, 그쪽은 Unity 를 몰라야
/// Unity 없이 확인할 수 있으므로 밖으로 꺼냈다.
///
/// '쓴다' 자리에 들어갈 말이 이걸로 갈린다 — 먹는 것이면 "먹는다",
/// 아니면 "사용한다". 장비는 갈래와 무관하게 EquipmentSlot 이 정한다.
/// </summary>
public enum ItemKind
{
    /// <summary>먹는 것.</summary>
    Food = 0,
    /// <summary>쓰면 없어지는 것.</summary>
    Expendables = 1,
    /// <summary>몸에 걸치는 것.</summary>
    Equipment = 2,
    /// <summary>그 밖의 것.</summary>
    Special = 3,
}
