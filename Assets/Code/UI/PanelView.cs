using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 안의 창을 하나로 합친 것. 목록이든 메뉴든 아이템 설명이든 이 창 하나가 맡는다.
///
/// 예전에는 하는 일이 거의 같은 패널이 넷이었다.
///   ItemPanel              - 가방 목록 (글자 + 커서 열이 따로)
///   SelectPanel203.199-25  - 행동 메뉴 (먹는다 / 확인한다 / 둔다 / 던진다)
///   itemexplainPanel       - 아이템 설명 (그림 + 이름 + 가격 + 본문)
///   AnswerPanel            - 계단 앞 예 / 아니오
/// 넷 다 "상자 하나에 글자를 넣어 보여 준다" 는 점에서 같은데, 크기와 자리를
/// 저마다 손으로 맞춰 두어서 하나를 고치면 나머지와 어긋났다.
///
/// 이제 창은 하나뿐이고, 무엇을 보여 줄지에 따라 모습만 바꾼다.
///   목록 모드 - 글자만 보여 주고 글자에 맞춰 상자가 줄었다 늘었다 한다
///   설명 모드 - 그림·이름·가격·본문을 켜고 쓸 수 있는 영역을 통째로 쓴다
///
/// 커서는 글자 안에 "&gt; " 로 넣는다. 예전에는 가방 목록만 커서 열을 따로 두고
/// 나머지는 글자 안에 넣어서, 같은 목록인데 만드는 방법이 둘이었다.
///
/// 크기와 자리를 정하는 계산은 PanelLayout 이 한다. 그래서 "HUD 를 가리지 않는다"
/// 는 규칙을 Unity 없이도 검사할 수 있다.
/// </summary>
public class PanelView
{
    /// <summary>목록 모드에서 글자 둘레에 남길 여백. 자식들의 안쪽 여백과 맞춰 둔 값이다.</summary>
    private static readonly Vector2 ListPadding = new Vector2(56f, 32f);

    /// <summary>설명 모드처럼 "쓸 수 있는 만큼 다 쓴다" 를 나타내는 값.</summary>
    private static readonly Vector2 FillAvailable = new Vector2(10000f, 10000f);

    /// <summary>가방처럼 화면을 차지해도 되는 것이 놓이는 자리.</summary>
    public static readonly Vector2 CenterAnchor = new Vector2(0f, -40f);

    /// <summary>
    /// 상태창이나 메시지창 곁에 붙는 작은 목록이 놓이는 자리.
    ///
    /// 세로를 HudBottom 으로 주는 것은 "가운데를 거기 두라" 는 뜻이 아니라
    /// 위쪽에 붙이라는 뜻이다. PanelLayout 이 창 윗변을 그 선 아래로 눌러 주므로,
    /// 요청한 자리가 한계보다 위이면 결과는 언제나 '윗변이 한계에 딱 붙은' 배치가 된다.
    ///
    /// 덕분에 항목이 둘이든 여섯이든 목록의 윗변이 늘 같은 높이에 선다.
    /// 상태창(StatusPanel)의 윗변도 같은 선에 맞춰 두었기 때문에 둘이 나란해진다.
    /// </summary>
    public static readonly Vector2 SideAnchor = new Vector2(250f, PanelLayout.HudBottom);

    private readonly GameObject panel;
    private readonly RectTransform panelRect;

    private readonly TextMeshProUGUI listLabel;

    private readonly Image detailImage;
    private readonly TextMeshProUGUI detailName;
    private readonly TextMeshProUGUI detailPrice;
    private readonly TextMeshProUGUI detailDescription;

    public PanelView(
        GameObject panel,
        TextMeshProUGUI listLabel,
        Image detailImage,
        TextMeshProUGUI detailName,
        TextMeshProUGUI detailPrice,
        TextMeshProUGUI detailDescription)
    {
        this.panel = panel;
        this.listLabel = listLabel;
        this.detailImage = detailImage;
        this.detailName = detailName;
        this.detailPrice = detailPrice;
        this.detailDescription = detailDescription;

        panelRect = panel != null ? panel.GetComponent<RectTransform>() : null;

        if (listLabel != null)
        {
            // margin 이 0 이어야 GetPreferredValues 로 잰 크기와 실제 글자가 맞는다.
            listLabel.margin = Vector4.zero;

            // 목록은 한 줄에 하나씩이어야 한다. 접히면 커서와 줄이 어긋난다.
            listLabel.enableWordWrapping = false;

            // 왼쪽 정렬이어야 줄머리의 "> " 가 한 줄로 선다. 가운데 정렬이면
            // 글자 수가 다른 줄마다 커서 위치가 제각각으로 흔들린다.
            // 씬에 저장된 정렬값은 예전 TMP 형식이라 그대로 믿지 않고 여기서 정한다.
            listLabel.alignment = TextAlignmentOptions.TopLeft;
        }
    }

    public bool IsVisible => panel != null && panel.activeSelf;

    /// <summary>목록이나 메뉴를 띄운다. 글자가 딱 들어갈 만큼으로 창을 맞춘다.</summary>
    public void ShowList(string text) => ShowList(text, CenterAnchor);

    public void ShowList(string text, Vector2 preferredCenter)
    {
        if (panel == null) return;

        SetDetailActive(false);
        if (listLabel != null) listLabel.gameObject.SetActive(true);

        panel.SetActive(true);
        if (listLabel == null) return;

        listLabel.text = text;

        // TMP 가 계산해 주는 '이 글자가 차지할 크기' 를 쓴다.
        // 줄 수를 직접 세어 곱하는 것보다 정확하다(줄간격·글꼴이 바뀌어도 따라온다).
        Place(listLabel.GetPreferredValues(text), ListPadding, preferredCenter);
    }

    /// <summary>
    /// 아이템 설명을 띄운다.
    ///
    /// 그림·이름·가격이 창 안의 정해진 자리에 놓여 있어서 글자량에 따라 창을
    /// 줄이면 배치가 어그러진다. 그래서 설명은 쓸 수 있는 영역을 통째로 쓴다.
    /// 내용이 많은 화면이라 그렇게 해도 빈 상자로 보이지 않는다.
    /// </summary>
    public void ShowDetail(ItemDescription item)
    {
        if (panel == null) return;

        if (listLabel != null) listLabel.gameObject.SetActive(false);
        SetDetailActive(true);
        panel.SetActive(true);

        if (item != null)
        {
            if (detailImage != null)
            {
                detailImage.sprite = item.sprites;
                detailImage.preserveAspect = true;
                // 스프라이트가 없으면 흰 사각형이 남는다.
                detailImage.enabled = detailImage.sprite != null;
            }
            if (detailName != null) detailName.text = item.itemName;
            if (detailPrice != null) detailPrice.text = $"판매 가격 : {item.SellPrice}G";
            if (detailDescription != null) detailDescription.text = item.description;
        }

        Place(FillAvailable, Vector2.zero, CenterAnchor);
    }

    /// <summary>
    /// 글자만 담은 큰 판. 쓸 수 있는 영역을 통째로 쓴다.
    ///
    /// 목록 모드와 달리 글자량에 따라 줄이지 않는다. 여러 줄을 스크롤해 가며
    /// 읽는 화면이라, 줄 수가 바뀔 때마다 판이 들썩이면 읽기 어렵기 때문이다.
    /// </summary>
    public void ShowPage(string text)
    {
        if (panel == null) return;

        SetDetailActive(false);
        if (listLabel != null) listLabel.gameObject.SetActive(true);

        panel.SetActive(true);
        if (listLabel == null) return;

        listLabel.text = text;
        Place(FillAvailable, Vector2.zero, CenterAnchor);
    }

    /// <summary>
    /// 큰 판에 몇 줄까지 들어가는가.
    ///
    /// 글자 크기와 화면 비율에 따라 달라지므로 숫자를 박아 두지 않고 그때그때 잰다.
    /// 스크롤이 필요한지, 한 번에 몇 줄을 보여 줄지가 이 값으로 정해진다.
    /// </summary>
    public int PageLineCapacity
    {
        get
        {
            if (listLabel == null) return 1;

            // 한 줄 높이는 글꼴이 정한다. 줄간격이 바뀌어도 따라온다.
            float lineHeight = listLabel.GetPreferredValues("가").y;
            if (lineHeight <= 0f) return 1;

            float height = PanelLayout
                .Fit(FillAvailable, Vector2.zero, CanvasSize(), CenterAnchor).Size.y;

            return Mathf.Max(1, Mathf.FloorToInt((height - ListPadding.y) / lineHeight));
        }
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Place(Vector2 contentSize, Vector2 padding, Vector2 preferredCenter)
    {
        if (panelRect == null) return;

        PanelLayout.Placement placement =
            PanelLayout.Fit(contentSize, padding, CanvasSize(), preferredCenter);

        // 씬에 어떤 앵커가 저장돼 있든 계산과 어긋나지 않도록 한가운데 기준으로 맞춘다.
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        panelRect.sizeDelta = placement.Size;
        panelRect.anchoredPosition = placement.Center;
    }

    /// <summary>캔버스의 크기. 화면 비율이 바뀌면 세로가 달라지므로 그때그때 읽는다.</summary>
    private Vector2 CanvasSize()
    {
        var parent = panelRect != null ? panelRect.parent as RectTransform : null;
        return parent != null ? parent.rect.size : new Vector2(800f, 450f);
    }

    private void SetDetailActive(bool visible)
    {
        if (detailImage != null) detailImage.gameObject.SetActive(visible);
        if (detailName != null) detailName.gameObject.SetActive(visible);
        if (detailPrice != null) detailPrice.gameObject.SetActive(visible);
        if (detailDescription != null) detailDescription.gameObject.SetActive(visible);
    }
}
