using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시트의 줄마다 정의 애셋을 하나씩 만들어 준다.
///
/// 함정 9종 · 적 1종을 손으로 만들면서 번호를 옮겨 적으면 오타가 난다.
/// 번호는 기계가 넣고, 사람은 그림만 끌어다 놓으면 되게 한다.
///
/// 이미 있는 애셋은 건드리지 않는다. 사람이 넣어 둔 그림을 지우지 않기 위해서다.
/// </summary>
public static class DescriptionGenerator
{
    private const string TrapFolder = "Assets/Prefab/Traps/trapData";
    private const string MonsterFolder = "Assets/Prefab/Monsters/monsterData";
    private const string ItemFolder = "Assets/Prefab/Items/itemData";
    private const string PartnerFolder = "Assets/Prefab/partner/partnerData";

    [MenuItem("Tools/시트에서 아이템 정의 만들기")]
    public static void GenerateItems()
    {
        ExcelSheets.Reload();

        var made = new List<string>();
        EnsureFolder(ItemFolder);

        foreach (ItemDefinition definition in ItemCatalog.All)
        {
            string path = $"{ItemFolder}/{FileNameOf(definition.Name, definition.Id)}.asset";
            if (File.Exists(path)) continue;

            var asset = ScriptableObject.CreateInstance<ItemDescription>();
            asset.id = definition.Id;
            AssetDatabase.CreateAsset(asset, path);
            made.Add(definition.Name);
        }

        Finish("아이템", made, ItemCatalog.All.Count);
    }

    [MenuItem("Tools/시트에서 함정 정의 만들기")]
    public static void GenerateTraps()
    {
        ExcelSheets.Reload();

        var made = new List<string>();
        EnsureFolder(TrapFolder);

        foreach (TrapDefinition definition in TrapCatalog.All)
        {
            string path = $"{TrapFolder}/{FileNameOf(definition.Name, definition.Number)}.asset";
            if (File.Exists(path)) continue;

            var asset = ScriptableObject.CreateInstance<TrapDescription>();
            asset.id = definition.Number;
            AssetDatabase.CreateAsset(asset, path);
            made.Add(definition.Name);
        }

        Finish("함정", made, TrapCatalog.All.Count);
    }

    [MenuItem("Tools/시트에서 적 정의 만들기")]
    public static void GenerateMonsters()
    {
        ExcelSheets.Reload();

        var made = new List<string>();
        EnsureFolder(MonsterFolder);

        foreach (MonsterDefinition definition in MonsterCatalog.All)
        {
            string path = $"{MonsterFolder}/{FileNameOf(definition.Name, definition.Id)}.asset";
            if (File.Exists(path)) continue;

            var asset = ScriptableObject.CreateInstance<MonsterDescription>();
            asset.id = definition.Id;
            AssetDatabase.CreateAsset(asset, path);
            made.Add(definition.Name);
        }

        Finish("적", made, MonsterCatalog.All.Count);
    }

    [MenuItem("Tools/시트에서 동료 정의 만들기")]
    public static void GeneratePartners()
    {
        ExcelSheets.Reload();

        var made = new List<string>();
        EnsureFolder(PartnerFolder);

        foreach (PartnerDefinition definition in PartnerCatalog.All)
        {
            string path = $"{PartnerFolder}/{FileNameOf(definition.Name, definition.Id)}.asset";
            if (File.Exists(path)) continue;

            var asset = ScriptableObject.CreateInstance<PartnerDescription>();
            asset.id = definition.Id;
            AssetDatabase.CreateAsset(asset, path);
            made.Add(definition.Name);
        }

        Finish("동료", made, PartnerCatalog.All.Count);
    }

    /// <summary>이름이 비어 있으면 번호로 짓는다. 파일 이름에 못 쓰는 글자는 뺀다.</summary>
    private static string FileNameOf(string name, int number)
    {
        if (string.IsNullOrEmpty(name)) return number.ToString();

        foreach (char bad in Path.GetInvalidFileNameChars())
            name = name.Replace(bad, '_');

        return name;
    }

    private static void EnsureFolder(string folder)
    {
        if (Directory.Exists(folder)) return;

        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
    }

    private static void Finish(string what, List<string> made, int total)
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (total == 0)
        {
            Debug.LogWarning(
                $"{what} 표가 비어 있습니다. ExcelData.xlsx 를 한 번 저장해 " +
                "애셋을 다시 만든 뒤 실행하세요.");
            return;
        }

        Debug.Log(made.Count == 0
            ? $"{what} 정의 {total}개가 이미 다 있습니다. 새로 만든 것은 없습니다."
            : $"{what} 정의 {made.Count}개를 만들었습니다: {string.Join(", ", made)}");
    }
}
