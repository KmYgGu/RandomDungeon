/// <summary>표식의 모양. None 이면 표식이 없는 것이다.</summary>
public enum MarkShape
{
    None = 0,
    Circle = 1,
    Triangle = 2,
    Square = 3,
}

/// <summary>표식의 색. 무지개 차례로 여덟 가지.</summary>
public enum MarkColor
{
    Red = 0,
    Orange = 1,
    Yellow = 2,
    YellowGreen = 3,
    Green = 4,
    SkyBlue = 5,
    Blue = 6,
    Purple = 7,
}

/// <summary>
/// 가방의 아이템에 손으로 붙이는 표식.
///
/// 같은 이름의 물건이 여럿일 때 "이건 아까 그 저주받은 검" 처럼 스스로 표시해
/// 두라는 것이다. 게임 규칙은 표식을 전혀 보지 않는다 — 순전히 사람이 눈으로
/// 가려내기 위한 것이라, 무엇을 어떤 색으로 두든 코드가 상관하지 않는다.
///
/// ── 왜 개체마다 붙는가 ────────────────────────────────────────────
/// 종류가 아니라 <b>그 물건 하나</b>에 붙는다(ItemInstance). 강화 수치와 같은
/// 자리다. 같은 이름이어도 하나는 좋은 것, 하나는 나쁜 것일 수 있고, 그것을
/// 가려내는 것이 표식의 목적이기 때문이다.
///
/// 글자와 색만 다루므로 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public static class ItemMark
{
    /// <summary>화면에 보이는 모양 글자.</summary>
    public static string GlyphOf(MarkShape shape)
    {
        switch (shape)
        {
            case MarkShape.Circle: return "●";
            case MarkShape.Triangle: return "▲";
            case MarkShape.Square: return "■";
            default: return string.Empty;
        }
    }

    /// <summary>고를 때 보여 줄 모양 이름.</summary>
    public static string NameOf(MarkShape shape)
    {
        switch (shape)
        {
            case MarkShape.Circle: return "●  동그라미";
            case MarkShape.Triangle: return "▲  세모";
            case MarkShape.Square: return "■  네모";
            default: return "지운다";
        }
    }

    /// <summary>
    /// TextMeshPro 의 색 태그에 쓸 값.
    ///
    /// 어두운 배경 위에 얹히므로 원색 그대로 두지 않고 조금 밝게 잡았다.
    /// 특히 파랑·보라는 원색이면 배경에 묻혀 무슨 색인지 분간이 안 된다.
    /// </summary>
    public static string HexOf(MarkColor color)
    {
        switch (color)
        {
            case MarkColor.Red: return "#FF4A4A";
            case MarkColor.Orange: return "#FF9A3C";
            case MarkColor.Yellow: return "#FFE14A";
            case MarkColor.YellowGreen: return "#B6E356";
            case MarkColor.Green: return "#4ADE80";
            case MarkColor.SkyBlue: return "#5CC8FF";
            case MarkColor.Blue: return "#6E8BFF";
            case MarkColor.Purple: return "#C77DFF";
            default: return "#FFFFFF";
        }
    }

    /// <summary>고를 때 보여 줄 색 이름.</summary>
    public static string NameOf(MarkColor color)
    {
        switch (color)
        {
            case MarkColor.Red: return "빨강";
            case MarkColor.Orange: return "주황";
            case MarkColor.Yellow: return "노랑";
            case MarkColor.YellowGreen: return "연두";
            case MarkColor.Green: return "초록";
            case MarkColor.SkyBlue: return "하늘";
            case MarkColor.Blue: return "파랑";
            case MarkColor.Purple: return "보라";
            default: return string.Empty;
        }
    }

    /// <summary>고를 수 있는 것들. 화면에 보이는 순서다.</summary>
    public static readonly MarkShape[] Shapes =
    {
        MarkShape.Circle, MarkShape.Triangle, MarkShape.Square, MarkShape.None,
    };

    public static readonly MarkColor[] Colors =
    {
        MarkColor.Red, MarkColor.Orange, MarkColor.Yellow, MarkColor.YellowGreen,
        MarkColor.Green, MarkColor.SkyBlue, MarkColor.Blue, MarkColor.Purple,
    };

    // ── 끈적끈적 ───────────────────────────────────────────────────────────
    //
    // 사람이 붙이는 표식이 아니라 <b>게임이 붙이는 사실</b>이다. 그래서 모양도 색도
    // 고를 수 없고, 마킹의 여덟 색 어디와도 겹치지 않는 무채색을 쓴다.
    // "내가 적어 둔 것" 과 "물건에 실제로 일어난 일" 이 한눈에 갈려야 한다.

    /// <summary>끈적끈적한 아이템에 붙는 글자.</summary>
    public const string StickyGlyph = "※";

    /// <summary>
    /// 끈적끈적의 색. 마킹의 여덟 색과 겹치지 않는 흰색.
    ///
    /// 회색으로 두었더니 어두운 창 위에서 잘 보이지 않았다. 여덟 색이 모두
    /// 유채색이라 흰색이면 겹치지 않으면서 가장 또렷하다.
    /// </summary>
    public const string StickyHex = "#FFFFFF";

    /// <summary>끈적끈적한 아이템 이름 앞에 붙일 글자.</summary>
    public static string StickyPrefix() => $"<color={StickyHex}>{StickyGlyph}</color>";

    /// <summary>
    /// 아이템 이름 앞에 붙일 글자. 표식이 없으면 빈 문자열.
    ///
    /// 색은 TextMeshPro 의 태그로 입힌다. 태그가 표식에만 걸리도록 곧바로 닫으므로,
    /// 뒤따르는 아이템 이름은 평소 색 그대로 나온다.
    /// </summary>
    public static string Prefix(MarkShape shape, MarkColor color)
    {
        string glyph = GlyphOf(shape);
        if (glyph.Length == 0) return string.Empty;

        return $"<color={HexOf(color)}>{glyph}</color>";
    }
}
