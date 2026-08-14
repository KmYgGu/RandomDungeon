using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 상태창의 '그리기' 담당. 입력을 읽거나 상태를 판단하지 않는다.
/// 화면 아래 절반을 차지하며, 가방 화면과 달리 위쪽 던전은 계속 보인다.
/// </summary>
public class StatusView
{
    /// <summary>장비를 차고 있지 않은 슬롯에 쓸 문구.</summary>
    private const string EmptySlotMark = "―";

    // ── 강화 수치 라벨의 생김새 ────────────────────────────────────────────
    //
    // 인스펙터가 아니라 여기서 정한다. 씬을 고치려면 에디터를 닫아 달라고
    // 부탁해야 하고(HANDOVER 1절), 열어 둔 채로 .unity 를 고치면 저장하는
    // 순간 메모리의 옛 버전이 덮어쓴다. 슬롯이 셋이라 손으로 맞추면 어긋나기도 쉽다.
    //
    // <b>인스펙터에서 이 값들을 바꿔도 실행하면 여기 값으로 돌아온다.</b>
    // 생김새를 고칠 곳은 이 아래 넷이다.

    /// <summary>글자 크기. 씬에 있던 30 은 아이콘 위에서 잘 보이지 않았다.</summary>
    private const float EnhanceFontSize = 44f;

    /// <summary>슬롯 테두리에서 이만큼 띄운다. 0 이면 글자가 칸에 딱 붙어 답답하다.</summary>
    private const float EnhanceInset = 4f;

    /// <summary>
    /// 강화 수치의 색. 마킹 팔레트의 초록을 그대로 쓴다.
    ///
    /// 같은 값을 두 곳에 적어 두면 한쪽만 바뀐다. 그 초록은 어두운 창 위에서
    /// 읽히도록 원색보다 밝게 잡아 둔 것이라(ItemMark.HexOf) 여기 조건과 같다.
    /// </summary>
    private static readonly Color EnhanceColor = ParseHex(ItemMark.HexOf(MarkColor.Green));

    private static Color ParseHex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.green;

    private readonly GameObject panel;

    private readonly Image weaponIcon;
    private readonly TextMeshProUGUI weaponEnhance;
    private readonly Image armorIcon;
    private readonly TextMeshProUGUI armorEnhance;
    private readonly Image accessoryIcon;
    private readonly TextMeshProUGUI accessoryEnhance;

    private readonly TextMeshProUGUI leftColumn;
    private readonly TextMeshProUGUI rightColumn;

    public StatusView(
        GameObject panel,
        Image weaponIcon, TextMeshProUGUI weaponEnhance,
        Image armorIcon, TextMeshProUGUI armorEnhance,
        Image accessoryIcon, TextMeshProUGUI accessoryEnhance,
        TextMeshProUGUI leftColumn, TextMeshProUGUI rightColumn)
    {
        this.panel = panel;
        this.weaponIcon = weaponIcon;
        this.weaponEnhance = weaponEnhance;
        this.armorIcon = armorIcon;
        this.armorEnhance = armorEnhance;
        this.accessoryIcon = accessoryIcon;
        this.accessoryEnhance = accessoryEnhance;
        this.leftColumn = leftColumn;
        this.rightColumn = rightColumn;

        StyleEnhanceLabel(weaponEnhance);
        StyleEnhanceLabel(armorEnhance);
        StyleEnhanceLabel(accessoryEnhance);
    }

    /// <summary>
    /// 강화 수치를 슬롯의 <b>오른쪽 아래</b>에 크게 놓는다.
    ///
    /// 라벨을 슬롯 전체로 늘려 두고 글자만 오른쪽 아래로 붙인다. 띠를 따로
    /// 만들어 자리를 숫자로 잡으면 슬롯 크기를 바꿀 때마다 다시 맞춰야 하는데,
    /// 늘려 두면 슬롯이 커지든 작아지든 늘 그 모서리에 붙는다.
    ///
    /// 아이콘보다 뒤에 그려져야 글자가 그림에 가리지 않는다. 두 라벨 모두
    /// 아이콘 다음 형제라 지금은 그대로 위에 온다.
    /// </summary>
    private static void StyleEnhanceLabel(TextMeshProUGUI label)
    {
        if (label == null) return;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(EnhanceInset, EnhanceInset);     // 왼쪽 · 아래
        rect.offsetMax = new Vector2(-EnhanceInset, -EnhanceInset);   // 오른쪽 · 위

        label.alignment = TextAlignmentOptions.BottomRight;
        label.fontSize = EnhanceFontSize;

        // 자동 크기 조절이 켜져 있으면 위에서 정한 크기를 덮어쓴다.
        label.enableAutoSizing = false;
    }

    public void SetVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    /// <summary>
    /// 상태창을 다시 그린다.
    ///
    /// 여기서는 '이 화면에서만 볼 수 있는 것' 만 보여 준다.
    /// 체력·레벨·층은 이미 상단 HUD 에 늘 떠 있어 중복이고,
    /// 공격력·방어력은 슬롯의 강화 수치를 보면 대강 가늠이 되며,
    /// 던지기 사거리·위력은 숫자가 둘이라 오히려 헷갈린다.
    /// 남는 것은 만복도 · 경험치 · 힘 · 이번 층의 턴수 넷뿐이고,
    /// 그래서 글자를 크게 키울 수 있다.
    /// </summary>
    public void Render(PlayerStats stats, Equipment gear, int turnCount)
    {
        RenderSlot(weaponIcon, weaponEnhance, gear.Weapon);
        RenderSlot(armorIcon, armorEnhance, gear.Armor);
        RenderSlot(accessoryIcon, accessoryEnhance, gear.Accessory);

        if (leftColumn != null)
            leftColumn.text = $"힘      {stats.Strength}/{stats.MaxStrength}\n만복도  {stats.Hunger}/{stats.MaxHunger}";

        if (rightColumn != null)
            rightColumn.text = $"경험치  {stats.Exp}/{stats.ExpToNextLevel}\n턴      {turnCount}";
    }

    /// <summary>슬롯 하나. 장비 이미지와 그 위의 강화 수치.</summary>
    private static void RenderSlot(
        Image icon, TextMeshProUGUI enhanceLabel, GameObject item)
    {
        ItemDescription description = ItemInstance.DescriptionOf(item);

        if (icon != null)
        {
            icon.sprite = description != null ? description.sprites : null;
            icon.preserveAspect = true;
            // 빈 슬롯은 아이콘을 감춘다. 스프라이트가 null 이면 흰 사각형이 남기 때문이다.
            icon.enabled = icon.sprite != null;
        }

        if (enhanceLabel == null) return;

        if (item == null)
        {
            // 빈 자리 표시는 초록으로 두지 않는다. 강화 수치와 같은 색이면
            // "아무것도 안 찼다" 가 수치처럼 읽힌다.
            enhanceLabel.color = Color.white;
            enhanceLabel.text = EmptySlotMark;
            return;
        }

        int enhancement = ItemInstance.EnhancementOf(item);

        enhanceLabel.color = EnhanceColor;
        enhanceLabel.text = enhancement > 0 ? $"+{enhancement}" : string.Empty;
    }

}
