using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 상태이상 표를 읽어 들고 있는 곳.
///
/// 표 자체는 Assets/Resources/ExcelData.xlsx 이고 엑셀에서 편집한다.
/// 저장하면 ExcelImporter 가 ExcelData.asset 을 만들어 주고, 여기서 그것을 읽는다.
/// 여기서는 한 번만 읽어 두고, 코드가 물어볼 때 꺼내 준다.
/// </summary>
public static class ConditionCatalog
{
    // ── 코드가 직접 가리키는 것들 ──────────────────────────────────────────
    //    시트의 id 와 같아야 한다. 시트에서 id 를 바꾸면 여기도 바꿔야 한다.
    public const string HungryId = "hungry";
    public const string StarvingId = "starving";

    private static List<ConditionDefinition> all;

    /// <summary>
    /// 표에 적힌 모든 상태. 해로운 것부터, 같은 분류 안에서는 시트에 적힌 차례로.
    ///
    /// 표시 순서가 뜰 때마다 달라지면 어느 줄이 무엇인지 눈으로 좇을 수 없다.
    /// </summary>
    public static IReadOnlyList<ConditionDefinition> All
    {
        get
        {
            if (all != null) return all;

            var data = Resources.Load<ExcelData>(ConditionSheet.ResourcePath);
            if (data == null)
            {
                // 엑셀을 한 번도 저장하지 않았거나 임포터가 아직 돌지 않은 상태.
                Debug.LogWarning(
                    $"상태이상 표를 찾지 못했습니다: Resources/{ConditionSheet.ResourcePath}. " +
                    "ExcelData.xlsx 를 한 번 저장하면 만들어집니다.");
                all = new List<ConditionDefinition>();
                return all;
            }

            var problems = new List<string>();
            all = Sort(ConditionSheet.FromEntities(data.Conditions, problems));

            // 표를 고치는 사람이 무엇이 잘못됐는지 바로 알 수 있게 알린다.
            foreach (string problem in problems)
                Debug.LogWarning($"상태이상 표: {problem}");

            return all;
        }
    }

    /// <summary>표를 고친 뒤 다시 읽게 한다. 에디터에서 손볼 때 쓴다.</summary>
    public static void Reload() => all = null;

    public static ConditionDefinition Find(string id)
    {
        foreach (ConditionDefinition definition in All)
        {
            if (definition.Id == id) return definition;
        }
        return null;
    }

    /// <summary>해로움 → 이로움 → 중립. 같은 분류 안에서는 시트 차례를 지킨다.</summary>
    public static List<ConditionDefinition> Sort(List<ConditionDefinition> definitions)
    {
        var sorted = new List<ConditionDefinition>();
        foreach (ConditionKind kind in new[]
                 { ConditionKind.Harmful, ConditionKind.Beneficial, ConditionKind.Neutral })
        {
            foreach (ConditionDefinition definition in definitions)
            {
                if (definition.Kind == kind) sorted.Add(definition);
            }
        }
        return sorted;
    }
}

/// <summary>
/// 상태 화면에 무엇을 어떻게 띄울지 계산한다.
///
/// 글자를 만들고 스크롤 자리를 정하는 일만 한다. 화면도 입력도 모르므로
/// Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ConditionReport
{
    /// <summary>아무 이상이 없을 때.</summary>
    public const string HealthyText = "특별함 없이 건강하다";

    /// <summary>
    /// 지금 걸려 있는 것들.
    ///
    /// 굶주림은 따로 걸어 주는 곳이 없어도 만복도만 보면 알 수 있으므로 여기서 뽑는다.
    /// 나머지는 그것을 거는 시스템(독 함정·두루마리 같은 것)이 생기면 더해진다.
    /// </summary>
    public static List<ConditionDefinition> Active(PlayerStats stats)
    {
        var active = new List<ConditionDefinition>();
        if (stats == null) return active;

        foreach (ConditionDefinition definition in ConditionCatalog.All)
        {
            if (IsActive(definition, stats)) active.Add(definition);
        }
        return active;
    }

    private static bool IsActive(ConditionDefinition definition, PlayerStats stats)
    {
        switch (definition.Id)
        {
            case ConditionCatalog.StarvingId:
                return stats.IsStarving;

            case ConditionCatalog.HungryId:
                // 바닥나기 전, 경고가 시작되는 지점부터를 '배고프다' 로 본다.
                // 이미 바닥났다면 굶주림 쪽이 대신 뜨므로 겹쳐 보이지 않는다.
                return !stats.IsStarving
                    && stats.Hunger <= stats.MaxHunger * HungerWarning.RumblePercent / 100;

            default:
                // 아직 이 상태를 거는 것이 게임 안에 없다.
                return false;
        }
    }

    /// <summary>
    /// 스크롤 위치를 목록 안으로 되돌린다.
    ///
    /// 위로 더 올릴 것이 없으면 0, 아래로 더 내릴 것이 없으면 화면이 꽉 차는 자리에 멈춘다.
    /// 다 들어가는 짧은 목록은 아예 움직이지 않는다.
    /// </summary>
    public static int ClampOffset(int offset, int count, int capacity)
    {
        if (capacity <= 0) return 0;
        return Mathf.Clamp(offset, 0, Mathf.Max(0, count - capacity));
    }

    /// <summary>
    /// 판에 채울 글자. capacity 줄까지만, offset 번째부터 보여 준다.
    ///
    /// 번호는 화면에 보이는 순서가 아니라 목록에서의 실제 번호다.
    /// 스크롤해서 넷째 줄부터 보고 있으면 "4." 로 시작한다 —
    /// 그래야 지금 몇 번째를 보고 있는지 알 수 있다.
    /// </summary>
    public static string BuildText(
        IReadOnlyList<ConditionDefinition> active, int offset, int capacity)
    {
        if (active == null || active.Count == 0) return HealthyText;

        int start = ClampOffset(offset, active.Count, capacity);
        int end = Mathf.Min(start + Mathf.Max(capacity, 1), active.Count);

        var sb = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            if (i > start) sb.Append('\n');
            sb.Append(i + 1).Append(". ").Append(active[i].Description);
        }
        return sb.ToString();
    }
}
