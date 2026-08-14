using System;
using System.Collections;
using System.Collections.Generic;
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
            case StatusMenu.Records:
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
}
