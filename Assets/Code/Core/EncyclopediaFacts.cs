using System;
using System.Text;

/// <summary>
/// 도감 중단에 한 자리로 놓이는 것. 적이든 동료든 짜임이 같아서 하나로 둔다.
/// </summary>
/// <typeparam name="T">시트 한 줄의 형(MonsterDefinition · PartnerDefinition).</typeparam>
public class FactRow<T>
{
    /// <summary>줄머리에 붙는 이름("체력").</summary>
    public readonly string Label;

    /// <summary>
    /// 이만큼 겪어야 열린다. 적은 쓰러뜨린 횟수, 동료는 호감도다.
    ///
    /// <b>이 값이 같은 것끼리 한 줄에 놓인다</b>(EncyclopediaFacts.BuildLines).
    /// 같이 열리는 것을 따로 놓으면 줄만 길어지고, 한꺼번에 열리는 것이 눈에 안 띈다.
    /// </summary>
    public readonly int Required;

    private readonly Func<T, string> read;

    public FactRow(string label, int required, Func<T, string> read)
    {
        Label = label;
        Required = required;
        this.read = read;
    }

    public string ValueOf(T definition) => read(definition);
}

/// <summary>
/// 도감 중단의 글자를 만든다. 적 도감과 동료 도감이 같은 것을 쓴다.
///
/// ── 같이 열리는 것은 같은 줄에 ────────────────────────────────────
/// 항목마다 한 줄을 주면 일곱 줄이 되어 창을 가득 채운다. 문턱이 같은 것들은
/// 어차피 <b>한꺼번에 열리므로</b> 한 줄에 나란히 놓는다. 그러면 줄 수가 문턱
/// 수와 같아지고(셋), 한 줄이 통째로 물음표에서 값으로 바뀌는 것이 눈에 띈다.
///
/// 표에서 문턱을 바꾸면 줄이 저절로 갈리거나 합쳐진다. 줄 나누기를 따로 적어 두면
/// 문턱만 고쳤을 때 둘이 어긋나는데, 그래도 에러는 나지 않는다.
///
/// 화면도 입력도 모르므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class EncyclopediaFacts
{
    /// <summary>가려져 있는 값 자리에 놓는 글자.</summary>
    public const string HiddenValue = "???";

    /// <summary>시트 칸이 비어 있을 때 자리를 채우는 글자.</summary>
    public const string EmptyValue = "-";

    /// <summary>한 줄 안에서 항목과 항목 사이를 벌리는 간격.</summary>
    private const string Gap = "   ";

    /// <summary>
    /// 중단에 채울 글자. 문턱이 같은 항목끼리 한 줄로 묶고, 아직 안 열린 것은 가린다.
    /// </summary>
    /// <param name="progress">얼마나 겪었는가. 적은 쓰러뜨린 횟수, 동료는 호감도.</param>
    public static string BuildLines<T>(FactRow<T>[] rows, T definition, int progress)
    {
        if (rows == null || rows.Length == 0 || definition == null) return string.Empty;

        var sb = new StringBuilder();
        bool firstLine = true;

        for (int i = 0; i < rows.Length; i++)
        {
            // 앞 항목과 문턱이 다르면 줄을 바꾼다. 같으면 옆에 나란히 붙인다.
            bool startsLine = i == 0 || rows[i].Required != rows[i - 1].Required;

            if (startsLine)
            {
                if (!firstLine) sb.Append('\n');
                firstLine = false;
            }
            else
            {
                sb.Append(Gap);
            }

            FactRow<T> row = rows[i];
            sb.Append(row.Label).Append(" : ");
            sb.Append(progress >= row.Required ? row.ValueOf(definition) : HiddenValue);
        }

        return sb.ToString();
    }

    /// <summary>비어 있는 칸을 '-' 로 채운다. 빈칸으로 두면 줄이 끊긴 것처럼 보인다.</summary>
    public static string Or(string cell) => string.IsNullOrEmpty(cell) ? EmptyValue : cell;
}
