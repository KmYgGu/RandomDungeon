using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 플레이어의 가방.
///
/// 예전에는 GameManager 의 public static List&lt;GameObject&gt; items 와
/// public static int maxbag 였고, 용량 검사(items.Count &lt; maxbag)를
/// 아이템을 줍는 쪽(Player)에서 직접 하고 있었다.
/// 가방의 규칙은 가방이 알고 있어야 한다.
/// </summary>
public class Inventory
{
    private readonly List<GameObject> items = new List<GameObject>();

    public int Capacity { get; }

    public Inventory(int capacity)
    {
        Capacity = capacity;
    }

    public int Count => items.Count;
    public bool IsEmpty => items.Count == 0;
    public bool IsFull => items.Count >= Capacity;

    public GameObject this[int index] => items[index];

    /// <summary>가방에 넣는다. 가득 찼으면 넣지 않고 false.</summary>
    public bool TryAdd(GameObject item)
    {
        if (IsFull) return false;
        items.Add(item);
        return true;
    }

    public GameObject RemoveAt(int index)
    {
        GameObject removed = items[index];
        items.RemoveAt(index);
        return removed;
    }

    /// <summary>
    /// 그 자리의 것을 다른 것으로 갈아 끼우고, 밀려난 것을 돌려준다.
    ///
    /// 발밑의 것과 맞바꿀 때 쓴다. 빼고 다시 넣으면 목록 맨 끝으로 가 버려서
    /// 커서가 가리키던 줄이 바뀐다. 자리를 지키는 편이 손에 자연스럽다.
    /// </summary>
    public GameObject Replace(int index, GameObject item)
    {
        GameObject previous = items[index];
        items[index] = item;
        return previous;
    }

    public void Clear()
    {
        items.Clear();
    }

    /// <summary>인덱스를 가방 범위 안으로 되돌린다(위/아래 끝에서 반대편으로 순환).</summary>
    public int WrapIndex(int index)
    {
        if (IsEmpty) return 0;
        if (index < 0) return items.Count - 1;
        if (index >= items.Count) return 0;
        return index;
    }

    /// <summary>
    /// 가방 안 내용을, 고른 줄에 커서를 붙여 한 덩이 문자열로 만든다.
    ///
    /// 예전에는 내용과 커서를 두 개의 문자열로 따로 만들어 두 개의 글자 상자에
    /// 겹쳐 그렸다. 그런데 행동 메뉴와 상태창 메뉴는 커서를 글자 안에 넣고 있어서,
    /// 같은 "커서 붙은 목록" 을 만드는 방법이 프로젝트 안에 둘이었다.
    /// 겹쳐 그리는 쪽은 두 상자의 줄간격이 조금만 달라도 커서와 줄이 어긋난다.
    /// 이제 한 가지 방법으로 모았다.
    /// </summary>
    public string BuildListText(int selectedIndex, string emptyText)
    {
        if (IsEmpty) return emptyText;

        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(i == selectedIndex ? "> " : "  ");
            sb.Append(items[i].name);
        }
        return sb.ToString();
    }
}
