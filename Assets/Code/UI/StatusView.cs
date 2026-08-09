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
    public void Render(PlayerStats stats, Equipment gear, ItemCatalog catalog, int turnCount)
    {
        RenderSlot(weaponIcon, weaponEnhance, gear.Weapon, catalog);
        RenderSlot(armorIcon, armorEnhance, gear.Armor, catalog);
        RenderSlot(accessoryIcon, accessoryEnhance, gear.Accessory, catalog);

        if (leftColumn != null)
            leftColumn.text = $"힘      {stats.Strength}/{stats.MaxStrength}\n만복도  {stats.Hunger}/{stats.MaxHunger}";

        if (rightColumn != null)
            rightColumn.text = $"경험치  {stats.Exp}/{stats.ExpToNextLevel}\n턴      {turnCount}";
    }

    /// <summary>슬롯 하나. 장비 이미지와 그 위의 강화 수치.</summary>
    private static void RenderSlot(
        Image icon, TextMeshProUGUI enhanceLabel, GameObject item, ItemCatalog catalog)
    {
        ItemDescription description = item != null ? catalog.Find(item.name) : null;

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
            enhanceLabel.text = EmptySlotMark;
            return;
        }

        int enhancement = ItemInstance.EnhancementOf(item);
        enhanceLabel.text = enhancement > 0 ? $"+{enhancement}" : string.Empty;
    }

}
