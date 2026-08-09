using System;
using System.Collections;
using System.Text;

/// <summary>
/// 상태창의 메뉴 흐름.
///
/// 상태창을 열면 세 가지가 한꺼번에 뜬다.
///   상태 패널   - 장비 슬롯과 수치 (StatusView)
///   선택 패널   - 발밑 / 상태 / 도감 / 로그 / 기록 / 중단
///   메시지 패널 - 지금 가리키고 있는 항목의 설명
///
/// 메시지 패널과 선택 패널은 계단 앞 "예/아니오" 에 쓰던 것을 그대로 재사용한다.
/// 같은 화면 요소를 두 벌 만들 이유가 없고, 생김새도 저절로 통일된다.
///
/// 이 클래스는 고르는 일까지만 한다. 고른 뒤에 무엇을 할지는 GameManager 가 정한다.
/// </summary>
public class StatusMenuController
{
    private readonly MessageBoxView messageBox;

    private int selected;

    public StatusMenuController(MessageBoxView messageBox)
    {
        this.messageBox = messageBox;
    }

    /// <summary>
    /// 메뉴를 띄우고 고를 때까지 돈다.
    /// 취소하면 onChosen 에 null 이 간다.
    /// </summary>
    public IEnumerator Run(Action<StatusMenu?> onChosen)
    {
        selected = 0;

        while (true)
        {
            StatusMenu current = StatusMenuCatalog.Order[selected];

            messageBox.ShowSelectList(BuildMenuText());
            messageBox.ShowPersistent(StatusMenuCatalog.DescriptionOf(current));

            // 이 화면을 연 그 키가 곧바로 항목을 고르지 않도록 한 프레임 넘긴다.
            yield return null;

            if (GameInput.AssistPressed || GameInput.StatusPressed)
            {
                onChosen(null);
                yield break;
            }

            int step = GameInput.MenuStep;
            if (step != 0)
            {
                int count = StatusMenuCatalog.Order.Length;
                selected = (selected + step + count) % count;
            }

            if (GameInput.ActionPressed)
            {
                onChosen(StatusMenuCatalog.Order[selected]);
                yield break;
            }
        }
    }

    /// <summary>가리키고 있는 줄에만 커서를 붙인다.</summary>
    private string BuildMenuText()
    {
        var sb = new StringBuilder();

        for (int i = 0; i < StatusMenuCatalog.Order.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(i == selected ? "> " : "  ");
            sb.Append(StatusMenuCatalog.LabelOf(StatusMenuCatalog.Order[i]));
        }

        return sb.ToString();
    }
}
