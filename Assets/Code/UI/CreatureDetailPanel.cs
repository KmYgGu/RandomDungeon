using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감의 상세 화면. 창 안쪽을 네 층으로 나눠 쓴다.
///
///   상단   - No. 와 이름
///   상중단 - 그림, 그 아래에 세어 온 값
///   중단   - 수치들 (얼마나 겪었는지에 따라 열린다)
///   하단   - 소개(explanation)
///
/// ── 적과 동료가 같은 화면을 쓴다 ──────────────────────────────────
/// 적은 아래 줄이 '쓰러뜨린 횟수' 이고 동료는 '호감도' 지만, 화면이 하는 일은
/// 똑같다 — 다섯 자리에 글자와 그림 하나를 놓는다. 그래서 <b>무엇을 보여 주는지는
/// 모르고</b> 다 만들어진 글자만 받는다. 시트를 아는 일은 부르는 쪽
/// (EnemyEncyclopediaReport · PartnerEncyclopediaReport)이 한다.
///
/// 이렇게 두면 도감 갈래가 하나 더 늘어도 이 파일은 그대로다.
///
/// ── 왜 씬이 아니라 코드로 만드는가 ────────────────────────────────
/// 아이템 설명 화면은 씬에 놓인 자식들(DetailImage·DetailName…)을 인스펙터로
/// 연결해 쓴다. 이 화면은 자리가 다섯이라 그만큼 연결을 늘려야 하는데,
/// 씬 파일은 Unity 를 켠 채로 고칠 수 없다(고치면 에디터가 저장할 때 덮어쓴다).
/// 그래서 메시지 창의 '▼' 표시가 그랬듯(MessageBoxView.CreateContinuePrompt)
/// 처음 필요할 때 코드가 만든다. 인스펙터 연결이 하나도 늘지 않는다.
/// </summary>
public class CreatureDetailPanel
{
    // 창 안에서 각 자리가 차지하는 세로 구간(0 = 아래, 1 = 위).
    // 숫자를 한자리에 모아 두어야 자리끼리 겹치거나 벌어진 것을 눈으로 잡을 수 있다.
    private static readonly Vector2 TitleBand = new Vector2(0.88f, 1.00f);
    private static readonly Vector2 ImageBand = new Vector2(0.60f, 0.87f);
    private static readonly Vector2 CounterBand = new Vector2(0.53f, 0.60f);
    private static readonly Vector2 StatsBand = new Vector2(0.21f, 0.52f);
    private static readonly Vector2 AboutBand = new Vector2(0.02f, 0.20f);

    /// <summary>그림이 놓이는 가로 구간. 가운데에 정사각으로 둔다.</summary>
    private static readonly Vector2 ImageSide = new Vector2(0.36f, 0.64f);

    /// <summary>글자가 놓이는 가로 구간. 창 테두리에 붙지 않게 안쪽으로 들인다.</summary>
    private static readonly Vector2 TextSide = new Vector2(0.07f, 0.93f);

    private const float TitleFontSize = 30f;
    private const float BodyFontSize = 22f;
    private const float AboutFontSize = 19f;

    /// <summary>글자가 줄어들 수 있는 한계. 이보다 작으면 읽을 수 없다.</summary>
    private const float MinFontSize = 12f;

    private readonly RectTransform parent;
    private readonly TMP_FontAsset font;

    private GameObject root;
    private Image portrait;
    private TextMeshProUGUI title;
    private TextMeshProUGUI counter;
    private TextMeshProUGUI stats;
    private TextMeshProUGUI about;

    /// <param name="parent">창 본체. 여기 아래에 자식으로 붙는다.</param>
    /// <param name="font">빌려 쓸 글꼴. null 이면 TMP 기본 글꼴이 된다.</param>
    public CreatureDetailPanel(RectTransform parent, TMP_FontAsset font)
    {
        this.parent = parent;
        this.font = font;
    }

    /// <summary>
    /// 보이거나 감춘다.
    ///
    /// 감출 때는 만들지 않는다. 이 화면을 한 번도 열지 않은 사람의 창에
    /// 빈 오브젝트 다섯이 매달려 있을 이유가 없다.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            if (root != null) root.SetActive(false);
            return;
        }

        Build();
        if (root != null) root.SetActive(true);
    }

    /// <summary>
    /// 내용을 채운다. 전부 다 만들어진 글자로 받는다.
    /// </summary>
    /// <param name="titleText">상단. "No.1  걸어다니는 시체"</param>
    /// <param name="sprite">그림. null 이면 그 자리를 비운다.</param>
    /// <param name="counterText">그림 아래. "쓰러뜨린 횟수 : 4" / "호감도 : 12"</param>
    /// <param name="statsText">중단. 줄바꿈으로 나뉜 여러 줄.</param>
    /// <param name="aboutText">하단. 소개 글.</param>
    public void Render(
        string titleText, Sprite sprite, string counterText, string statsText, string aboutText)
    {
        Build();
        if (root == null) return;

        title.text = titleText;
        counter.text = counterText;
        stats.text = statsText;
        about.text = aboutText;

        // 정의 애셋이 연결되지 않았으면 그림 자리를 비운다. 켜 두면 흰 사각형이 남는다
        // (PanelView.ShowDetail 이 같은 이유로 같은 일을 한다).
        portrait.sprite = sprite;
        portrait.enabled = sprite != null;
    }

    private void Build()
    {
        if (root != null || parent == null) return;

        root = new GameObject("CreatureDetail");
        root.transform.SetParent(parent, worldPositionStays: false);

        var rootRect = root.AddComponent<RectTransform>();
        Stretch(rootRect, Vector2.zero, Vector2.one);

        title = MakeText(rootRect, "Title", TextSide, TitleBand,
                         TitleFontSize, TextAlignmentOptions.Center, wrap: false);

        portrait = MakeImage(rootRect, "Portrait", ImageSide, ImageBand);

        counter = MakeText(rootRect, "Counter", TextSide, CounterBand,
                           BodyFontSize, TextAlignmentOptions.Center, wrap: false);

        // 수치는 왼쪽 정렬이어야 줄머리의 이름이 한 줄로 선다(PanelView 의 목록과 같은 이유).
        stats = MakeText(rootRect, "Stats", TextSide, StatsBand,
                         BodyFontSize, TextAlignmentOptions.TopLeft, wrap: false);

        about = MakeText(rootRect, "About", TextSide, AboutBand,
                         AboutFontSize, TextAlignmentOptions.TopLeft, wrap: true);
    }

    private TextMeshProUGUI MakeText(
        RectTransform holder, string name, Vector2 side, Vector2 band,
        float fontSize, TextAlignmentOptions alignment, bool wrap)
    {
        var go = new GameObject(name);
        go.transform.SetParent(holder, worldPositionStays: false);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableWordWrapping = wrap;
        text.margin = Vector4.zero;
        text.raycastTarget = false;

        // 글이 길면 알아서 줄어든다. 소개 글은 저마다 길이가 제각각이라
        // 고정 크기로 두면 어떤 줄에서는 창 밖으로 넘친다.
        text.enableAutoSizing = true;
        text.fontSizeMin = MinFontSize;
        text.fontSizeMax = fontSize;

        Stretch(text.rectTransform, new Vector2(side.x, band.x), new Vector2(side.y, band.y));

        // 굶주림 표시(연두 테)는 씬이 만들어질 때 글자를 모아 두므로,
        // 나중에 만든 글자는 스스로 끼어들어야 함께 물든다.
        StarvingTint.Register(text);

        return text;
    }

    private static Image MakeImage(RectTransform holder, string name, Vector2 side, Vector2 band)
    {
        var go = new GameObject(name);
        go.transform.SetParent(holder, worldPositionStays: false);

        var image = go.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        Stretch(image.rectTransform, new Vector2(side.x, band.x), new Vector2(side.y, band.y));
        return image;
    }

    /// <summary>부모 안에서 차지할 구간을 비율로 정한다.</summary>
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
