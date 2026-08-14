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

    // ── 장착 중 ────────────────────────────────────────────────────────────
    //
    // 끈적끈적과 같은 갈래다 — 사람이 고르는 표식이 아니라 <b>게임이 붙이는 사실</b>이라
    // 색을 고를 수 없다. 그래서 색도 같은 무채색을 쓴다.
    //
    // 장비는 가방에서 빠지지 않고 "차고 있다" 는 표시만 붙는다(Equipment 주석).
    // 그 표시가 목록에 없으면 같은 이름의 장비가 둘일 때 어느 쪽을 차고 있는지
    // 알 길이 없고, 차고 있는 것을 던지거나 놓으려다 되돌리게 된다.

    /// <summary>장착 중인 장비에 붙는 글자.</summary>
    public const string EquippedGlyph = "E";

    /// <summary>장착 표시의 색. 끈적끈적과 같은 이유로 마킹의 여덟 색과 겹치지 않는 흰색.</summary>
    public const string EquippedHex = "#FFFFFF";

    /// <summary>장착 중인 장비 이름 앞에 붙일 글자.</summary>
    public static string EquippedPrefix() => $"<color={EquippedHex}>{EquippedGlyph}</color>";

    // ── 장비 자리별 글자 색 ────────────────────────────────────────────────
    //
    // 가방에서 <b>장비를 한눈에 가려내기</b> 위한 것이다. 이름만 늘어놓으면
    // 어느 것이 무기이고 어느 것이 소모품인지 한 줄씩 읽어야 알 수 있다.
    //
    // 표식과 달리 <b>사람이 고르지 않는다.</b> 시트의 equipslot 이 곧 색이라,
    // 새 장비를 표에 더하면 색은 저절로 따라온다.
    //
    // 색은 마킹과 같은 이유로 원색보다 조금 밝게 잡았다 — 어두운 창 위에
    // 얹히므로 원색이면 배경에 묻힌다.

    /// <summary>무기 이름의 색. 노랑.</summary>
    public const string WeaponHex = "#FFE14A";

    /// <summary>방어구 이름의 색. 주황.</summary>
    public const string ArmorHex = "#FF9A3C";

    /// <summary>장신구 이름의 색. 진한 분홍.</summary>
    public const string AccessoryHex = "#FF4FA3";

    /// <summary>그 자리의 이름에 입힐 색. 장비가 아니면 빈 문자열.</summary>
    public static string SlotHexOf(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: return WeaponHex;
            case EquipmentSlot.Armor: return ArmorHex;
            case EquipmentSlot.Accessory: return AccessoryHex;
            default: return string.Empty;
        }
    }

    /// <summary>
    /// 장비면 자리에 맞는 색을 입히고, 장비가 아니면 이름을 그대로 둔다.
    ///
    /// 태그가 <b>이름만</b> 감싼다. 앞에 붙는 표식(E · ※ · ●)은 저마다 제 색으로
    /// 이미 닫혀 있으므로, 이름을 감싸는 이 태그가 그것들까지 물들이지 않는다.
    /// </summary>
    public static string SlotColored(string name, EquipmentSlot slot)
    {
        string hex = SlotHexOf(slot);
        if (hex.Length == 0 || string.IsNullOrEmpty(name)) return name;

        return $"<color={hex}>{name}</color>";
    }

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
