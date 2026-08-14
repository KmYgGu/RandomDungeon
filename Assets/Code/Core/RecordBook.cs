using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 지난 모험들의 기록. 상태창의 '기록' 이 이것을 보여 준다.
///
/// 무엇을 적는가는 표(RecordCatalog)가 쥐고 있고, 여기서는 <b>어떻게 쌓고
/// 어떻게 저장하는가</b>만 안다. 그래서 항목을 늘려도 이 파일은 그대로다.
///
/// ── 한 번도 없었던 것과 0 은 다르다 ───────────────────────────────
/// "가장 크게 준 피해 0" 과 "아직 한 번도 때려 본 적이 없다" 는 다른 이야기다.
/// 적어 둔 적이 있는지를 따로 들고 있다가, 없으면 <b>물음표로 보여 준다.</b>
/// 0 으로 채워 두면 아직 안 해 본 것과 해 봤지만 0 인 것이 구별되지 않는다.
///
/// <b>이번 판의 점수는 여기 없다.</b> 점수는 모험이 끝나는 순간에만 매겨진다
/// (RunScore 주석). 도중에 기록을 열어도 지난 판까지의 것만 보인다.
///
/// 저장하는 법은 모른다 — 그 일은 EncyclopediaStorage 가 한다.
/// Unity 것을 하나도 건드리지 않으므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class RecordBook
{
    private readonly Dictionary<RecordStat, int> values = new Dictionary<RecordStat, int>();

    /// <summary>아직 아무것도 적히지 않았는가.</summary>
    public bool IsEmpty => values.Count == 0;

    /// <summary>한 번이라도 적힌 적이 있는가. 없으면 화면에 물음표가 뜬다.</summary>
    public bool Has(RecordStat stat) => values.ContainsKey(stat);

    /// <summary>적어 둔 값. 없으면 0(있는지는 Has 로 묻는다).</summary>
    public int ValueOf(RecordStat stat)
        => values.TryGetValue(stat, out int value) ? value : 0;

    /// <summary>
    /// 기록을 적는다. 쌓는 방법은 표가 정한다(RecordKind).
    /// 최고 기록을 <b>새로 세웠으면</b> true.
    ///
    /// Max 는 첫 값이면 무조건 세운 것으로 본다 — 0 점짜리 첫 모험도 기록이다.
    /// 크기만 견주면 처음 것이 조용히 안 적힌다.
    /// </summary>
    public bool Report(RecordStat stat, int value)
    {
        RecordDefinition definition = RecordCatalog.Find(stat);
        if (definition == null) return false;

        bool had = values.TryGetValue(stat, out int current);

        switch (definition.Kind)
        {
            case RecordKind.Total:
                // 음수로 기록을 깎을 수는 없다.
                if (value <= 0) return false;
                values[stat] = current + value;
                return true;

            case RecordKind.Last:
                values[stat] = value;
                return true;

            default:   // Max
                if (had && value <= current) return false;
                values[stat] = value;
                return true;
        }
    }

    /// <summary>화면에 놓이는 줄 수. 갈래 사이의 빈 줄까지 센다.</summary>
    public int LineCount => BuildLines().Count;

    /// <summary>
    /// 기록 화면에 채울 글자. capacity 줄까지만, offset 번째부터 보여 준다.
    ///
    /// 넘겨 보는 규칙은 로그·상태 화면과 같은 것을 쓴다(PageScroll).
    /// </summary>
    public string BuildText(int offset, int capacity)
    {
        List<string> lines = BuildLines();
        if (lines.Count == 0) return "아직 기록이 없다";

        int start = PageScroll.ClampOffset(offset, lines.Count, capacity);
        int end = Mathf.Min(start + Mathf.Max(capacity, 1), lines.Count);

        var sb = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            if (i > start) sb.Append('\n');
            sb.Append(lines[i]);
        }
        return sb.ToString();
    }

    /// <summary>표에 적힌 차례대로 한 줄씩. 한 번도 없었던 것은 물음표.</summary>
    private List<string> BuildLines()
    {
        var lines = new List<string>();

        foreach (RecordDefinition definition in RecordCatalog.All)
        {
            // 갈래가 바뀌는 자리에 빈 줄을 넣는다. 맨 앞에는 넣지 않는다.
            if (definition.StartsGroup && lines.Count > 0) lines.Add(string.Empty);

            string value = Has(definition.Stat) ? ValueOf(definition.Stat).ToString() : "?";
            lines.Add($"{definition.Label} : {value}");
        }

        return lines;
    }

    /// <summary>저장할 글자. "이름=값" 을 쉼표로 늘어놓는다.</summary>
    public string Save()
    {
        var pieces = new List<string>();

        // 표의 차례대로 적는다. Dictionary 를 그대로 돌면 저장할 때마다 순서가
        // 달라져서, 파일이 바뀌었는지 눈으로 견줄 수 없다.
        foreach (RecordDefinition definition in RecordCatalog.All)
        {
            if (!Has(definition.Stat)) continue;
            pieces.Add($"{definition.Id}={ValueOf(definition.Stat)}");
        }

        return string.Join(",", pieces);
    }

    /// <summary>
    /// 저장된 글자에서 되살린다.
    ///
    /// 못 읽는 조각과 <b>모르는 이름</b>은 건너뛴다. 모르는 이름은 잘못이 아니라
    /// 지워진 옛 항목이다 — 그것 때문에 나머지 기록까지 버리면, 항목 하나를
    /// 정리했다고 지난 기록이 통째로 날아간다.
    /// </summary>
    public static RecordBook Load(string saved)
    {
        var book = new RecordBook();
        if (string.IsNullOrEmpty(saved)) return book;

        foreach (string piece in saved.Split(','))
        {
            string[] parts = piece.Split('=');
            if (parts.Length != 2) continue;

            RecordDefinition definition = RecordCatalog.Find(parts[0]);
            if (definition == null) continue;

            if (!int.TryParse(parts[1], out int value)) continue;

            book.values[definition.Stat] = value;
        }

        return book;
    }
}
