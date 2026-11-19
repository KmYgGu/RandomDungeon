using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 아이템을 가지고 하는 일 전부 — 가방 화면, 발밑 명령, 줍기·교환·쓰기·던지기·장착.
///
/// ── 왜 GameManager 에서 떼어 냈나 ─────────────────────────────────
/// GameManager 가 1859 줄이었고 그중 400 줄이 이것이었다. 아이템 관련해서
/// 무언가 고치려면 층 진행 · 턴 · 상태이상 · 함정이 함께 든 파일을 열어야 했고,
/// 반대로 턴을 고치다 아이템 코드를 스쳐 지나가는 일이 잦았다.
///
/// <b>MonoBehaviour 가 아니다.</b> 이 프로젝트가 갓 오브젝트를 풀어 온 방식
/// 그대로다(HudView · StatusView · BagMenuController). 씬의 Inspector 연결을
/// 하나도 건드리지 않으므로, 옮기는 것만으로 씬이 깨질 일이 없다.
///
/// ── 코루틴은 남의 것을 빌린다 ─────────────────────────────────────
/// IEnumerator 를 만드는 것은 여기서 하고, 돌리는 것(StartCoroutine)은
/// MonoBehaviour 만 할 수 있어서 GameManager 에게 맡긴다.
/// </summary>
public class ItemCommands
{
    private readonly GameManager game;
    private readonly MessageBoxView messageBox;
    private readonly PanelView menuPanel;

    public ItemCommands(GameManager game, MessageBoxView messageBox, PanelView menuPanel)
    {
        this.game = game;
        this.messageBox = messageBox;
        this.menuPanel = menuPanel;
    }

    // 판 단위 상태는 GameManager 가 static 으로 들고 있다. 여기서 사본을 두지 않는
    // 이유는, 층을 넘을 때 갈아 끼워지는 값들이라 사본이 곧 낡은 값이 되기 때문이다.
    private static Inventory Bag => GameManager.Bag;
    private static Equipment Gear => GameManager.Gear;
    private static GroundObjectRegistry Ground => GameManager.Ground;
    private static PlayerStats Stats => GameManager.Stats;

    /// <summary>
    /// 가방에 넣고, 처음 보는 종류면 도감에도 적는다.
    ///
    /// Bag.TryAdd 가 들어오는 자리 셋(줍기·바닥에서 바로 장착·발밑에서 바로 줍기)이
    /// 전부 이걸 거치게 해서, 도감에 적는 일을 한 곳에서만 신경 쓰면 된다.
    /// 교환(Bag.Replace)은 자리를 갈아 끼우는 것이라 여기와 갈래가 달라 SwapWithGround
    /// 에서 따로 적는다.
    /// </summary>
    private static bool AddToBag(GameObject item)
    {
        if (!Bag.TryAdd(item)) return false;
        ShopGood.Taken(item);

        GameManager.RecordItemDiscovery(ItemInstance.IdOf(item));
        GameManager.ReportRecord(RecordStat.ItemsPicked);

        // 주운 그 물건의 강화 수치가 여태 본 것 중 가장 높을 수 있다.
        // 손으로 강화하는 길이 아직 없어서, 지금은 이것이 유일하게 세는 자리다.
        GameManager.ReportRecord(RecordStat.BestEnhancement, ItemInstance.EnhancementOf(item));
        return true;
    }

    /// <summary>
    /// 먹은 것이면 기록에 센다. 먹는 것과 쓰는 것은 시트의 갈래로 갈린다.
    ///
    /// <b>지우기 전에 불러야 한다.</b> 오브젝트를 지운 뒤에는 무엇이었는지
    /// 물어볼 길이 없다.
    /// </summary>
    private static void ReportIfFood(GameObject item)
    {
        ItemDescription description = ItemInstance.DescriptionOf(item);
        if (description != null && description.itemType == ItemKind.Food)
            GameManager.ReportRecord(RecordStat.FoodEaten);
    }

    /// <summary>가방을 연다. 닫히거나 아이템을 쓸 때까지 조작이 잠긴다.</summary>
    public void OpenBag() => OpenItemMenu(startOnGround: false);

    /// <summary>
    /// 발밑 화면을 연다. 밟고 선 것이 무엇이냐에 따라 하는 일이 다르다.
    ///
    ///   함정  - 다시 밟아 작동을 시도한다(턴을 쓴다)
    ///   아이템 - 줍기 / 교환하기 화면을 연다
    ///   아무것도 없으면 그렇다고 알린다(턴을 쓰지 않는다)
    ///
    /// 함정을 먼저 보는 이유는, 함정과 아이템이 같은 칸에 놓이지 않도록
    /// 던전을 만들 때 떨어뜨려 두기 때문이다(DungeonPopulator.PlaceTraps).
    /// 그래도 겹치는 일이 생기면 함정 쪽이 급한 일이다.
    /// </summary>
    public void UseUnderFoot()
    {
        Player player = game.PlayerComponent;

        if (player != null && player.TrapUnderFoot != null)
        {
            player.TrapUnderFoot.TryTrigger();
            game.EndPlayerTurn();
            return;
        }

        if (player != null && player.ItemUnderFoot != null)
        {
            OpenItemMenu(startOnGround: true);
            return;
        }

        game.ShowMessage(GameMessage.NothingUnderFoot);
    }

    private void OpenItemMenu(bool startOnGround)
    {
        InputFocus.Give(InputOwner.BagMenu);
        messageBox.Hide();

        Player player = game.PlayerComponent;
        GameObject ground = player != null ? player.ItemUnderFoot : null;

        var menu = new BagMenuController(
            menuPanel, messageBox, Bag, Gear, ground, startOnGround);

        game.StartCoroutine(RunBagMenu(menu, ground));
    }

    private IEnumerator RunBagMenu(BagMenuController menu, GameObject groundItem)
    {
        BagMenuController.ItemAction? action = null;
        int index = 0;

        yield return menu.Run((chosen, at) =>
        {
            action = chosen;
            index = at;
        });

        if (action == null)
        {
            // 아무것도 안 하고 닫았다. 턴은 지나가지 않는다.
            InputFocus.ReturnToPlayer();
            yield break;
        }

        switch (action.Value)
        {
            case BagMenuController.ItemAction.PickUp:
                PickUpFromGround(groundItem);
                yield break;

            case BagMenuController.ItemAction.Swap:
                yield return SwapWithGround(groundItem, index);
                yield break;

            case BagMenuController.ItemAction.GroundUse:
                yield return UseOnGround(groundItem);
                yield break;

            case BagMenuController.ItemAction.GroundThrow:
                yield return ThrowFromGround(groundItem);
                yield break;
        }

        yield return PerformItemAction(action.Value, index);
    }

    /// <summary>
    /// 발밑의 것을 줍는다.
    ///
    /// 가방이 가득 차 있으면 줍지 못하고, 이때는 턴도 쓰지 않는다.
    /// 아무 일도 일어나지 않았는데 적이 한 걸음 다가오면 억울하기 때문이다.
    /// </summary>
    private void PickUpFromGround(GameObject groundItem)
    {
        InputFocus.ReturnToPlayer();

        if (groundItem == null) return;

        // 돈은 가방에 넣는 물건이 아니라 소지금으로 들어간다.
        // 그래서 가방이 가득 차 있어도 주울 수 있다.
        if (GoldPile.IsGold(groundItem))
        {
            int amount = GoldPile.AmountOf(groundItem);
            Stats.AddGold(amount);
            GameManager.ReportRecord(RecordStat.MostGold, Stats.Gold);

            Ground.Remove(groundItem.transform.position);
            groundItem.SetActive(false);

            game.EndPlayerTurn(GameMessage.GoldPickedUp, amount: amount);
            return;
        }

        if (Bag.IsFull)
        {
            game.ShowMessage(GameMessage.BagFull, ItemInstance.FullNameOf(groundItem));
            return;
        }

        TakeFromGround(groundItem);
        AddToBag(groundItem);

        // 이름은 GameObject 이름이 아니라 정의에서 읽고, 강화 수치를 붙여 보여 준다
        // (ItemInstance.FullNameOf). 프리팹 이름이 시트와 한 글자만 어긋나도
        // 조용히 다른 이름이 떴던 자리다.
        game.EndPlayerTurn(
            GameMessage.ItemPickedUpFromGround, ItemInstance.FullNameOf(groundItem));
    }

    /// <summary>
    /// 발밑의 것과 가방의 index 번째를 맞바꾼다.
    ///
    /// 가방이 가득 차 있어도 할 수 있다는 점이 '줍는다' 와 다르다.
    /// 개수가 늘지 않기 때문이다. 가방이 꽉 찼을 때 바닥의 더 좋은 물건을
    /// 가져갈 방법이 이것뿐이라, 이 경우를 막으면 교환의 쓸모가 거의 사라진다.
    /// </summary>
    private IEnumerator SwapWithGround(GameObject groundItem, int index)
    {
        InputFocus.ReturnToPlayer();

        if (groundItem == null || Bag.Count == 0) yield break;

        // 차고 있는 끈적한 장비는 내놓을 수 없다. 교환도 가방을 떠나는 일이라
        // 여기서 막지 않으면 아래 UnequipIfWearing 이 조용히 벗겨 버린다.
        GameObject offered = Bag[index];
        if (Gear.IsEquipped(offered) && ItemInstance.IsSticky(offered))
        {
            game.ShowMessage(GameMessage.StickyStuck, ItemInstance.FullNameOf(offered));
            yield break;
        }

        // 이름은 GameObject 이름이 아니라 정의에서 읽는다. 바닥에서 집어 든 것도
        // 강화 수치가 붙어 있으면 함께 보여야 교환한 결과를 알 수 있다.
        string takenName = ItemInstance.FullNameOf(groundItem);
        TakeFromGround(groundItem);
        GameManager.RecordItemDiscovery(ItemInstance.IdOf(groundItem));

        // 몸에 걸치고 있던 것을 내려놓게 되면 반드시 벗겨야 한다.
        GameObject given = Bag.Replace(index, groundItem);
        string givenName = ItemInstance.FullNameOf(given);
        Gear.UnequipIfWearing(given);

        // 내려놓는 쪽은 씬으로 되돌려 그 자리에 놓는다.
        SceneManager.MoveGameObjectToScene(given, SceneManager.GetActiveScene());
        given.transform.position = game.PlayerPosition;
        given.SetActive(true);
        Ground.Add(game.PlayerPosition);

        // 바꿔 내놓은 것도 그 자리에서 도로 집으면 교환이 없던 일이 된다.
        game.PlayerComponent?.KeepUnderFootWithoutTaking(given);

        game.EndPlayerTurn(GameMessage.ItemSwapped,givenName,secondName:takenName);
    }

    /// <summary>
    /// 발밑의 것을 줍지 않고 그 자리에서 쓴다.
    ///
    /// 장비는 걸치는 것이라 가방을 거쳐야 한다. 몸에 걸친 물건은 가방 안에 있다는
    /// 것이 이 게임의 규칙이라(Equipment 주석), 바닥에 둔 채로 착용시키면
    /// "가방에 없는데 공격력이 붙어 있는" 상태가 된다. 그래서 장비는 먼저 줍는다.
    /// </summary>
    private IEnumerator UseOnGround(GameObject groundItem)
    {
        if (groundItem == null) { InputFocus.ReturnToPlayer(); yield break; }

        int itemId = ItemInstance.IdOf(groundItem);
        string itemName = ItemInstance.FullNameOf(groundItem);

        if (Gear.SlotOf(groundItem) != EquipmentSlot.None)
        {
            if (Bag.IsFull)
            {
                InputFocus.ReturnToPlayer();
                game.ShowMessage(GameMessage.BagFull, itemName);
                yield break;
            }

            TakeFromGround(groundItem);
            AddToBag(groundItem);
            ToggleEquip(groundItem, itemName);
            yield break;
        }

        // 끈적한 것은 바닥에 있든 가방에 있든 쓸 수 없다.
        if (ItemInstance.IsSticky(groundItem))
        {
            InputFocus.ReturnToPlayer();
            game.ShowMessage(GameMessage.StickyUnusable, itemName);
            yield break;
        }

        ShopGood.Taken(groundItem);
        Ground.Remove(groundItem.transform.position);
        ReportIfFood(groundItem);
        Object.Destroy(groundItem);
        InputFocus.ReturnToPlayer();

        if (ItemEffects.TryUse(itemId, Stats, out GameMessage useMessage))
            game.EndPlayerTurn(useMessage, itemName);
        else
            game.EndPlayerTurn();
    }

    /// <summary>발밑의 것을 줍지 않고 그 자리에서 던진다.</summary>
    private IEnumerator ThrowFromGround(GameObject groundItem)
    {
        if (groundItem == null) { InputFocus.ReturnToPlayer(); yield break; }

        string itemName = ItemInstance.FullNameOf(groundItem);

        // 날아가는 동안에는 이 칸에 없으므로 바닥 목록에서 먼저 뺀다.
        // ItemThrower 가 떨어진 자리에 다시 넣어 준다.
        ShopGood.Taken(groundItem);
        Ground.Remove(groundItem.transform.position);

        // 밟고 선 칸에 있으니 이미 같은 자리지만, 던지기는 출발점을 기준으로
        // 거리를 세므로 어긋난 채로 시작하지 않도록 맞춰 둔다.
        groundItem.transform.position = game.PlayerPosition;

        var thrower = new ItemThrower(GameManager.FloorTilemap, Ground, game.enemies);
        GameMessage? result = null;

        yield return thrower.Throw(
            groundItem, GameManager.LookDirection, Stats, message => result = message);

        InputFocus.ReturnToPlayer();

        if (result.HasValue) game.EndPlayerTurn(result.Value, itemName);
        else game.EndPlayerTurn();
    }

    /// <summary>바닥의 물건을 손에 넣을 수 있는 상태로 만든다.</summary>
    private void TakeFromGround(GameObject groundItem)
    {
        ShopGood.Taken(groundItem);
        Object.DontDestroyOnLoad(groundItem);
        groundItem.name = groundItem.name.Replace("(Clone)", string.Empty);

        Ground.Remove(groundItem.transform.position);
        groundItem.SetActive(false);
    }

    /// <summary>가방에서 고른 행동을 실제로 수행한다.</summary>
    private IEnumerator PerformItemAction(BagMenuController.ItemAction action, int index)
    {
        GameObject item = Bag[index];
        int itemId = ItemInstance.IdOf(item);
        string itemName = ItemInstance.FullNameOf(item);

        // 장비는 가방에서 빠지지 않는다. 차고 있다는 표시만 바뀐다.
        if (action == BagMenuController.ItemAction.Use && Gear.SlotOf(item) != EquipmentSlot.None)
        {
            ToggleEquip(item, itemName);
            yield break;
        }

        // ── 끈적끈적 ───────────────────────────────────────────────────────
        //
        // 소모품이면 쓸 수 없다. 버리거나 던지는 것은 되므로 여기서만 막는다.
        if (action == BagMenuController.ItemAction.Use && ItemInstance.IsSticky(item))
        {
            InputFocus.ReturnToPlayer();
            game.ShowMessage(GameMessage.StickyUnusable, itemName);
            yield break;
        }

        // 차고 있는 끈적한 장비는 가방을 떠날 수 없다. 벗을 수 없다는 것이
        // 곧 "버리지도 던지지도 교환하지도 못한다" 는 뜻이다 — 여기서 막지 않으면
        // 아래 UnequipIfWearing 이 조용히 벗겨서 규칙에 구멍이 난다.
        if (Gear.IsEquipped(item) && ItemInstance.IsSticky(item))
        {
            InputFocus.ReturnToPlayer();
            game.ShowMessage(GameMessage.StickyStuck, itemName);
            yield break;
        }

        // 몸에 걸치고 있던 것이 가방을 떠나면 반드시 벗겨야 한다.
        // 그러지 않으면 손에 없는 물건의 공격력이 계속 붙어 있게 된다.
        Gear.UnequipIfWearing(item);

        // DontDestroyOnLoad 로 옮겨 뒀던 오브젝트를 현재 씬으로 되돌린다.
        SceneManager.MoveGameObjectToScene(item, SceneManager.GetActiveScene());
        Bag.RemoveAt(index);

        switch (action)
        {
            case BagMenuController.ItemAction.Use:
                // 지우기 전에 무엇이었는지 센다. 지운 뒤에는 갈래를 물어볼 수 없다.
                ReportIfFood(item);

                Object.Destroy(item);
                InputFocus.ReturnToPlayer();

                if (ItemEffects.TryUse(itemId, Stats, out GameMessage useMessage))
                    game.EndPlayerTurn(useMessage, itemName);
                else
                    game.EndPlayerTurn();
                break;

            case BagMenuController.ItemAction.Drop:
                item.transform.position = game.PlayerPosition;
                item.SetActive(true);
                Ground.Add(game.PlayerPosition);

                // 방금 내 손으로 놓은 것이므로 그 자리에서 도로 집지 않는다.
                game.PlayerComponent?.KeepUnderFootWithoutTaking(item);

                InputFocus.ReturnToPlayer();
                game.EndPlayerTurn(GameMessage.ItemDropped,itemName);
                break;

            case BagMenuController.ItemAction.Throw:
                item.transform.position = game.PlayerPosition;
                item.SetActive(true);

                var thrower = new ItemThrower(GameManager.FloorTilemap, Ground, game.enemies);
                GameMessage? result = null;

                yield return thrower.Throw(
                    item, GameManager.LookDirection, Stats,
                    message => result = message);

                InputFocus.ReturnToPlayer();

                // 적에게 맞았다면 그쪽에서 이미 결과를 알렸으므로 덧붙이지 않는다.
                if (result.HasValue)
                    game.EndPlayerTurn(result.Value, itemName);
                else
                    game.EndPlayerTurn();
                break;
        }
    }

    /// <summary>
    /// 장비를 차거나 벗는다. 어느 쪽이든 가방에는 그대로 남는다.
    /// 같은 슬롯에 이미 뭔가 차고 있었다면 그것과 바꿔 낀다.
    /// </summary>
    private void ToggleEquip(GameObject item, string itemName)
    {
        InputFocus.ReturnToPlayer();

        if (Gear.IsEquipped(item))
        {
            // 차는 것은 됐지만 벗는 것은 안 된다. 끈적한 장비를 집어 든 순간이
            // 아니라 <b>차는 순간</b>이 돌이킬 수 없는 지점이다.
            if (ItemInstance.IsSticky(item))
            {
                game.ShowMessage(GameMessage.StickyStuck, itemName);
                return;
            }

            Gear.Unequip(Gear.SlotOf(item));
            game.EndPlayerTurn(GameMessage.Unequipped, itemName);
            return;
        }

        Gear.TryEquip(item);
        game.EndPlayerTurn(GameMessage.Equipped, itemName);
    }

    /// <summary>바닥의 아이템을 줍는다. 가방이 가득 찼으면 줍지 못한다.</summary>
    public void PickUpItem(GameObject item)
    {
        if (ShopGood.IsStock(item))
        { game.ShowMessage(GameMessage.SteppedOnItem,ItemInstance.FullNameOf(item)); return; }
        if (Bag.IsFull)
        {
            game.ShowMessage(GameMessage.BagFull, ItemInstance.FullNameOf(item));
            return;
        }

        Object.DontDestroyOnLoad(item);
        item.name = item.name.Replace("(Clone)", string.Empty);

        AddToBag(item);
        Ground.Remove(item.transform.position);

        game.ShowMessage(GameMessage.ItemPickedUp, ItemInstance.FullNameOf(item));
        item.SetActive(false);
    }
}
