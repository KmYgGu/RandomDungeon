using UnityEngine;

/// <summary>
/// 바닥에 떨어져 있거나 가방에 든 아이템 '한 개' 의 개별 정보.
///
/// ItemDescription 은 종류마다 하나뿐인 설명서라서 "이 검은 +3, 저 검은 +0"
/// 같은 개체별 차이를 담을 수 없다. 그런 값은 여기에 둔다.
///
/// 이 컴포넌트가 붙어 있지 않은 아이템은 강화 0 으로 본다.
/// 그래서 기존 소모품 프리팹들은 아무것도 고치지 않아도 된다.
/// </summary>
public class ItemInstance : MonoBehaviour
{
    /// <summary>강화 수치(+n). 무기면 공격력, 방어구면 방어력에 그대로 더해진다.</summary>
    public int enhancement;

    /// <summary>컴포넌트가 없으면 0.</summary>
    public static int EnhancementOf(GameObject item)
    {
        if (item == null) return 0;

        ItemInstance instance = item.GetComponent<ItemInstance>();
        return instance != null ? instance.enhancement : 0;
    }
}
