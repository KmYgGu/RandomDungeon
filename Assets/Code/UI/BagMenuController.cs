using System;
using System.Collections;
using System.Text;
using UnityEngine;

/// <summary>
/// 가방 화면과 발밑 화면의 흐름을 담당한다.
///
/// 예전에는 bagTextBox / MakeBagCursor / MenuCursor / WaitForCloseExplainPanel 이
/// 서로를 StartCoroutine 으로 불러 대는 구조였다. 코루틴 여러 개가 동시에 살아
/// 같은 입력을 읽는 일이 생겼고, isMenuActive / isBagOpen / PlayerTextCheck 같은
/// 플래그가 어느 코루틴 소유인지 알기 어려웠다.
///
/// 여기서는 하나의 코루틴이 상태를 순서대로 지나간다. 어느 시점에 어떤 입력을
/// 받는지가 코드를 위에서 아래로 읽으면 그대로 보인다.
///
/// ── 두 화면을 한 곳에서 다루는 이유 ───────────────────────────────
/// 가방과 발밑은 좌우 방향키로 서로 오갈 수 있다. 둘을 따로 만들면 넘어갈 때마다
/// 한쪽을 닫고 다른 쪽을 여는 코루틴이 교차하게 되고, 예전에 겪은 "코루틴 둘이
/// 같은 입력을 읽는" 문제가 그대로 돌아온다. 화면은 상태 하나로 두었다.
///
/// MonoBehaviour 가 아니지만 IEnumerator 를 돌려주므로
/// GameManager 가 StartCoroutine(controller.Run(...)) 으로 실행할 수 있다.
/// </summary>
public class BagMenuController
{
    /// <summary>고른 행동. 실제로 수행하는 일은 GameManager 가 한다.</summary>
    public enum ItemAction
    {
        /// <summary>가방의 것을 쓴다(먹는다 / 장착한다).</summary>
        Use,
        /// <summary>가방의 것을 바닥에 둔다.</summary>
        Drop,
        /// <summary>가방의 것을 던진다.</summary>
        Throw,
        /// <summary>발밑의 것을 줍는다. 가방 index 는 쓰지 않는다.</summary>
        PickUp,
        /// <summary>발밑의 것과 가방의 index 번째를 맞바꾼다.</summary>
        Swap,
        /// <summary>발밑의 것을 줍지 않고 그 자리에서 쓴다.</summary>
        GroundUse,
        /// <summary>발밑의 것을 줍지 않고 그 자리에서 던진다.</summary>
        GroundThrow,
    }

    /// <summary>지금 보고 있는 화면.</summary>
    private enum Screen { Bag, UnderFoot }

    /// <summary>Browse 가 "좌우로 넘어갔다" 를 알리는 값.</summary>
    private const int SwitchedAway = -2;

    /// <summary>Browse 가 "닫았다" 를 알리는 값.</summary>
    private const int Cancelled = -1;

    private const string EmptyBagText = "가방에는 아무것도 없다";

    private readonly PanelView panel;
    private readonly MessageBoxView messageBox;
    private readonly Inventory inventory;
    private readonly Equipment equipment;

    /// <summary>지금 밟고 서 있는 물건. 없으면 null.</summary>
    private readonly GameObject groundItem;

    private Screen screen;
    private int selectedItem;

    /// <summary>발밑에서 '교환한다' 를 골라 가방으로 넘어온 상태인가.</summary>
    private bool swapping;

    public BagMenuController(
        PanelView panel,
        MessageBoxView messageBox,
        Inventory inventory,
        Equipment equipment,
        GameObject groundItem,
        bool startOnGround)
    {
        this.panel = panel;
        this.messageBox = messageBox;
        this.inventory = inventory;
        this.equipment = equipment;
        this.groundItem = groundItem;

        screen = startOnGround && groundItem != null ? Screen.UnderFoot : Screen.Bag;
    }

    private bool HasGroundItem => groundItem != null;

    /// <summary>발밑의 것이 아이템이 아니라 돈인가.</summary>
    private bool GroundIsGold => GoldPile.IsGold(groundItem);

    /// <summary>
    /// 돈은 가방에 들어가지 않으므로 바꿀 대상이 될 수 없다.
    /// 가방 쪽 메뉴에 '교환한다' 를 붙일지도 이 값으로 정한다.
    /// </summary>
    private bool CanSwapWithGround => HasGroundItem && !GroundIsGold;

    /// <summary>
    /// 발밑의 것을 화면에 뭐라고 부를지.
    /// 돈은 종류가 하나뿐이라 이름만으로는 알 수 없으니 액수를 함께 보여 준다.
    /// </summary>
    private string GroundItemName
    {
        get
        {
            if (groundItem == null) return string.Empty;

            // 돈에는 표식을 붙일 수 없다(종류가 하나뿐이고 액수가 곧 이름이다).
            return GroundIsGold
                ? GoldPile.DisplayName(GoldPile.AmountOf(groundItem))
                : ItemInstance.MarkPrefixOf(groundItem) + ItemInstance.DisplayNameOf(groundItem);
        }
    }

    /// <summary>
    /// 화면을 열고, 닫힐 때까지 돈다.
    /// 무언가 하기로 했으면 onAction(행동, 가방 인덱스)을 부르고 끝난다.
    /// 그냥 닫았으면 onAction 은 불리지 않는다.
    /// </summary>
    public IEnumerator Run(Action<ItemAction, int> onAction)
    {
        selectedItem = 0;

        while (true)
        {
            if (screen == Screen.UnderFoot)
            {
                GroundChoice? picked = null;
                yield return ChooseGroundAction(result => picked = result);

                if (picked == null) break;                       // 취소 → 닫는다

                switch (picked.Value)
                {
                    case GroundChoice.SwitchToBag:
                        screen = Screen.Bag;
                        continue;
                    case GroundChoice.Swap:
                        // 가방으로 넘어가서 무엇과 바꿀지 고르게 한다.
                        screen = Screen.Bag;
                        swapping = true;
                        continue;
                    case GroundChoice.PickUp:
                        Finish(onAction, ItemAction.PickUp, 0);
                        yield break;
                    case GroundChoice.Use:
                        Finish(onAction, ItemAction.GroundUse, 0);
                        yield break;
                    case GroundChoice.Throw:
                        Finish(onAction, ItemAction.GroundThrow, 0);
                        yield break;
                }
            }

            yield return Browse();

            if (selectedItem == SwitchedAway)
            {
                screen = Screen.UnderFoot;
                swapping = false;                                // 물음은 여기서 끝난다
                messageBox.Hide();
                continue;
            }

            if (selectedItem == Cancelled) break;

            // 발밑에서 '교환한다' 로 넘어온 길이면, 무엇과 바꿀지만 고르면 끝이다.
            // 여기서 행동 메뉴를 한 번 더 띄우면 이미 정한 일을 다시 고르게 하는 셈이다.
            if (swapping)
            {
                Finish(onAction, ItemAction.Swap, selectedItem);
                yield break;
            }

            ItemAction? chosen = null;
            yield return ChooseAction(result => chosen = result);

            if (chosen == null) continue;                        // 취소 → 목록으로

            Finish(onAction, chosen.Value, selectedItem);
            yield break;
        }

        panel.Hide();
        messageBox.Hide();
    }

    private void Finish(Action<ItemAction, int> onAction, ItemAction action, int index)
    {
        panel.Hide();
        messageBox.Hide();
        onAction?.Invoke(action, index);
    }

    // ── 가방 화면 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 목록 화면. 아이템을 고르면 그 인덱스가, 닫으면 Cancelled 가,
    /// 좌우로 넘어가면 SwitchedAway 가 selectedItem 에 남는다.
    /// </summary>
    private IEnumerator Browse()
    {
        while (true)
        {
            if (swapping)
                messageBox.ShowPersistent(MessageCatalog.Format(GameMessage.SwapWithWhat));

            panel.ShowList(inventory.BuildListText(selectedItem, EmptyBagText));

            // 이 상태에 막 들어온 프레임의 입력은 무시한다.
            // (가방을 연 그 키가 곧바로 가방을 닫아 버리는 것을 막는다)
            yield return null;

            // 두 버튼을 함께 눌렀으면 정리다. 제자리 대기와 같은 손짓을 쓴다.
            //
            // 반드시 A·B 각각의 판정보다 먼저 봐야 한다. 뒤에 두면 A 가 먼저
            // 잡혀서 아이템을 고르는 것으로 새어 나간다. 그래서 둘 다 소비한다.
            if (GameInput.WaitRequested)
            {
                GameInput.ConsumeAction();
                GameInput.ConsumeAssist();
                SortBag();
                continue;
            }

            // B 는 <b>뗄 때</b> 닫는다. 누르는 순간 닫으면 "B 를 누른 채 A" 라는
            // 조합 자체가 성립하지 않아 위의 정리 입력을 칠 수가 없다.
            // 플레이어 조작에서 A+B 대기가 되는 이유도 같다 — 거기서도 B 는
            // 누를 때 아무 일도 하지 않고 뗄 때 조준이 된다.
            //
            // 정리로 쓰인 B 는 위에서 소비했으므로 손을 뗄 때 닫히지 않는다.
            if (GameInput.AssistTapped)
            {
                selectedItem = Cancelled;
                yield break;
            }

            // 발밑에 무언가 있을 때만 좌우로 넘어갈 수 있다.
            if (HasGroundItem && GameInput.MenuSwitch != 0)
            {
                selectedItem = SwitchedAway;
                yield break;
            }

            if (inventory.IsEmpty) continue;

            int step = GameInput.MenuStep;
            if (step != 0)
                selectedItem = inventory.WrapIndex(selectedItem + step);

            if (GameInput.ActionPressed) yield break;
        }
    }

    /// <summary>가방 아이템의 행동 메뉴. 취소하면 null 을 넘긴다.</summary>
    private IEnumerator ChooseAction(Action<ItemAction?> onResult)
    {
        string[] labels = BuildBagLabels(selectedItem);
        ItemAction[] actions = BuildBagActions();
        int selectedAction = 0;

        // 창이 하나뿐이라 메뉴가 뜨면 목록은 자리를 내준다. 그러면 어떤 아이템을
        // 고른 상태였는지가 화면에서 사라지므로, 맨 윗줄에 그 이름을 남겨 둔다.
        //
        // 오브젝트 이름이 아니라 시트에 적힌 이름을 쓴다. 표식도 함께 얹어서,
        // 마킹한 뒤 무엇이 붙었는지 그 자리에서 보이게 한다.
        while (true)
        {
            string title = TitleOf(inventory[selectedItem]);
            panel.ShowList(BuildMenuText(title, labels, selectedAction));

            yield return null;

            if (GameInput.AssistPressed)
            {
                // 소비하지 않으면 손을 뗄 때 Tapped 가 목록 화면으로 새어 나가
                // 가방까지 함께 닫힌다(Browse 는 뗄 때 닫는다).
                GameInput.ConsumeAssist();
                onResult(null);
                yield break;
            }

            int step = GameInput.MenuStep;
            if (step != 0)
                selectedAction = (selectedAction + step + labels.Length) % labels.Length;

            if (!GameInput.ActionPressed) continue;

            // '확인한다' 는 창을 닫으면 목록이 아니라 이 메뉴로 되돌아온다.
            // 설명을 본 뒤 곧바로 쓰거나 던지는 일이 많다.
            if (selectedAction == InspectIndex)
            {
                yield return ShowDetail(ItemInstance.DescriptionOf(inventory[selectedItem]));
                continue;
            }

            // '마킹한다' 도 여기서 끝난다. 턴을 쓰지 않는 표시일 뿐이라
            // GameManager 까지 갈 일이 없다.
            if (selectedAction == MarkIndex)
            {
                yield return ChooseMark(inventory[selectedItem]);
                continue;
            }

            onResult(actions[selectedAction]);
            yield break;
        }
    }

    // ── 발밑 화면 ──────────────────────────────────────────────────────────

    private enum GroundChoice { Use, Inspect, PickUp, Throw, Swap, SwitchToBag }


    /// <summary>
    /// 발밑 아이템의 행동 메뉴.
    ///
    /// 가방과 달리 고를 물건이 하나뿐이라 목록 단계가 없다. 곧바로 메뉴가 뜬다.
    /// '둔다' 와 '던진다' 는 넣지 않았다. 이미 바닥에 있는 것을 다시 두거나,
    /// 손에 들지도 않은 것을 던질 수는 없기 때문이다.
    /// </summary>
    private IEnumerator ChooseGroundAction(Action<GroundChoice?> onResult)
    {
        // 가방 메뉴와 자리를 맞췄다. 가방은 [쓴다 · 확인한다 · 둔다 · 던진다 · 교환한다],
        // 발밑은 셋째 자리만 '둔다' 대신 그 반대인 '줍는다' 가 온다.
        // 같은 자리에 같은 성격의 항목이 오면 손이 자리를 외운다.
        //
        // 돈은 쓸 수도, 설명을 볼 수도, 가방의 것과 바꿀 수도 없다.
        // 예전에는 그런 항목까지 그대로 띄워서 '안된다' 라는 고를 수 없는 줄이 보였다.
        // 할 수 없는 일은 아예 내놓지 않는다.
        string[] labels;
        GroundChoice[] choices;

        if (GroundIsGold)
        {
            labels = new[] { "줍는다", "던진다" };
            choices = new[] { GroundChoice.PickUp, GroundChoice.Throw };
        }
        else
        {
            labels = new[]
            {
                UseLabelOf(ItemInstance.DescriptionOf(groundItem)), "확인한다", "줍는다", "던진다", "교환한다",
            };
            choices = new[]
            {
                GroundChoice.Use, GroundChoice.Inspect, GroundChoice.PickUp,
                GroundChoice.Throw, GroundChoice.Swap,
            };
        }

        int selected = 0;

        while (true)
        {
            panel.ShowList(BuildMenuText(GroundItemName, labels, selected));

            yield return null;

            if (GameInput.AssistPressed)
            {
                // 소비하지 않으면 손을 뗄 때 Tapped 가 목록 화면으로 새어 나가
                // 가방까지 함께 닫힌다(Browse 는 뗄 때 닫는다).
                GameInput.ConsumeAssist();
                onResult(null);
                yield break;
            }

            if (GameInput.MenuSwitch != 0)
            {
                onResult(GroundChoice.SwitchToBag);
                yield break;
            }

            int step = GameInput.MenuStep;
            if (step != 0)
                selected = (selected + step + labels.Length) % labels.Length;

            if (!GameInput.ActionPressed) continue;

            if (choices[selected] == GroundChoice.Inspect)
            {
                yield return ShowDetail(ItemInstance.DescriptionOf(groundItem));
                continue;
            }

            onResult(choices[selected]);
            yield break;
        }
    }

    // ── 공통 ───────────────────────────────────────────────────────────────

    /// <summary>설명창. 취소 키를 누를 때까지 떠 있는다.</summary>
    private IEnumerator ShowDetail(ItemDescription description)
    {
        panel.ShowDetail(description);

        do
        {
            yield return null;
        }
        while (!GameInput.AssistPressed);

        // 설명을 닫은 B 가 뗄 때 목록까지 닫지 않도록 소비한다.
        GameInput.ConsumeAssist();
    }

    /// <summary>
    /// 아이템 종류에 따라 첫 번째 메뉴 항목의 이름이 달라진다.
    ///
    /// 장비는 먹거나 쓰는 것이 아니라 몸에 걸치는 것이므로
    /// 이미 차고 있으면 "벗는다", 아니면 "장착한다" 가 된다.
    ///
    /// 발밑에 무언가 있으면 '교환한다' 가 뒤에 붙는다. 발밑에서 '교환한다' 를
    /// 골라 넘어온 경우뿐 아니라 평소에도 붙는다 — 같은 자리에서 같은 일을
    /// 하는데 들어온 길에 따라 메뉴가 달라지면 손이 헷갈리기 때문이다.
    /// </summary>
    private string[] BuildBagLabels(int index)
    {
        GameObject item = inventory[index];
        string useLabel = UseLabelOf(ItemInstance.DescriptionOf(item), item);

        return CanSwapWithGround
            ? new[] { useLabel, "확인한다", "둔다", "던진다", "마킹한다", "교환한다" }
            : new[] { useLabel, "확인한다", "둔다", "던진다", "마킹한다" };
    }

    /// <summary>
    /// '쓴다' 자리에 들어갈 말. 먹는 것, 쓰는 것, 걸치는 것이 다르다.
    ///
    /// item 을 함께 주면 이미 차고 있는지까지 보고 "벗는다" 가 된다.
    /// 바닥에 있는 것은 차고 있을 수가 없으므로 주지 않아도 된다.
    /// </summary>
    private string UseLabelOf(ItemDescription description, GameObject item = null)
    {
        if (description == null) return "안된다";

        if (description.IsEquipment)
        {
            bool worn = item != null && equipment != null && equipment.IsEquipped(item);

            // 끈적한 장비는 차는 것은 되지만 벗을 수 없다. 차고 있는 동안에는
            // 무엇을 골라도 안 된다는 것이 메뉴에서 미리 보여야 헛손질을 안 한다.
            if (worn && ItemInstance.IsSticky(item)) return "떨어지지 않는다";

            return worn ? "벗는다" : "장착한다";
        }

        // 끈적한 소모품은 쓸 수 없다. 버리거나 던지는 것은 되므로 그 항목은 남는다.
        if (ItemInstance.IsSticky(item)) return "쓸 수 없다";

        return description.itemType == ItemKind.Food ? "먹는다" : "사용한다";
    }

    /// <summary>
    /// BuildBagLabels 와 같은 순서여야 한다.
    ///
    /// '확인한다' 와 '마킹한다' 자리는 쓰이지 않는다. 그 둘은 창을 닫지도 턴을
    /// 쓰지도 않고 이 컨트롤러 안에서 끝나므로 GameManager 까지 갈 일이 없다.
    /// 자리만 맞춰 두면 되지만, 무엇이든 넣으면 나중에 그 값이 진짜로 쓰이는 줄
    /// 알기 쉬워서 Use 로 채우고 여기 적어 둔다.
    /// </summary>
    private ItemAction[] BuildBagActions()
    {
        return CanSwapWithGround
            ? new[] { ItemAction.Use, ItemAction.Use, ItemAction.Drop, ItemAction.Throw,
                      ItemAction.Use, ItemAction.Swap }
            : new[] { ItemAction.Use, ItemAction.Use, ItemAction.Drop, ItemAction.Throw,
                      ItemAction.Use };
    }

    /// <summary>창을 닫지 않고 그 자리에서 처리하는 항목의 자리(BuildBagLabels 기준).</summary>
    private const int InspectIndex = 1;
    private const int MarkIndex = 4;

    /// <summary>
    /// 가방을 이름순으로 정리한다. 턴을 쓰지 않는다.
    ///
    /// 커서는 보고 있던 <b>물건</b>을 계속 따라간다. 번호만 그대로 두면 정리 후
    /// 다른 물건이 선택된 상태가 되어, 이어서 누르던 손이 엉뚱한 것을 쓴다.
    ///
    /// 문구는 띄우지 않는다. 정리된 것은 목록이 다시 그려지는 것으로 이미 보이고,
    /// 메시지창까지 쓰면 정작 읽어야 할 소식이 밀린다. 알림은 앞으로 효과음이 맡는다.
    /// </summary>
    private void SortBag()
    {
        if (inventory.IsEmpty) return;

        GameObject watching = inventory[selectedItem];
        selectedItem = inventory.SortByName(watching);
    }

    // ── 마킹 ───────────────────────────────────────────────────────────────

    /// <summary>메뉴 맨 윗줄에 얹을 이름. 표식이 있으면 함께 보여 준다.</summary>
    private static string TitleOf(GameObject item)
        => ItemInstance.MarkPrefixOf(item) + ItemInstance.DisplayNameOf(item);

    /// <summary>
    /// 표식을 고른다. 색을 먼저, 그다음 모양.
    ///
    /// 색을 먼저 묻는 이유는 모양 화면에서 <b>고른 색으로 칠해 보여 주기</b>
    /// 위해서다. 반대 순서면 모양만 흰색으로 고르고 나서 색을 상상해야 한다.
    ///
    /// 어느 단계에서든 B 로 물러설 수 있고, 그때는 표식이 바뀌지 않는다.
    /// 표식은 턴을 쓰지 않으므로 무르는 데 대가가 없어야 한다.
    /// </summary>
    private IEnumerator ChooseMark(GameObject item)
    {
        var colorLabels = new string[ItemMark.Colors.Length];
        for (int i = 0; i < ItemMark.Colors.Length; i++)
        {
            MarkColor c = ItemMark.Colors[i];
            colorLabels[i] = $"<color={ItemMark.HexOf(c)}>■ {ItemMark.NameOf(c)}</color>";
        }

        int chosenColor = -1;
        yield return PickFromList(TitleOf(item), colorLabels, index => chosenColor = index);
        if (chosenColor < 0) yield break;

        MarkColor color = ItemMark.Colors[chosenColor];

        // 모양은 고른 색으로 칠해 보여 준다. 붙은 뒤의 모습이 그대로 보인다.
        var shapeLabels = new string[ItemMark.Shapes.Length];
        for (int i = 0; i < ItemMark.Shapes.Length; i++)
        {
            MarkShape shape = ItemMark.Shapes[i];
            shapeLabels[i] = shape == MarkShape.None
                ? ItemMark.NameOf(shape)
                : $"<color={ItemMark.HexOf(color)}>{ItemMark.NameOf(shape)}</color>";
        }

        int chosenShape = -1;
        yield return PickFromList(TitleOf(item), shapeLabels, index => chosenShape = index);
        if (chosenShape < 0) yield break;

        // 이 물건 하나가 아니라 그 종류 전부에 붙는다. 다음에 만날 같은 것도
        // 곧바로 알아볼 수 있어야 적어 둔 뜻이 있다(MarkBook 참고).
        GameManager.Marks.Set(ItemInstance.IdOf(item), ItemMark.Shapes[chosenShape], color);
    }

    /// <summary>
    /// 목록에서 하나 고른다. B 로 물러서면 -1.
    ///
    /// 가방 메뉴 · 발밑 메뉴가 각자 쓰고 있던 같은 모양의 반복문을 한 벌로 모았다.
    /// </summary>
    private IEnumerator PickFromList(string title, string[] labels, Action<int> onResult)
    {
        int selected = 0;

        while (true)
        {
            panel.ShowList(BuildMenuText(title, labels, selected));

            // 이 화면을 연 그 키가 곧바로 고르지 않도록 한 프레임 넘긴다.
            yield return null;

            if (GameInput.AssistPressed)
            {
                GameInput.ConsumeAssist();
                onResult(-1);
                yield break;
            }

            int step = GameInput.MenuStep;
            if (step != 0)
                selected = (selected + step + labels.Length) % labels.Length;

            if (!GameInput.ActionPressed) continue;

            onResult(selected);
            yield break;
        }
    }

    /// <summary>고른 것의 이름을 제목처럼 얹은 메뉴 글자.</summary>
    private static string BuildMenuText(string title, string[] options, int selectedIndex)
    {
        var sb = new StringBuilder();
        sb.Append(title).Append('\n');

        for (int i = 0; i < options.Length; i++)
        {
            sb.Append('\n');
            sb.Append(i == selectedIndex ? "> " : "  ");
            sb.Append(options[i]);
        }
        return sb.ToString();
    }
}
