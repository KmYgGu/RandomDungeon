using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 상점 한 곳. 장부(<see cref="ShopLedger"/>)와 씬의 실제 물건을 잇는다.
///
/// 규칙은 전부 장부가 안다 — 무엇이 상인 것이고, 얼마를 빚졌고, 언제 돌아서는가.
/// 이 클래스는 <b>씬에서 일어난 일을 장부에 알리고</b>(집었다 · 돌려놨다 · 밖으로 나갔다),
/// 장부의 답을 화면으로 옮길 뿐이다(문을 막는다 · 물건을 되돌린다 · 상인이 돌아선다).
/// 그래서 규칙은 Unity 없이 확인할 수 있다(Tools/Verify 의 [상점 …]).
///
/// ── 장부는 턴마다 맞춘다 ──────────────────────────────────────────
/// 한때는 LateUpdate 에서 매 프레임 맞췄다. 턴제라 턴이 흐르지 않는 동안에는
/// 장부가 바뀔 일이 없는데, 프레임마다 가방과 진열품을 전부 훑고 있었다.
/// 지금은 세 군데서만 맞춘다 —
///   턴이 지날 때(<see cref="TickAll"/>, TurnLoop 이 부른다)
///   상인에게 말을 걸 때(<see cref="Talk"/>)
///   방을 나서려 할 때(<see cref="BlocksWalk"/>)
/// 뒤의 둘은 턴 사이에 장부를 읽는 자리라 읽기 직전에 한 번 더 맞춘다.
/// 안 그러면 방 안에 던져 돌려놓은 물건이 아직 날아가는 중이었을 때, 다음 턴이
/// 오기 전까지 "값을 치르라" 며 문을 막는다 — 막혀서 턴이 안 가므로 풀리지도 않는다.
/// </summary>
public sealed class ShopRoom : MonoBehaviour
{
    /// <summary>물건이 손에서 진열대로 날아 돌아가는 시간과 떠오르는 높이(칸).</summary>
    private const float ReturnFlightSeconds = 0.18f;
    private const float ReturnFlightHeight = 0.8f;

    /// <summary>환영 인사를 띄워 두는 시간.</summary>
    private const float WelcomeSeconds = 0.6f;

    /// <summary>"돈이 모자란다" 를 읽을 틈. 곧바로 물건이 되돌아가면 문구가 묻힌다.</summary>
    private const float NoGoldPauseSeconds = 0.5f;

    /// <summary>이 층의 상점들. 층이 바뀌면 오브젝트와 함께 사라진다.</summary>
    private static readonly List<ShopRoom> all = new List<ShopRoom>();

    /// <summary>상인의 물건들. 열쇠는 장부가 쓰는 번호(오브젝트의 InstanceID)다.</summary>
    private readonly Dictionary<int, ShopGood> goods = new Dictionary<int, ShopGood>();

    public ShopLedger Ledger { get; } = new ShopLedger();

    /// <summary>방 안쪽(칸). 안이냐 밖이냐가 도둑을 가른다.</summary>
    public RectInt Interior { get; private set; }

    /// <summary>하나뿐인 문. 빚이 있으면 상인이 여기를 막는다.</summary>
    public Vector3Int Door { get; private set; }

    /// <summary>상인이 평소에 서 있는 자리.</summary>
    public Vector3Int Waiting { get; private set; }

    public Merchant Merchant { get; set; }

    private bool configured;

    /// <summary>대화가 진행 중인가. 겹쳐서 말을 걸지 못하게 한다.</summary>
    private bool talking;

    /// <summary>
    /// 물건을 되돌려 놓는 중인가. 그동안에는 장부를 맞추지 않는다 —
    /// 날아가는 물건은 가방에도 바닥에도 없어서 "사라졌다" 로 읽힌다.
    /// </summary>
    private bool returning;

    private readonly ShopVisit visit = new ShopVisit();

    /// <summary>
    /// 이 상점이 선 층. 씬을 다시 부르는 사이에 지난 층의 상점이 잠깐 남아
    /// 있을 수 있어서, 지금 층의 것인지 물을 때 쓴다.
    /// </summary>
    private int floorStage;

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDestroy() => all.Remove(this);

    public void Configure(RectInt room, Vector3Int door, Vector3Int waiting)
    {
        Interior = room;
        Door = door;
        Waiting = waiting;
        floorStage = GameManager.Stage;
        configured = true;
    }

    public void Register(ShopGood good, int price)
    {
        goods[good.GetInstanceID()] = good;
        Ledger.Register(good.GetInstanceID(), price);
    }

    public void Take(ShopGood good) => Ledger.Take(good.GetInstanceID());

    /// <summary>지금 층에서 살아 있는 상점인가.</summary>
    private bool IsOpenOnThisFloor => configured && floorStage == GameManager.Stage;

    /// <summary>상인이 가게를 보고 있는가(쓰러지지 않았는가).</summary>
    private bool MerchantAlive => Merchant != null && Merchant.Actor.ThisHp > 0;

    private bool Inside(Vector3 world)
    {
        var grid = GameManager.FloorTilemap;
        if (grid == null) return false;

        Vector3Int cell = grid.WorldToCell(world);
        return Interior.Contains(new Vector2Int(cell.x, cell.y));
    }

    // ── 장부 맞추기 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 씬의 지금 모습을 장부에 옮긴다. 여러 번 불러도 답이 같다.
    ///
    ///   가방에 든 상인 물건       → 집어 든 것
    ///   방 안 바닥의 상인 물건    → 돌려놓은 것
    ///   방 밖 바닥의 상인 물건    → 써 버린 것(던져 내보냈다. 값이 남는다)
    ///   빚을 진 채 방 밖에 있다   → 도둑
    /// </summary>
    private void Reconcile()
    {
        GameManager game = GameManager.instance;

        if (!IsOpenOnThisFloor || returning || !MerchantAlive) return;
        if (game == null || game.Player == null || Ledger.Hostile) return;

        bool wasBlocking = Ledger.BlocksExit;

        for (int i = 0; i < GameManager.Bag.Count; i++)
        {
            GameObject item = GameManager.Bag[i];
            ShopGood good = item != null ? item.GetComponent<ShopGood>() : null;

            if (good != null && good.Owner == this && good.ForSale)
                Ledger.Take(good.GetInstanceID());
        }

        foreach (KeyValuePair<int, ShopGood> pair in goods)
        {
            ShopGood good = pair.Value;

            // 바닥에 놓인 것만 본다. 가방 속(꺼져 있다)과 날아가는 중(바닥 목록에
            // 없다)인 것은 아직 어디에도 놓이지 않았다.
            if (good == null || !good.ForSale || !good.gameObject.activeInHierarchy) continue;
            if (!GameManager.Ground.Contains(good.transform.position)) continue;

            if (Inside(good.transform.position)) Ledger.Return(pair.Key);
            else Ledger.Use(pair.Key);
        }

        // 빚이 방금 다 풀렸으면(전부 돌려놓았다) 상인이 문에서 비켜선다.
        if (wasBlocking && !Ledger.BlocksExit) Merchant.MoveAside();

        // 걸어서는 못 나간다(BlocksWalk). 여기 걸리는 것은 워프 · 날려지기 ·
        // 구멍처럼 문을 거치지 않고 나간 경우다.
        bool wasHostile = Ledger.Hostile;
        if (Ledger.Leave(Inside(game.Player.transform.position)))
            AnnounceHostility(wasHostile, GameMessage.ShopTheft);
    }

    /// <summary>한 턴이 지났다. 이 층의 모든 상점이 장부를 맞춘다.</summary>
    public static void TickAll()
    {
        // 맞추다가 상인이 돌아서며 목록이 바뀔 일은 없지만, 오브젝트가 사라지며
        // OnDestroy 가 목록을 고칠 수 있어 번호로 거꾸로 돈다.
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i] != null) all[i].Reconcile();
        }
    }

    /// <summary>
    /// 이 걸음이 값을 안 치르고 상점을 나서는 걸음인가. 그렇다면 막고 알린다.
    ///
    /// <b>물음이면서 문구도 띄운다.</b> 막힌 걸음은 턴을 쓰지 않아 다른 곳에서
    /// 알릴 기회가 없기 때문이다.
    /// </summary>
    public static bool BlocksWalk(Vector3 from, Vector3 to)
    {
        foreach (ShopRoom shop in all)
        {
            if (shop == null || !shop.IsOpenOnThisFloor || !shop.MerchantAlive) continue;
            if (!shop.Inside(from) || shop.Inside(to)) continue;

            // 방금 돌려놓은 것이 있을 수 있다. 읽기 전에 맞춘다(클래스 주석).
            shop.Reconcile();
            if (!shop.Ledger.BlocksExit) continue;

            if (GameManager.instance != null)
                GameManager.instance.ShowMessage(GameMessage.ShopPayFirst);
            return true;
        }

        return false;
    }

    // ── 돌아서기 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 상인이 돌아선다. 이미 돌아서 있었으면 문구는 다시 띄우지 않는다.
    ///
    /// 장부의 규칙으로 돌아서는 경우(도둑 · 원거리)는 장부가 직접 정하고
    /// (Ledger.Leave · Ledger.RangedHit) 뒷일만 <see cref="AnnounceHostility"/> 가 한다.
    /// 이 메서드는 그 둘에 들지 않는 경우에 쓴다 — 써 버린 물건값을 안 치렀거나,
    /// 빚을 진 채 층을 떠날 때.
    /// </summary>
    public void BecomeHostile(GameMessage reason)
    {
        bool wasHostile = Ledger.Hostile;
        Ledger.Steal();
        AnnounceHostility(wasHostile, reason);
    }

    /// <summary>돌아섰다는 장부의 답을 씬에 옮긴다 — 상인을 깨우고, 처음이면 알린다.</summary>
    private void AnnounceHostility(bool wasHostile, GameMessage reason)
    {
        if (!Ledger.Hostile) return;

        if (Merchant != null) Merchant.Actor.WakeMerchant();

        if (!wasHostile && GameManager.instance != null)
            GameManager.instance.ShowMessage(reason);
    }

    /// <summary>상인이 멀리서 날아온 것에 맞았다. 적이 쏜 것이면 참는다(장부의 규칙).</summary>
    public void RangedHit(bool fromEnemy)
    {
        bool wasHostile = Ledger.Hostile;
        Ledger.RangedHit(fromEnemy);
        AnnounceHostility(wasHostile, GameMessage.ShopAngry);
    }

    // ── 빈자리 찾기 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 방 안에서 빈 칸 하나를 찾는다. preferred 가 비어 있으면 그 칸이다.
    /// </summary>
    /// <param name="forMerchant">
    /// 상인이 설 자리를 찾는가. 그때는 <b>생물</b>이 없는 칸을, 아니면(물건을 놓을
    /// 자리) <b>바닥에 아무것도 없는</b> 칸을 찾는다.
    /// </param>
    public bool TryFreeCell(Vector3Int preferred, out Vector3Int found, bool forMerchant = false)
    {
        var grid = GameManager.FloorTilemap;

        bool Free(Vector3Int cell)
        {
            if (grid == null || !grid.HasTile(cell) || !TileRules.IsWalkableCell(grid, cell)) return false;

            if (!forMerchant) return !GameManager.Ground.Contains(cell);

            return !IsCreatureAt(grid, cell);
        }

        // 방 안인지와 훑는 차례는 몬스터 하우스의 착지와 같은 것을 쓴다.
        return new MonsterHousePlan(Interior)
            .TryLanding(preferred, new HashSet<Vector3Int>(), Free, out found);
    }

    /// <summary>그 칸에 플레이어 · 다른 적 · 동료가 서 있는가. 상인 자신은 세지 않는다.</summary>
    private bool IsCreatureAt(UnityEngine.Tilemaps.Tilemap grid, Vector3Int cell)
    {
        GameManager game = GameManager.instance;

        if (grid.WorldToCell(game.Player.transform.position) == cell) return true;

        foreach (Enemy actor in game.enemies)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy) continue;
            if (Merchant != null && actor == Merchant.Actor) continue;

            if (grid.WorldToCell(actor.transform.position) == cell) return true;
        }

        foreach (Partner actor in game.partners)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy) continue;

            if (grid.WorldToCell(actor.transform.position) == cell) return true;
        }

        return false;
    }

    // ── 대화 ────────────────────────────────────────────────────────────────

    private IEnumerator Ask(GameMessage message, int amount, MessageBoxView.Choice answer, string itemName = null)
    {
        MessageBoxView box = GameManager.instance.MessageBox;

        GameInput.ConsumeAction();
        box.ShowPersistent(MessageCatalog.Format(message, name: itemName, amount: amount));

        // 말을 건 그 A 가 곧바로 '예' 가 되지 않게 막는 일은 물음 쪽이 한다.
        yield return box.AskYesNo(answer);
        box.Hide();
    }

    /// <summary>
    /// 상인에게 말을 건다. 상황에 따라 셋 중 하나가 된다.
    ///
    ///   빚이 있다                  → 사겠느냐고 묻는다. 거절하면 물건을 되돌린다
    ///   내 물건을 방 안에 내려놨다 → 팔겠느냐고 묻는다
    ///   둘 다 아니다               → 인사만 한다
    ///
    /// 턴을 쓰지 않는다. 물어보는 동안 입력 권한을 가져가고 끝나면 돌려준다.
    /// </summary>
    public IEnumerator Talk()
    {
        if (talking || Merchant == null) yield break;

        talking = true;
        InputFocus.Give(InputOwner.Prompt);
        GameManager game = GameManager.instance;

        try
        {
            // 턴 사이에 장부를 읽는 자리라 먼저 맞춘다(클래스 주석).
            Reconcile();

            if (Ledger.Hostile)
            {
                game.ShowMessage(GameMessage.ShopAngry);
                yield break;
            }

            if (Ledger.Debt > 0)
            {
                yield return SettleDebt(game);
                yield break;
            }

            List<GameObject> offers = CollectOffers();
            if (offers.Count == 0)
            {
                game.ShowMessage(GameMessage.ShopGreeting);
                yield break;
            }

            yield return SellItems(offers);
        }
        finally
        {
            // 어느 길로 끝나든(도중에 코루틴이 끊겨도) 입력을 돌려준다.
            talking = false;
            InputFocus.ReturnToPlayer();
        }
    }

    /// <summary>들고 있는 것들을 사겠느냐고 묻는다. 거절하거나 돈이 모자라면 되돌린다.</summary>
    private IEnumerator SettleDebt(GameManager game)
    {
        var answer = new MessageBoxView.Choice();
        yield return Ask(GameMessage.ShopBuyPrompt, Ledger.Debt, answer);

        // 묻는 사이에 돌아섰을 수 있다.
        if (Ledger.Hostile) yield break;

        // 치르고 나면 장부에서 지워지므로 무엇을 샀는지 먼저 받아 둔다.
        List<int> ids = Ledger.Taken;

        if (answer.Value && Ledger.Purchase(GameManager.Stats.Gold, out int paid))
        {
            GameManager.Stats.AddGold(-paid);

            foreach (int id in ids)
            {
                if (goods.TryGetValue(id, out ShopGood good) && good != null) good.Bought();
            }

            Merchant.MoveAside();
            game.ShowMessage(GameMessage.ShopPaid, amount: paid);
            yield break;
        }

        // 사겠다고 했는데 못 샀으면 돈이 모자란 것이다.
        if (answer.Value)
        {
            game.ShowMessage(GameMessage.ShopNoGold);
            yield return new WaitForSeconds(NoGoldPauseSeconds);
        }

        yield return ReturnGoods();
    }

    /// <summary>
    /// 손님이 방 안 바닥에 내려놓은 것들 중 상인이 사 줄 만한 것.
    /// 상인의 물건과 값이 안 나가는 것은 뺀다.
    /// </summary>
    private List<GameObject> CollectOffers()
    {
        var offers = new List<GameObject>();

        foreach (GameObject item in GameObject.FindGameObjectsWithTag("Item"))
        {
            if (!IsSellable(item)) continue;

            ItemDescription description = ItemInstance.DescriptionOf(item);
            int quote = description != null ? ShopLedger.BuyOffer(description.SellPrice) : 0;

            if (quote > 0) offers.Add(item);
        }

        return offers;
    }

    /// <summary>방 안 바닥에 놓여 있고 상인 것이 아닌가.</summary>
    private bool IsSellable(GameObject item)
    {
        return item != null
            && item.activeInHierarchy
            && Inside(item.transform.position)
            && GameManager.Ground.Contains(item.transform.position)
            && !ShopGood.IsStock(item);
    }

    /// <summary>
    /// 내려놓은 것들을 한꺼번에 팔겠느냐고 묻고, 승낙하면 상인의 물건으로 만든다.
    ///
    /// 판 물건은 그 자리에 그대로 놓인 채 상인 것이 된다. 도로 집으면 시세대로
    /// 값을 치러야 한다(상인은 절반에 사서 제값에 판다).
    /// </summary>
    private IEnumerator SellItems(List<GameObject> offers)
    {
        GameManager game = GameManager.instance;

        long total = 0;
        foreach (GameObject item in offers)
        {
            ItemDescription description = ItemInstance.DescriptionOf(item);
            if (description != null) total += ShopLedger.BuyOffer(description.SellPrice);
        }

        int price = (int)Math.Min(int.MaxValue, total);
        string itemName = offers.Count == 1
            ? ItemInstance.FullNameOf(offers[0])
            : $"놓으신 물건 {offers.Count}개";

        var sell = new MessageBoxView.Choice();
        yield return Ask(GameMessage.ShopSellPrompt, price, sell, itemName);

        if (!sell.Value || Ledger.Hostile) yield break;

        // 묻는 사이에 사정이 달라졌을 수 있으므로 하나씩 다시 확인하고,
        // <b>실제로 넘긴 것만큼만</b> 값을 준다.
        long received = 0;

        foreach (GameObject item in offers)
        {
            if (!IsSellable(item)) continue;

            ItemDescription description = ItemInstance.DescriptionOf(item);
            if (description == null) continue;

            ShopGood good = item.GetComponent<ShopGood>();
            if (good == null) good = item.AddComponent<ShopGood>();

            good.Bind(this, GameManager.FloorTilemap.WorldToCell(item.transform.position), description.SellPrice);
            received += ShopLedger.BuyOffer(description.SellPrice);
        }

        // 소지금이 int 를 넘지 않게 받을 수 있는 만큼만 받는다.
        int gained = (int)Math.Min(received, int.MaxValue - GameManager.Stats.Gold);

        GameManager.Stats.AddGold(gained);
        game.ShowMessage(GameMessage.ShopSold, amount: (int)Math.Min(int.MaxValue, received));
    }

    // ── 드나들기 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 선 칸을 알려 준다. 이번에 막 들어선 상점이 있으면 그것을 돌려준다.
    /// 층을 시작할 때와 턴마다 불린다.
    /// </summary>
    public static ShopRoom ObserveEntrance(Vector3Int playerCell)
    {
        ShopRoom entered = null;

        foreach (ShopRoom shop in all)
        {
            if (shop == null || !shop.IsOpenOnThisFloor) continue;

            bool inside = shop.Interior.Contains(new Vector2Int(playerCell.x, playerCell.y));
            bool friendly = !shop.Ledger.Hostile && shop.MerchantAlive;

            if (shop.visit.Observe(inside, friendly)) entered = shop;
        }

        return entered;
    }

    /// <summary>환영 인사. 달리는 중이었으면 멈춰 세운다.</summary>
    public IEnumerator Welcome()
    {
        GameManager game = GameManager.instance;
        game.PlayerComponent?.InterruptForRoomEvent();

        yield return game.Announce(
            MessageCatalog.Format(GameMessage.ShopWelcome), WelcomePause(), waitForConfirmation: false);
    }

    private static IEnumerator WelcomePause()
    {
        yield return new WaitForSeconds(WelcomeSeconds);
    }

    // ── 되돌려 놓기 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 값을 안 치른 물건들을 진열대로 되돌린다.
    ///
    ///   전부 되돌렸다          → 상인이 비켜서고 끝난다
    ///   놓을 자리가 없다       → 그것은 손에 남고, 빚도 남는다
    ///   써 버려서 없는 것이 있다 → 되돌릴 수 없으므로 상인이 돌아선다
    /// </summary>
    private IEnumerator ReturnGoods()
    {
        returning = true;
        bool missing = false;
        bool noSpace = false;

        try
        {
            foreach (int id in Ledger.Taken)
            {
                if (Ledger.WasUsed(id) || !goods.TryGetValue(id, out ShopGood good) || good == null)
                {
                    missing = true;
                    continue;
                }

                if (!TryFreeCell(good.Home, out Vector3Int landing))
                {
                    noSpace = true;
                    continue;
                }

                yield return FlyBack(id, good.gameObject, landing);
            }
        }
        finally
        {
            returning = false;
        }

        if (missing)
        {
            BecomeHostile(GameMessage.ShopTheft);
        }
        else if (noSpace)
        {
            GameManager.instance.ShowMessage(GameMessage.ShopNoSpace);
        }
        else
        {
            Merchant.MoveAside();
            GameManager.instance.ShowMessage(GameMessage.ShopReturned);
        }
    }

    /// <summary>물건 하나를 가방(또는 바닥)에서 꺼내 진열 칸으로 날려 보낸다.</summary>
    private IEnumerator FlyBack(int id, GameObject item, Vector3Int landing)
    {
        // 가방에 있으면 꺼낸다. 차고 있던 장비면 벗긴다.
        int index = -1;
        for (int i = 0; i < GameManager.Bag.Count; i++)
        {
            if (GameManager.Bag[i] == item) { index = i; break; }
        }

        if (index >= 0)
        {
            GameManager.Gear.UnequipIfWearing(item);
            GameManager.Bag.RemoveAt(index);
        }

        // 바닥에 있었으면(방 안 다른 칸) 그 자리를 비운다.
        GameManager.Ground.Remove(item.transform.position);

        // 가방 속 물건은 씬을 넘어 살아남는 쪽에 옮겨져 있다. 이 층의 것으로 되돌린다.
        SceneManager.MoveGameObjectToScene(item, SceneManager.GetActiveScene());

        // 날아가는 동안에는 밟히지 않게 한다. 손에서 떠나는 순간 플레이어와
        // 겹쳐 있어서, 켜 둔 채면 날아가는 것을 도로 줍는다.
        Collider2D[] colliders = item.GetComponentsInChildren<Collider2D>(true);
        var wasEnabled = new bool[colliders.Length];

        for (int i = 0; i < colliders.Length; i++)
        {
            wasEnabled[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }

        item.SetActive(true);

        Vector3 start = GameManager.instance.Player.transform.position;
        Vector3 end = GridUtil.CellCenter(landing);

        try
        {
            for (float t = 0f; t < ReturnFlightSeconds; t += Time.deltaTime)
            {
                float progress = t / ReturnFlightSeconds;

                item.transform.position = Vector3.Lerp(start, end, progress)
                    + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * ReturnFlightHeight);

                yield return null;
            }
        }
        finally
        {
            // 연출이 도중에 끊겨도 물건은 반드시 제자리에 놓고 장부에 적는다.
            // 공중에 뜬 채 남으면 어디에도 속하지 않는 물건이 된다.
            if (item != null)
            {
                item.transform.position = end;

                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null) colliders[i].enabled = wasEnabled[i];
                }

                GameManager.Ground.Add(end);
                MinimapOverlay.TrackSpawnedItem(item);
                Ledger.Return(id);
            }
        }
    }

    // ── 층이 끝날 때 ────────────────────────────────────────────────────────

    /// <summary>
    /// 남은 물건을 전부 놓아준다. 상인이 쓰러졌거나 층이 끝날 때 부른다.
    /// 가방에 든 물건이 지난 층의 상점을 계속 가리키지 않게 한다.
    /// </summary>
    public void ReleaseStock()
    {
        foreach (ShopGood good in goods.Values)
        {
            if (good != null) good.Bought();
        }
    }

    /// <summary>
    /// 씬을 다시 불러 다음 층으로(또는 새 판으로) 넘어간다.
    /// <b>층을 떠나는 모든 길이 여기를 지난다</b> — 계단 · 구멍 · 게임 오버.
    ///
    /// 상점의 뒷정리가 층을 떠나는 순간에 묶여 있어서 여기 있다. 살아서 빚을 진 채
    /// 내려가면 도둑이고(죽어서 넘어가는 것은 아니다), 어느 쪽이든 물건은 놓아준다.
    /// </summary>
    public static void LoadNextFloor()
    {
        foreach (ShopRoom shop in new List<ShopRoom>(all))
        {
            if (shop == null) continue;

            bool alive = GameManager.Stats != null && !GameManager.Stats.IsDead;
            if (alive && shop.Ledger.Debt > 0) shop.BecomeHostile(GameMessage.ShopTheft);

            shop.ReleaseStock();
        }

        SceneManager.LoadScene("SampleScene");
    }

    /// <summary>새 판을 시작할 때. 오브젝트는 씬과 함께 사라지므로 목록만 비운다.</summary>
    public static void ResetAll() => all.Clear();
}
