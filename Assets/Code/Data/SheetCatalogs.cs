using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 엑셀에서 만들어진 애셋을 한 번만 읽어 두는 곳.
///
/// 표가 넷으로 늘면서 "Resources 에서 불러오고, 없으면 알리고, 이상한 줄을
/// 로그로 남긴다" 가 카탈로그마다 반복될 참이었다. 그 일은 여기서 한 번만 한다.
///
/// 로그를 여기서 찍는 이유는 반대다 — 표를 옮겨 담는 쪽(ConditionSheet 등)은
/// Unity 를 몰라야 Unity 없이 확인할 수 있기 때문에, 알리는 일만 이쪽으로 뺐다.
/// </summary>
public static class ExcelSheets
{
    private static ExcelData data;
    private static bool tried;

    /// <summary>못 찾으면 null. 한 번만 불러오고 그 뒤로는 들고 있던 것을 준다.</summary>
    public static ExcelData Data
    {
        get
        {
            if (tried) return data;
            tried = true;

            data = Resources.Load<ExcelData>(ConditionSheet.ResourcePath);
            if (data == null)
            {
                // 엑셀을 한 번도 저장하지 않았거나 임포터가 아직 돌지 않은 상태.
                Debug.LogWarning(
                    $"표를 찾지 못했습니다: Resources/{ConditionSheet.ResourcePath}. " +
                    "ExcelData.xlsx 를 한 번 저장하면 만들어집니다.");
            }
            return data;
        }
    }

    /// <summary>표를 고친 뒤 다시 읽게 한다. 카탈로그들도 함께 비운다.</summary>
    public static void Reload()
    {
        data = null;
        tried = false;

        ConditionCatalog.Reload();
        TrapCatalog.Reload();
        MonsterCatalog.Reload();
        ItemCatalog.Reload();
    }

    /// <summary>표를 고치는 사람이 무엇이 잘못됐는지 바로 알 수 있게 알린다.</summary>
    public static void Report(string sheetName, List<string> problems)
    {
        if (problems == null) return;

        foreach (string problem in problems)
            Debug.LogWarning($"{sheetName} 표: {problem}");
    }
}

/// <summary>
/// 함정 표를 읽어 들고 있는 곳.
///
/// 프리팹은 번호(Number)로 자기 정의를 찾고, 효과를 만드는 코드는 이름(Id)으로
/// 갈래를 나눈다. 둘 다 필요해서 양쪽으로 찾을 수 있게 두었다.
/// </summary>
public static class TrapCatalog
{
    private static List<TrapDefinition> all;

    public static IReadOnlyList<TrapDefinition> All
    {
        get
        {
            if (all != null) return all;

            var problems = new List<string>();
            all = TrapSheet.FromEntities(ExcelSheets.Data?.Trap, problems);
            ExcelSheets.Report("함정", problems);

            return all;
        }
    }

    public static void Reload() => all = null;

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static TrapDefinition Find(int number)
    {
        foreach (TrapDefinition definition in All)
        {
            if (definition.Number == number) return definition;
        }
        return null;
    }

    /// <summary>코드가 쓰는 이름(dart, gas …)으로 찾는다. 없으면 null.</summary>
    public static TrapDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (TrapDefinition definition in All)
        {
            if (definition.Id == id) return definition;
        }
        return null;
    }
}

/// <summary>
/// 적 표를 읽어 들고 있는 곳.
///
/// 적은 코드가 특정 마리를 가리킬 일이 없어서 번호로만 찾는다.
/// </summary>
public static class MonsterCatalog
{
    private static List<MonsterDefinition> all;

    public static IReadOnlyList<MonsterDefinition> All
    {
        get
        {
            if (all != null) return all;

            var problems = new List<string>();
            all = MonsterSheet.FromEntities(ExcelSheets.Data?.Monster, problems);
            ExcelSheets.Report("적", problems);

            return all;
        }
    }

    public static void Reload() => all = null;

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static MonsterDefinition Find(int id)
    {
        foreach (MonsterDefinition definition in All)
        {
            if (definition.Id == id) return definition;
        }
        return null;
    }
}

/// <summary>
/// 아이템 표를 읽어 들고 있는 곳.
///
/// 예전의 ItemCatalog 는 이름으로 ItemDescription 애셋을 찾아 주는 색인이었다.
/// 지금은 아이템 오브젝트가 자기 정의를 직접 들고 다니므로(ItemInstance)
/// 그 색인이 필요 없어졌고, 이 이름은 시트를 들고 있는 쪽이 물려받았다.
/// </summary>
public static class ItemCatalog
{
    private static List<ItemDefinition> all;

    public static IReadOnlyList<ItemDefinition> All
    {
        get
        {
            if (all != null) return all;

            var problems = new List<string>();
            all = ItemSheet.FromEntities(ExcelSheets.Data?.item, problems);
            ExcelSheets.Report("아이템", problems);

            return all;
        }
    }

    public static void Reload() => all = null;

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static ItemDefinition Find(int id)
    {
        foreach (ItemDefinition definition in All)
        {
            if (definition.Id == id) return definition;
        }
        return null;
    }
}
