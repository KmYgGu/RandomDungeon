using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 캐릭터 상태창과 그 안의 메뉴 — 상태 · 로그 · (앞으로) 도감 · 기록 · 중단.
///
/// ── 왜 GameManager 에서 떼어 냈나 ─────────────────────────────────
/// 메뉴가 늘어날 자리다. 도감 · 기록 · 중단 셋이 아직 비어 있고, 늘릴 때마다
/// 갓 오브젝트를 열게 하고 싶지 않았다. 셋 다 ShowScrollingPage 를 그대로
/// 쓰면 되므로, 그 흐름이 한 파일에 모여 있어야 다음 사람이 찾는다.
///
/// <b>MonoBehaviour 가 아니다.</b> 씬의 Inspector 연결을 건드리지 않는다.
/// </summary>
public class StatusScreen
{
    private readonly GameManager game;
    private readonly MessageBoxView messageBox;
    private readonly PanelView menuPanel;
    private readonly StatusView statusView;

    public StatusScreen(
        GameManager game, MessageBoxView messageBox, PanelView menuPanel, StatusView statusView)
    {
        this.game = game;
        this.messageBox = messageBox;
        this.menuPanel = menuPanel;
        this.statusView = statusView;
    }


    /// <summary>
    /// 캐릭터 상태창을 연다. 열려 있는 동안에는 조작이 잠기고,
    /// 아무 키(A 또는 B)나 상태창 키를 다시 누르면 닫힌다.
    ///
    /// 가방과 달리 턴을 쓰지 않는다. 그냥 들여다보는 화면이다.
    /// </summary>
    public void OpenStatus()
    {
        InputFocus.Give(InputOwner.Status);
        game.StartCoroutine(ShowStatusUntilClosed());
    }

    private IEnumerator ShowStatusUntilClosed()
    {
        statusView.SetVisible(true);
        statusView.Render(GameManager.Stats, GameManager.Gear, GameManager.TurnCount);

        // 메뉴가 열려 있는 동안 턴은 흐르지 않으므로 수치는 한 번만 그리면 된다.
        StatusMenu? chosen = null;
        yield return new StatusMenuController(messageBox).Run(result => chosen = result);

        statusView.SetVisible(false);
        menuPanel.Hide();
        messageBox.Hide();
        InputFocus.ReturnToPlayer();

        if (chosen.HasValue)
            HandleStatusMenu(chosen.Value);
    }

    /// <summary>
    /// 상태창 메뉴에서 고른 것을 처리한다.
    ///
    /// 여섯 항목 모두 앞으로 만들 기능이라, 지금은 자리만 잡아 두었다.
    /// 기능이 생기면 해당 case 한 줄만 바꾸면 된다.
    /// </summary>
    private void HandleStatusMenu(StatusMenu menu)
    {
        switch (menu)
        {
            case StatusMenu.UnderFoot:
                game.UseUnderFoot();
                break;

            case StatusMenu.Condition:
                game.StartCoroutine(ShowConditions());
                break;

            case StatusMenu.Log:
                game.StartCoroutine(ShowLog());
                break;

            case StatusMenu.Encyclopedia:
                game.StartCoroutine(ShowEncyclopedia());
                break;

            case StatusMenu.Records:
                game.StartCoroutine(ShowRecords());
                break;

            case StatusMenu.Abort:
                messageBox.Show($"{StatusMenuCatalog.LabelOf(menu)}은(는) 아직 준비되지 않았다.");
                break;
        }
    }

    /// <summary>
    /// 여러 줄을 큰 판에 늘어놓고 상하 방향키로 넘겨 보는 화면.
    /// 상태와 로그가 같은 흐름을 쓴다.
    ///
    /// 판에 몇 줄이 들어가는지는 글꼴과 화면 비율에 달렸으므로 매 프레임 다시
    /// 물어본다 — 창 크기가 바뀌어도 스크롤이 어긋나지 않는다.
    /// 반대로 줄 수는 열 때 한 번만 받는다. 이 화면이 떠 있는 동안에는 턴이
    /// 흐르지 않아 내용이 늘거나 줄지 않기 때문이다.
    /// </summary>
    /// <param name="lineCount">늘어놓을 줄 수. 스크롤이 넘어갈 범위를 정한다.</param>
    /// <param name="buildText">(시작 줄, 담을 줄 수) → 판에 채울 글자.</param>
    /// <param name="startOffset">처음 보여 줄 자리. int.MaxValue 면 맨 끝에서 시작한다.</param>
    private IEnumerator ShowScrollingPage(
        int lineCount, Func<int, int, string> buildText, int startOffset)
    {
        InputFocus.Give(InputOwner.Prompt);

        int offset = startOffset;

        while (true)
        {
            int capacity = menuPanel.PageLineCapacity;
            offset = PageScroll.ClampOffset(offset, lineCount, capacity);

            menuPanel.ShowPage(buildText(offset, capacity));

            // 이 화면을 연 그 키가 곧바로 닫아 버리지 않도록 한 프레임 넘긴다.
            yield return null;

            if (GameInput.AssistPressed || GameInput.StatusPressed) break;

            offset += GameInput.MenuStep;
        }

        menuPanel.Hide();
        InputFocus.ReturnToPlayer();
    }

    /// <summary>상태 화면. 지금 걸려 있는 것들을 위에서부터 늘어놓는다.</summary>
    private IEnumerator ShowConditions()
    {
        List<ConditionDefinition> active = ConditionReport.Active(GameManager.Stats, GameManager.Conditions);

        return ShowScrollingPage(
            active.Count,
            (offset, capacity) => ConditionReport.BuildText(active, offset, capacity),
            startOffset: 0);
    }

    /// <summary>
    /// 로그 화면. 메시지 창을 지나간 글을 오래된 것부터 늘어놓는다.
    ///
    /// 맨 끝에서 시작한다. 이 화면을 여는 이유는 대개 "방금 무엇에 맞았지" 라서,
    /// 열자마자 직전 몇 줄이 보여야 한다. 위로 올릴수록 과거로 간다.
    /// </summary>
    private IEnumerator ShowLog()
    {
        return ShowScrollingPage(GameManager.Log.Count, GameManager.Log.BuildText, startOffset: int.MaxValue);
    }

    /// <summary>
    /// 기록 화면. 지금까지 쌓인 수치를 위에서부터 늘어놓는다.
    ///
    /// <b>이번 판의 점수는 여기 없다.</b> 점수는 죽거나 클리어하는 순간에만
    /// 매겨지므로(RunScore 주석), 도중에 열면 지난 판까지의 것만 보인다.
    ///
    /// 한 번도 없었던 기록은 물음표로 뜬다(RecordBook 주석). 항목이 판보다 많으면
    /// 로그·상태 화면과 같은 방식으로 위아래로 밀어 본다 — 도감과 달리 고를 것이
    /// 없는 화면이라, 페이지가 아니라 두루마리 쪽이 맞다.
    /// </summary>
    private IEnumerator ShowRecords()
    {
        RecordBook records = GameManager.Records;

        return ShowScrollingPage(records.LineCount, records.BuildText, startOffset: 0);
    }

    // ── 도감 ───────────────────────────────────────────────────────────────

    /// <summary>도감에서 고를 수 있는 갈래. 화면에 보이는 순서다.</summary>
    private enum EncyclopediaCategory { Item, Enemy, Partner }

    private static readonly EncyclopediaCategory[] EncyclopediaOrder =
    {
        EncyclopediaCategory.Item, EncyclopediaCategory.Enemy, EncyclopediaCategory.Partner,
    };

    private static string LabelOf(EncyclopediaCategory category)
    {
        switch (category)
        {
            case EncyclopediaCategory.Item: return "아이템";
            case EncyclopediaCategory.Enemy: return "적";
            case EncyclopediaCategory.Partner: return "동료";
            default: return string.Empty;
        }
    }

    /// <summary>
    /// 도감 갈래를 고른다. 적·동료 도감은 아직 없어서 고르면 그렇다고만 알린다.
    ///
    /// 아이템 도감에서 B 로 물러나면 다시 이 갈래 선택으로 돌아온다 — 완전히
    /// 닫는 것은 <b>이 화면</b>에서 B 를 눌렀을 때뿐이다. InputFocus 도 그래서
    /// 여기서만 잡고 놓는다. ShowItemEncyclopedia 는 물려받은 자리를 그대로 쓴다.
    /// </summary>
    private IEnumerator ShowEncyclopedia()
    {
        InputFocus.Give(InputOwner.Prompt);

        int selected = 0;

        while (true)
        {
            EncyclopediaCategory? chosen = null;
            bool closed = false;

            while (true)
            {
                messageBox.ShowSelectList(BuildCategoryText(selected));

                // 이 화면을 연 그 키(또는 아이템 도감에서 물러난 그 키)가 곧바로
                // 항목을 고르지 않도록 한 프레임 넘긴다.
                yield return null;

                if (GameInput.AssistPressed)
                {
                    GameInput.ConsumeAssist();
                    closed = true;
                    break;
                }

                int step = GameInput.MenuStep;
                if (step != 0)
                    selected = (selected + step + EncyclopediaOrder.Length) % EncyclopediaOrder.Length;

                if (GameInput.ActionPressed)
                {
                    chosen = EncyclopediaOrder[selected];
                    break;
                }
            }

            messageBox.Hide();

            if (closed) break;

            if (chosen == EncyclopediaCategory.Item)
            {
                yield return ShowItemEncyclopedia();
            }
            else if (chosen == EncyclopediaCategory.Enemy)
            {
                yield return ShowEnemyEncyclopedia();
            }
            else if (chosen == EncyclopediaCategory.Partner)
            {
                yield return ShowPartnerEncyclopedia();
            }
        }

        InputFocus.ReturnToPlayer();
    }

    private static string BuildCategoryText(int selected)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < EncyclopediaOrder.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(i == selected ? "> " : "  ");
            sb.Append(LabelOf(EncyclopediaOrder[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 도감 목록을 페이지로 넘겨 보는 화면. 갈래 셋이 이것 하나를 나눠 쓴다.
    ///
    ///   위/아래 : <b>이 페이지 안에서만</b> 커서를 옮긴다(끝에서 감싸 돈다)
    ///   좌/우   : 다음·이전 페이지. 커서는 새 페이지의 첫 줄로 간다
    ///   A       : 이미 아는 것이면 상세를 연다
    ///   B       : 갈래 선택으로 돌아간다
    ///
    /// ── 왜 스크롤이 아니라 페이지인가 ─────────────────────────────
    /// 한때 위/아래가 커서를 좇아 화면을 밀었다(스크롤). 그러면 좌/우로 넘긴
    /// 페이지 경계가 곧 흐트러져서 "지금 몇 페이지인가" 가 사라지고, 목록 전체를
    /// 위/아래만으로도 지나갈 수 있게 되어 좌/우를 둔 뜻이 없어진다.
    /// 지금은 한 판이 한 페이지고, 페이지는 좌/우로만 넘어간다(PageScroll).
    ///
    /// ── 셋을 한 곳에 모은 이유 ────────────────────────────────────
    /// 갈래마다 이 반복문을 따로 두었더니 서른 줄짜리가 셋이 되었고, 넘기는 규칙을
    /// 한 곳만 고치면 같은 창인데 손맛이 갈렸다. 다른 것은 <b>글자를 어떻게 만들고
    /// 무엇을 열 수 있는가</b>뿐이라 그 둘만 받는다.
    ///
    /// InputFocus 는 건드리지 않는다 — 부르는 쪽(ShowEncyclopedia)이 갈래 선택부터
    /// 쭉 잡고 있고, 여기서 B 로 물러나면 그 화면으로 돌아갈 뿐 손을 놓는 것이 아니다.
    /// </summary>
    /// <param name="count">목록에 놓인 줄 수.</param>
    /// <param name="buildText">(고른 줄, 페이지 첫 줄, 한 페이지 줄 수) → 판에 채울 글자.</param>
    /// <param name="canOpen">그 줄의 상세를 열 수 있는가(아직 모르는 것은 못 연다).</param>
    /// <param name="openDetail">그 줄의 상세를 여는 코루틴.</param>
    private IEnumerator BrowseEncyclopedia(
        int count,
        Func<int, int, int, string> buildText,
        Func<int, bool> canOpen,
        Func<int, IEnumerator> openDetail)
    {
        int selected = 0;
        int page = 0;

        while (true)
        {
            // 판에 몇 줄이 들어가는지는 글꼴과 화면 비율에 달렸으므로 매 프레임 다시
            // 물어본다(ShowScrollingPage 와 같은 이유). 그래서 페이지 계산도 매번 한다.
            int capacity = menuPanel.PageLineCapacity;
            page = PageScroll.ClampPage(page, count, capacity);

            int start = page * capacity;

            // 창 크기가 바뀌어 커서가 이 페이지 밖으로 나갔으면 첫 줄로 되돌린다.
            if (selected < start || selected >= start + PageScroll.RowsOnPage(page, count, capacity))
                selected = start;

            menuPanel.ShowPage(buildText(selected, start, capacity));

            yield return null;

            if (GameInput.AssistPressed)
            {
                GameInput.ConsumeAssist();
                break;
            }

            int step = GameInput.MenuStep;
            if (step != 0)
                selected = PageScroll.StepWithinPage(selected, step, page, count, capacity);

            // 끝 페이지에서 더 넘기면 ClampPage 가 붙잡아 두므로 아무 일도 없다.
            int pageSwitch = GameInput.MenuSwitch;
            if (pageSwitch != 0)
            {
                int nextPage = PageScroll.ClampPage(page + pageSwitch, count, capacity);
                if (nextPage != page)
                {
                    page = nextPage;
                    selected = page * capacity;   // 새 페이지의 첫 줄
                }
            }

            // 아직 모르는 것은 골라도 아무 일이 없다 — 보여 줄 정보가 없다.
            if (GameInput.ActionPressed && count > 0 && canOpen(selected))
                yield return openDetail(selected);
        }

        menuPanel.Hide();
    }

    /// <summary>
    /// 아이템 도감. 시트에 있는 아이템 전부를 번호(id)가 작은 순서로 늘어놓는다.
    /// 아직 주워 본 적이 없으면 물음표로 가려진다.
    /// </summary>
    private IEnumerator ShowItemEncyclopedia()
    {
        List<ItemDefinition> sorted = ItemEncyclopediaReport.SortedEntries(ItemCatalog.All);

        return BrowseEncyclopedia(
            sorted.Count,
            (selected, start, capacity) => ItemEncyclopediaReport.BuildText(
                sorted, GameManager.ItemDex, selected, start, capacity),
            index => GameManager.ItemDex.IsDiscovered(sorted[index].Id),
            index => ShowItemDetail(sorted[index].Id));
    }

    /// <summary>
    /// 적 도감. 한 번도 못 쓰러뜨린 적은 물음표로 가려진다.
    /// </summary>
    private IEnumerator ShowEnemyEncyclopedia()
    {
        List<MonsterDefinition> sorted = EnemyEncyclopediaReport.SortedEntries(MonsterCatalog.All);

        return BrowseEncyclopedia(
            sorted.Count,
            (selected, start, capacity) => EnemyEncyclopediaReport.BuildListText(
                sorted, GameManager.EnemyDex, selected, start, capacity),
            index => GameManager.EnemyDex.IsDiscovered(sorted[index].Id),
            index => ShowEnemyDetail(sorted[index], index + 1));
    }

    /// <summary>
    /// 적 하나의 상세. 목록과 달리 창 안을 네 층으로 나눠 쓴다(EnemyDetailPanel).
    ///
    /// 수치·소개는 시트에서 바로 나오고, 정의 애셋은 그림 하나 때문에 찾는다.
    /// 못 찾아도 그림 자리만 비운 채 뜬다 — RandomDung 의 Monster Descriptions 가
    /// 아직 안 채워져 있으면 그렇게 된다.
    /// </summary>
    private IEnumerator ShowEnemyDetail(MonsterDefinition definition, int listNumber)
    {
        MonsterDescription description = game.FindMonsterDescription(definition.Id);
        int defeats = GameManager.EnemyDex.DefeatsOf(definition.Id);

        menuPanel.ShowCreatureDetail(
            EnemyEncyclopediaReport.BuildTitle(listNumber, definition),
            description != null ? description.sprite : null,
            EnemyEncyclopediaReport.BuildDefeatCount(defeats),
            EnemyEncyclopediaReport.BuildStatsText(definition, defeats),
            definition.Description);

        return WaitForDetailClose();
    }

    /// <summary>
    /// 상세 창을 B 로 닫을 때까지 기다린다. 적·동료 도감이 같은 것을 쓴다.
    /// </summary>
    private IEnumerator WaitForDetailClose()
    {
        do
        {
            yield return null;
        }
        while (!GameInput.AssistPressed);

        // 설명을 닫은 B 가 뗄 때 목록까지 닫지 않도록 소비한다(아이템 도감과 같다).
        GameInput.ConsumeAssist();
    }

    /// <summary>
    /// 동료 도감. 한 번도 동행해 보지 않은 동료는 물음표로 가려진다.
    /// </summary>
    private IEnumerator ShowPartnerEncyclopedia()
    {
        List<PartnerDefinition> sorted = PartnerEncyclopediaReport.SortedEntries(PartnerCatalog.All);

        return BrowseEncyclopedia(
            sorted.Count,
            (selected, start, capacity) => PartnerEncyclopediaReport.BuildListText(
                sorted, GameManager.PartnerDex, selected, start, capacity),
            index => GameManager.PartnerDex.IsDiscovered(sorted[index].Id),
            index => ShowPartnerDetail(sorted[index], index + 1));
    }

    /// <summary>
    /// 동료 하나의 상세. 적 도감과 같은 창을 쓰되, 아래 줄이 쓰러뜨린 횟수가 아니라
    /// 호감도다. 호감도는 아직 오르지 않는다(PartnerEncyclopedia 주석).
    /// </summary>
    private IEnumerator ShowPartnerDetail(PartnerDefinition definition, int listNumber)
    {
        PartnerDescription description = game.FindPartnerDescription(definition.Id);
        int affinity = GameManager.PartnerDex.AffinityOf(definition.Id);

        menuPanel.ShowCreatureDetail(
            PartnerEncyclopediaReport.BuildTitle(listNumber, definition),
            description != null ? description.sprite : null,
            PartnerEncyclopediaReport.BuildAffinity(affinity),
            PartnerEncyclopediaReport.BuildStatsText(definition, affinity),
            definition.Description);

        return WaitForDetailClose();
    }

    /// <summary>
    /// 아이템 하나의 정보. 가방의 '확인한다' 와 같은 창을 쓴다(PanelView.ShowDetail).
    /// </summary>
    private IEnumerator ShowItemDetail(int itemId)
    {
        ItemDescription description = game.FindItemDescription(itemId);
        if (description == null) yield break;

        menuPanel.ShowDetail(description);

        do
        {
            yield return null;
        }
        while (!GameInput.AssistPressed);

        // 설명을 닫은 B 가 뗄 때 목록까지 닫지 않도록 소비한다(BagMenuController.ShowDetail 과 같다).
        GameInput.ConsumeAssist();
    }
}
