using UnityEditor;
using UnityEngine;

/// <summary>
/// 정의 애셋의 인스펙터에 시트 값을 읽기 전용으로 보여 준다.
///
/// TrapDescription · MonsterDescription 은 번호와 그림만 들고 있고 나머지는
/// 엑셀에서 읽는다. 그래서 애셋을 열어도 인스펙터가 거의 비어 있어서,
/// 번호를 잘못 적었는지 눈으로 알 수 없다. 여기서 그것을 메워 준다.
///
/// 값을 보여 주기만 하고 애셋에 쓰지 않는다 — 쓰기 시작하면 시트의 사본이
/// 생기고, 시트를 고쳤을 때 조용히 어긋난다.
/// </summary>
public static class SheetPreview
{
    /// <summary>표에서 못 찾았을 때. 번호 오타가 가장 흔한 원인이다.</summary>
    public static void DrawNotFound(int id)
    {
        EditorGUILayout.HelpBox(
            $"{id} 번을 표에서 찾지 못했습니다.\n" +
            "번호가 맞는지, 그리고 ExcelData.xlsx 를 저장해 애셋이 최신인지 확인하세요.",
            MessageType.Warning);
    }

    public static void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("엑셀에서 읽어 온 값 (읽기 전용)", EditorStyles.boldLabel);
    }

    public static void DrawRow(string label, string value)
    {
        EditorGUILayout.LabelField(label, string.IsNullOrEmpty(value) ? "—" : value);
    }
}

[CustomEditor(typeof(TrapDescription))]
public class TrapDescriptionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var description = (TrapDescription)target;
        TrapDefinition definition = description.Definition;

        SheetPreview.DrawHeader();

        if (definition == null)
        {
            SheetPreview.DrawNotFound(description.id);
            return;
        }

        SheetPreview.DrawRow("이름", definition.Name);
        SheetPreview.DrawRow("코드 이름", definition.Id);
        SheetPreview.DrawRow("몸짓", definition.Animation.ToString());
        SheetPreview.DrawRow("설정", definition.Note);

        EditorGUILayout.LabelField("문구");
        EditorGUILayout.SelectableLabel(
            string.IsNullOrEmpty(definition.Message) ? "(비어 있음)" : definition.Message,
            EditorStyles.wordWrappedLabel, GUILayout.Height(32f));
    }
}

[CustomEditor(typeof(ItemDescription))]
public class ItemDescriptionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var description = (ItemDescription)target;
        ItemDefinition definition = description.Definition;

        SheetPreview.DrawHeader();

        if (definition == null)
        {
            SheetPreview.DrawNotFound(description.id);
            return;
        }

        SheetPreview.DrawRow("이름", definition.Name);
        SheetPreview.DrawRow("갈래", definition.Kind.ToString());
        SheetPreview.DrawRow("판매 가격", $"{definition.SellPrice}G");
        SheetPreview.DrawRow("장비 자리", definition.IsEquipment
            ? $"{definition.Slot} (기본 성능 {definition.BasePower})"
            : "장비 아님");

        EditorGUILayout.LabelField("설명");
        EditorGUILayout.SelectableLabel(
            string.IsNullOrEmpty(definition.Description) ? "(비어 있음)" : definition.Description,
            EditorStyles.wordWrappedLabel, GUILayout.Height(32f));

        // 효과가 붙어 있지 않으면 먹어도 마셔도 아무 일이 없다.
        // 번호를 잘못 적었을 때 가장 먼저 드러나는 곳이기도 하다.
        if (!ItemEffects.IsUsable(definition.Id) && !definition.IsEquipment)
        {
            EditorGUILayout.HelpBox(
                $"{definition.Id} 번에 붙은 효과가 없습니다 (ItemEffects). 써도 아무 일도 일어나지 않습니다.",
                MessageType.Info);
        }
    }
}

[CustomEditor(typeof(MonsterDescription))]
public class MonsterDescriptionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var description = (MonsterDescription)target;
        MonsterDefinition definition = description.Definition;

        SheetPreview.DrawHeader();

        if (definition == null)
        {
            SheetPreview.DrawNotFound(description.id);
            return;
        }

        SheetPreview.DrawRow("이름", definition.Name);
        SheetPreview.DrawRow("체력 · 공격 · 방어",
            $"{definition.Hp} · {definition.Attack} · {definition.Defense}");
        SheetPreview.DrawRow("경험치", definition.Exp.ToString());
        SheetPreview.DrawRow("아이템 드롭 확률", MonsterSheet.NameOf(definition.Rate));
        SheetPreview.DrawRow("갈래", definition.Category);

        EditorGUILayout.LabelField("설명");
        EditorGUILayout.SelectableLabel(
            string.IsNullOrEmpty(definition.Description) ? "(비어 있음)" : definition.Description,
            EditorStyles.wordWrappedLabel, GUILayout.Height(32f));
    }
}
