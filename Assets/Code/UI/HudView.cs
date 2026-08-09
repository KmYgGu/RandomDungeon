using TMPro;
using UnityEngine;

/// <summary>
/// 화면 상단의 상태 표시(체력바 / 층 / 레벨 / HP / 골드).
///
/// MonoBehaviour 가 아니라 순수 C# 클래스다. GameManager 가 Inspector 로 받아 둔
/// UI 참조를 생성자로 넘겨 주기 때문에, 씬에 붙어 있는 연결은 그대로 두면서도
/// "수치를 화면에 그리는 일" 을 GameManager 밖으로 뺄 수 있다.
/// </summary>
public class HudView
{
    // 체력바가 꽉 찼을 때의 픽셀 너비. 최대 체력이 20일 때를 기준으로 한다.
    private const float FullBarWidth = 60f;

    private readonly RectTransform healthBar;
    private readonly RectTransform maxHealthBar;
    private readonly TextMeshProUGUI stageText;
    private readonly TextMeshProUGUI levelText;
    private readonly TextMeshProUGUI hpText;
    private readonly TextMeshProUGUI goldText;

    public HudView(
        RectTransform healthBar,
        RectTransform maxHealthBar,
        TextMeshProUGUI stageText,
        TextMeshProUGUI levelText,
        TextMeshProUGUI hpText,
        TextMeshProUGUI goldText)
    {
        this.healthBar = healthBar;
        this.maxHealthBar = maxHealthBar;
        this.stageText = stageText;
        this.levelText = levelText;
        this.hpText = hpText;
        this.goldText = goldText;
    }

    public void Refresh(PlayerStats stats, int stage)
    {
        RefreshHealthBar(stats);

        if (stageText != null) stageText.text = $"B {stage}F";
        if (levelText != null) levelText.text = $"LV {stats.Level}";
        if (hpText != null) hpText.text = $"HP {stats.Hp}/{stats.MaxHp}";
        // 만복도는 여기 두지 않는다. 상태창(스페이스)에서 "만복도 n/N" 으로 볼 수 있고,
        // 정말 급할 때 — 굶주림이 한계에 닿았을 때 — 는 따로 알리는 방법을 붙일 예정이다.
        // 늘 떠 있는 숫자로는 어차피 그 순간을 놓친다.
        if (goldText != null) goldText.text = $"{stats.Gold}G";
    }

    private void RefreshHealthBar(PlayerStats stats)
    {
        if (maxHealthBar == null || healthBar == null) return;

        // 최대 체력이 늘어나면 바 자체가 길어진다.
        Vector2 maxSize = maxHealthBar.sizeDelta;
        maxSize.x = FullBarWidth * (stats.MaxHp / PlayerStats.InitialMaxHp);
        maxHealthBar.sizeDelta = maxSize;

        // 현재 체력은 그 길이에 대한 비율로 그린다.
        float ratio = stats.MaxHp > 0f ? stats.Hp / stats.MaxHp : 0f;
        Vector2 currentSize = maxSize;
        currentSize.x = maxSize.x * ratio;
        healthBar.sizeDelta = currentSize;
    }
}
