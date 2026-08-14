using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 아이템 도감 화면에 무엇을 어떻게 띄울지 계산한다.
///
/// 글자를 만드는 일만 한다. 화면도 입력도 모르므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ItemEncyclopediaReport
{
    /// <summary>번호(id)가 작은 것부터 늘어놓는다. 화면의 "No." 가 이 순서를 따른다.</summary>
    public static List<ItemDefinition> SortedEntries(IEnumerable<ItemDefinition> all)
    {
        var sorted = new List<ItemDefinition>(all);
        sorted.Sort((a, b) => a.Id.CompareTo(b.Id));
        return sorted;
    }

    /// <summary>
    /// 판에 채울 글자. capacity 줄까지만, offset 번째부터 보여 준다.
    ///
    /// 아직 만나 보지 못한 아이템은 이름 대신 물음표로 가린다. 번호는 화면에
    /// 보이는 순서가 아니라 목록에서의 실제 번호다(ConditionReport 와 같은 이유).
    /// </summary>
    public static string BuildText(
        IReadOnlyList<ItemDefinition> sorted, ItemEncyclopedia dex,
        int selected, int offset, int capacity)
    {
        if (sorted == null || sorted.Count == 0) return string.Empty;

        int end = capacity > 0 ? Mathf.Min(sorted.Count, offset + capacity) : sorted.Count;

        var sb = new StringBuilder();
        for (int i = offset; i < end; i++)
        {
            if (i > offset) sb.Append('\n');
            sb.Append(i == selected ? "> " : "  ");
            sb.Append(i + 1).Append(". ");
            sb.Append(dex.IsDiscovered(sorted[i].Id) ? sorted[i].Name : "?");
        }
        return sb.ToString();
    }
}
