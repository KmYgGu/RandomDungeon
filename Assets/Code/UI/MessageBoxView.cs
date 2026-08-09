using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 아래쪽 메시지 창과, 거기 딸린 예/아니오 선택창.
///
/// 예전에는 ShowDamagedTextBox 안에서 case 마다
/// "TextimageBox.enabled = true; panel.SetActive(true); 텍스트 대입;
///  RestartHidePanelCoroutine();" 네 줄이 16번 똑같이 반복됐다.
/// 창을 여닫는 방법은 이 클래스만 알면 된다.
/// </summary>
public class MessageBoxView
{
    /// <summary>메시지가 저절로 사라지기까지의 시간(초).</summary>
    private const float AutoHideDelay = 3f;

    // ── 중요한 알림이 최소한 떠 있어야 하는 시간 ──────────────────────────
    //
    // 뜨자마자 눌러서 넘길 수 있으면, 연달아 A 를 누르던 손에 그대로 지워진다.
    // 글이 길수록 오래 잡아 두되, 이야기 한 줄에 몇 초씩 묶이지 않도록 위를 막는다.

    /// <summary>글자 수와 상관없이 최소한 이만큼은 떠 있는다.</summary>
    private const float MinReadingSeconds = 0.5f;

    /// <summary>글자 하나마다 더해지는 시간. 초당 스물다섯 자쯤 읽는 셈이다.</summary>
    private const float SecondsPerCharacter = 0.04f;

    /// <summary>아무리 길어도 이보다 오래 붙잡지는 않는다.</summary>
    private const float MaxReadingSeconds = 2.5f;

    /// <summary>'▼' 가 켜졌다 꺼지는 주기의 절반(초).</summary>
    private const float BlinkSeconds = 0.45f;

    /// <summary>넘길 수 있다는 표시.</summary>
    private const string ContinueMark = "▼";

    // 코루틴을 돌려 줄 MonoBehaviour. 뷰 자신은 MonoBehaviour 가 아니다.
    private readonly MonoBehaviour runner;

    private readonly GameObject panel;
    private readonly Image frame;
    private readonly TextMeshProUGUI label;
    private readonly PanelView selectList;

    private Coroutine autoHideRoutine;

    /// <summary>"A 를 누르면 넘어간다" 를 알리는 표시. 처음 필요할 때 만든다.</summary>
    private TextMeshProUGUI continuePrompt;

    /// <summary>true 인 동안에는 자동으로 닫히지 않는다(선택창이 떠 있을 때 등).</summary>
    public bool KeepOpen { get; set; }

    public MessageBoxView(
        MonoBehaviour runner,
        GameObject panel,
        Image frame,
        TextMeshProUGUI label,
        PanelView selectList)
    {
        this.runner = runner;
        this.panel = panel;
        this.frame = frame;
        this.label = label;
        this.selectList = selectList;

        if (frame != null) frame.enabled = false;

        // 글자가 상자 밖으로 새지 않도록 코드에서 못을 박아 둔다.
        //
        // TMP 의 margin 은 음수를 넣으면 글자 영역이 상자 '밖으로' 넓어진다.
        // 이 창은 예전에 200x50 짜리였고 그때 넣어 둔 -263 이 남아 있어서,
        // 상자를 넓히자 글자가 화면을 뚫고 나갔다. 인스펙터에서 다시 건드려도
        // 어긋나지 않도록 여기서 정리한다.
        if (label != null)
        {
            label.margin = Vector4.zero;
            label.enableWordWrapping = true;

            // 긴 설명은 알아서 작아진다. 짧은 전투 메시지는 최대 크기 그대로.
            label.enableAutoSizing = true;
            label.fontSizeMin = MinFontSize;
            label.fontSizeMax = MaxFontSize;
        }
    }

    /// <summary>글자가 상자에 안 들어갈 때 줄어들 수 있는 한계.</summary>
    private const float MinFontSize = 16f;
    private const float MaxFontSize = 36f;

    /// <summary>
    /// 지나가는 메시지. 잠시 뒤 저절로 닫히고, 다음 메시지에 쉽게 덮인다.
    ///
    /// 중요한 알림이 떠 있는 동안에는 무시된다. 놓치면 안 되는 것을 띄워 놓고
    /// 전투 메시지가 그 위를 덮어 버리면 알림을 만든 뜻이 없어진다.
    /// </summary>
    public void Show(string message)
    {
        if (ShowingImportant) return;

        Open();
        if (label != null) label.text = message;
        RestartAutoHide();
    }

    /// <summary>지금 중요한 알림을 띄워 두고 확인을 기다리는 중인가.</summary>
    public bool ShowingImportant { get; private set; }

    /// <summary>
    /// 글을 읽는 데 걸린다고 보는 시간. 글자 수에 비례한다.
    ///
    /// 시간도 화면도 모르므로 Unity 없이 그대로 확인할 수 있다.
    /// </summary>
    public static float ReadingSeconds(string message)
    {
        int length = message != null ? message.Length : 0;
        return Mathf.Clamp(
            MinReadingSeconds + length * SecondsPerCharacter,
            MinReadingSeconds,
            MaxReadingSeconds);
    }

    /// <summary>
    /// 중요한 알림. 플레이어가 확인할 때까지 남는다.
    ///
    /// 레벨업처럼 놓치면 안 되는 것, 앞으로 붙을 대화 같은 것에 쓴다.
    /// 저절로 사라지지 않고 다른 메시지에 덮이지도 않는다.
    ///
    /// 뜨자마자는 넘길 수 없다. 공격하느라 A 를 연달아 누르던 손이 그대로
    /// 지워 버리기 때문이다. 글 길이만큼 기다린 뒤에야 A 가 먹는다.
    /// </summary>
    public IEnumerator ShowImportant(string message)
    {
        ShowingImportant = true;
        KeepOpen = true;
        StopAutoHide();

        Open();
        if (label != null) label.text = message;

        // 읽을 시간. 이 동안의 A 는 넘기는 데 쓰이지 않는다.
        // 표시도 아직 띄우지 않는다 — 눌러도 안 넘어가는데 누르라고 하면 거짓말이 된다.
        SetContinuePromptVisible(false);

        float remaining = ReadingSeconds(message);
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        // 읽는 동안 눌려 있던 A 가 곧바로 넘김으로 이어지지 않게 한 번 비운다.
        GameInput.ConsumeAction();

        // 이제부터 넘길 수 있다. 깜빡이는 표시로 알린다.
        float blink = 0f;
        while (!GameInput.ActionPressed)
        {
            blink += Time.deltaTime;
            SetContinuePromptVisible(blink % (BlinkSeconds * 2f) < BlinkSeconds);
            yield return null;
        }

        GameInput.ConsumeAction();
        SetContinuePromptVisible(false);

        ShowingImportant = false;
        KeepOpen = false;
        Hide();
    }

    /// <summary>자동으로 닫히지 않는 메시지를 띄운다(선택을 기다릴 때).</summary>
    public void ShowPersistent(string message)
    {
        Open();
        if (label != null) label.text = message;
    }

    public void Hide()
    {
        StopAutoHide();
        SetContinuePromptVisible(false);
        ShowingImportant = false;
        KeepOpen = false;
        if (panel != null) panel.SetActive(false);
        selectList.Hide();
    }

    /// <summary>
    /// 선택 패널에 아무 목록이나 띄운다.
    /// 계단 앞 "예/아니오" 와 상태창 메뉴가 같은 창을 나눠 쓴다.
    ///
    /// 이 목록은 메시지창이나 상태창과 나란히 뜨므로 화면 한가운데가 아니라
    /// 옆자리에 놓는다. 그래도 HUD 아래로 들어가는 것은 PanelLayout 이 지킨다.
    /// </summary>
    public void ShowSelectList(string text)
    {
        KeepOpen = true;
        StopAutoHide();

        selectList.ShowList(text, PanelView.SideAnchor);
    }

    /// <summary>
    /// 넘김 표시를 켜거나 끈다.
    ///
    /// 이 표시는 씬에 두지 않고 처음 필요할 때 만든다. 메시지 창 안쪽 아래 가운데에
    /// 붙이는 작은 글자 하나뿐이라, 씬 파일을 고쳐 인스펙터 연결을 늘리는 것보다
    /// 여기서 만들어 쓰는 편이 손이 덜 간다.
    /// </summary>
    private void SetContinuePromptVisible(bool visible)
    {
        if (continuePrompt == null)
        {
            if (!visible) return;          // 필요해질 때까지는 만들지 않는다
            continuePrompt = CreateContinuePrompt();
            if (continuePrompt == null) return;
        }

        continuePrompt.gameObject.SetActive(visible);
    }

    private TextMeshProUGUI CreateContinuePrompt()
    {
        if (panel == null || label == null) return null;

        var holder = new GameObject("ContinuePrompt");
        holder.transform.SetParent(panel.transform, worldPositionStays: false);

        var text = holder.AddComponent<TextMeshProUGUI>();

        // 글꼴은 메시지와 같은 것을 쓴다. 여기만 다른 글꼴이면 겉돌아 보인다.
        text.font = label.font;
        text.fontSize = PromptFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.text = ContinueMark;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(60f, 30f);
        rect.anchoredPosition = new Vector2(0f, 2f);

        // 굶주림 표시(연두 테)는 씬이 만들어질 때 글자를 모아 두므로,
        // 나중에 만든 이 글자는 스스로 끼어들어야 함께 물든다.
        StarvingTint.Register(text);

        holder.SetActive(false);
        return text;
    }

    /// <summary>넘김 표시의 글자 크기.</summary>
    private const float PromptFontSize = 24f;

    private void Open()
    {
        if (frame != null) frame.enabled = true;
        if (panel != null) panel.SetActive(true);
    }

    private void RestartAutoHide()
    {
        StopAutoHide();
        if (runner != null && runner.isActiveAndEnabled)
            autoHideRoutine = runner.StartCoroutine(AutoHideAfterDelay());
    }

    private void StopAutoHide()
    {
        if (autoHideRoutine == null) return;
        if (runner != null) runner.StopCoroutine(autoHideRoutine);
        autoHideRoutine = null;
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSeconds(AutoHideDelay);
        if (panel != null && !KeepOpen)
            panel.SetActive(false);
        autoHideRoutine = null;
    }

    /// <summary>
    /// 예/아니오를 묻고, 결정될 때까지 기다린다.
    /// 결과는 answer.Value 로 전달한다(코루틴은 값을 반환할 수 없기 때문).
    /// </summary>
    public IEnumerator AskYesNo(Choice answer)
    {
        KeepOpen = true;
        bool yesSelected = true;
        while (true)
        {
            if (GameInput.MenuStep < 0) yesSelected = true;
            else if (GameInput.MenuStep > 0) yesSelected = false;

            selectList.ShowList(yesSelected ? "> 예\n  아니오" : "  예\n> 아니오", PanelView.SideAnchor);

            if (GameInput.ActionPressed) break;

            yield return null;
        }

        KeepOpen = false;
        Hide();
        answer.Value = yesSelected;
    }

    /// <summary>코루틴 밖으로 결과를 들고 나오기 위한 상자.</summary>
    public class Choice
    {
        public bool Value;
    }
}
