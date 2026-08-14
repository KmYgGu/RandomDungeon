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
        PartnerCatalog.Reload();
        TraitCatalog.Reload();
        PartnerLineCatalog.Reload();
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

    // 찾는 길이 둘이라 색인도 둘이다(SheetIndex 주석). 목록을 버릴 때 함께 버린다.
    private static Dictionary<int, TrapDefinition> byNumber;
    private static Dictionary<string, TrapDefinition> byId;

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

    public static void Reload()
    {
        all = null;
        byNumber = null;
        byId = null;
    }

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static TrapDefinition Find(int number)
    {
        if (byNumber == null) byNumber = SheetIndex.Build(All, d => d.Number);

        return byNumber.TryGetValue(number, out TrapDefinition definition) ? definition : null;
    }

    /// <summary>코드가 쓰는 이름(dart, gas …)으로 찾는다. 없으면 null.</summary>
    public static TrapDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (byId == null) byId = SheetIndex.Build(All, d => d.Id);

        return byId.TryGetValue(id, out TrapDefinition definition) ? definition : null;
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
    private static Dictionary<int, MonsterDefinition> byId;

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

    public static void Reload()
    {
        all = null;
        byId = null;
    }

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static MonsterDefinition Find(int id)
    {
        if (byId == null) byId = SheetIndex.Build(All, d => d.Id);

        return byId.TryGetValue(id, out MonsterDefinition definition) ? definition : null;
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

    // ItemDescription 의 이름·자리·성능이 전부 프로퍼티라 한 줄을 그리는 데만도
    // 여기를 여러 번 거친다. 가방 목록이 그것을 아이템 수만큼 되풀이한다.
    private static Dictionary<int, ItemDefinition> byId;

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

    public static void Reload()
    {
        all = null;
        byId = null;
    }

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static ItemDefinition Find(int id)
    {
        if (byId == null) byId = SheetIndex.Build(All, d => d.Id);

        return byId.TryGetValue(id, out ItemDefinition definition) ? definition : null;
    }
}

/// <summary>특성 표를 읽어 들고 있는 곳. 동료가 번호로 자기 특성을 찾는다.</summary>
public static class TraitCatalog
{
    private static List<TraitDefinition> all;
    private static Dictionary<int, TraitDefinition> byId;

    public static IReadOnlyList<TraitDefinition> All
    {
        get
        {
            if (all != null) return all;

            var problems = new List<string>();
            all = TraitSheet.FromEntities(ExcelSheets.Data?.trait, problems);
            ExcelSheets.Report("특성", problems);

            return all;
        }
    }

    public static void Reload()
    {
        all = null;
        byId = null;
    }

    /// <summary>특성 번호로 찾는다. 없으면 null.</summary>
    public static TraitDefinition Find(int id)
    {
        if (byId == null) byId = SheetIndex.Build(All, d => d.Id);

        return byId.TryGetValue(id, out TraitDefinition definition) ? definition : null;
    }
}

/// <summary>
/// 동료 표를 읽어 들고 있는 곳.
///
/// 표를 옮겨 담은 뒤 <b>특성 표와 맞춰 본다.</b> 번호를 잘못 적으면 그 특성만
/// 조용히 없는 것이 되는데, 동료는 멀쩡히 따라오고 에러도 나지 않는다.
/// 맞춰 보는 일을 여기서 하는 것은 두 표가 다 읽힌 뒤라야 할 수 있어서다.
/// </summary>
public static class PartnerCatalog
{
    private static List<PartnerDefinition> all;
    private static Dictionary<int, PartnerDefinition> byId;

    public static IReadOnlyList<PartnerDefinition> All
    {
        get
        {
            if (all != null) return all;

            var problems = new List<string>();
            all = PartnerSheet.FromEntities(ExcelSheets.Data?.partner, problems);
            PartnerSheet.ReportUnknownTraits(all, TraitCatalog.All, problems);
            ExcelSheets.Report("동료", problems);

            return all;
        }
    }

    public static void Reload()
    {
        all = null;
        byId = null;
    }

    /// <summary>표 번호로 찾는다. 없으면 null.</summary>
    public static PartnerDefinition Find(int id)
    {
        if (byId == null) byId = SheetIndex.Build(All, d => d.Id);

        return byId.TryGetValue(id, out PartnerDefinition definition) ? definition : null;
    }
}

/// <summary>동료 대사 표를 읽어 들고 있는 곳.</summary>
public static class PartnerLineCatalog
{
    private static List<PartnerLineSet> all;
    private static Dictionary<int, PartnerLineSet> byId;

    public static IReadOnlyList<PartnerLineSet> All
    {
        get
        {
            if (all != null) return all;

            var problems = new List<string>();
            all = PartnerLineSheet.FromEntities(ExcelSheets.Data?.partnerline, problems);
            ExcelSheets.Report("동료 대사", problems);

            return all;
        }
    }

    public static void Reload()
    {
        all = null;
        byId = null;
    }

    /// <summary>
    /// 동료 번호로 찾는다. 없으면 null.
    ///
    /// 대사가 아직 없는 동료가 있으므로 <b>null 은 잘못이 아니다.</b>
    /// 부르는 쪽은 말을 하지 않는 것으로 다루면 된다.
    /// </summary>
    public static PartnerLineSet Find(int id)
    {
        if (byId == null) byId = SheetIndex.Build(All, d => d.Id);

        return byId.TryGetValue(id, out PartnerLineSet lines) ? lines : null;
    }
}
