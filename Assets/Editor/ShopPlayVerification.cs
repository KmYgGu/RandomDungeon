using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ShopPlayVerification
{
    private const string Active="ShopPlayVerification.Active";
    private static double deadline;
    private static int phase;
    private static Merchant pursued;
    static ShopPlayVerification()
    {
        EditorApplication.playModeStateChanged+=OnState;
        if(SessionState.GetBool(Active,false) && Application.isPlaying) Begin();
    }
    public static void Run()
    {
        if(!Application.isBatchMode) throw new InvalidOperationException("배치 전용");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var generator=UnityEngine.Object.FindObjectOfType<RandomDung>();
        generator.minRoomSize=generator.maxRoomSize=6;
        generator.shopChance=1f; generator.useFixedSeed=true; generator.fixedSeed=0;
        SessionState.SetBool(Active,true);
        EditorApplication.isPlaying=true;
    }
    private static void OnState(PlayModeStateChange state)
    { if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active,false)) Begin(); }
    private static void Begin()
    { deadline=EditorApplication.timeSinceStartup+100; EditorApplication.update-=Tick; EditorApplication.update+=Tick; }
    private static void Tick()
    {
        try
        {
            if(!Application.isPlaying) return;
            Require(EditorApplication.timeSinceStartup<deadline,"플레이 검사 시간 초과");
            var game=GameManager.instance;
            if(game==null || game.Player==null || !game.playersTurn || Time.timeSinceLevelLoad<3f) return;
            if(phase==1)
            {
                Require(pursued==null,"상인이 다음 층까지 추적함");
                Debug.Log("SHOP_PLAY_VERIFICATION_PASS: price, greeting, drop without sale, quote on talk, opening press rejected, explicit consent, declined ownership, floor-limited pursuit");
                SessionState.SetBool(Active,false); EditorApplication.update-=Tick; EditorApplication.Exit(0); return;
            }
            var shops=UnityEngine.Object.FindObjectsOfType<ShopRoom>();
            if(shops.Length==0)
            {
                Require(GameManager.Stage<12,"검사 가능한 상점 미생성");
                game.MapMake.fixedSeed++; ShopRoom.LoadNextFloor(); return;
            }
            CheckShop(game,shops[0]);
        }
        catch(Exception error)
        { Debug.LogException(error); SessionState.SetBool(Active,false); EditorApplication.update-=Tick; EditorApplication.Exit(1); }
    }
    private static void Position(GameManager game,Vector3 point)
    { game.Player.GetComponent<Rigidbody2D>().position=point; game.Player.transform.position=point; game.PlayerPosition=point; Physics2D.SyncTransforms(); }
    private static void Pickup(GameManager game,ShopGood good)
    {
        Position(game,good.transform.position);
        var commands=typeof(GameManager).GetField("items",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
        typeof(ItemCommands).GetMethod("PickUpFromGround",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(commands,new object[]{good.gameObject});
        game.StopAllCoroutines(); game.playersTurn=true;
    }
    private static void Drop(GameManager game,GameObject item,bool sell)
    {
        int index=-1;
        for(int n=0;n<GameManager.Bag.Count;n++) if(GameManager.Bag[n]==item) { index=n; break; }
        Require(index>=0,"판매 검사 가방 물건 누락");
        var commands=typeof(GameManager).GetField("items",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
        var action=(IEnumerator)typeof(ItemCommands).GetMethod("PerformItemAction",BindingFlags.NonPublic|BindingFlags.Instance)
            .Invoke(commands,new object[]{BagMenuController.ItemAction.Drop,index});
        Drive(action,sell); game.StopAllCoroutines(); game.playersTurn=true;
    }
    private static void Drive(IEnumerator root,bool yes,Action afterOpeningHold=null)
    {
        var stack=new Stack<IEnumerator>(); stack.Push(root); int steps=0;
        while(stack.Count>0)
        {
            Require(++steps<10000,"거래 코루틴 종료 실패");
            typeof(GameInput).GetField("polledFrame",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Time.frameCount);
            typeof(GameInput).GetField("menuStep",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,yes ? 0 : 1);
            var button=(ButtonPress)typeof(GameInput).GetField("action",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            bool down=steps<=16 || steps%3==0;
            button.Update(down,down,!down,0.02f);
            var current=stack.Peek();
            if(!current.MoveNext()) stack.Pop();
            else if(current.Current is IEnumerator nested) stack.Push(nested);
            if(steps==16) afterOpeningHold?.Invoke();
        }
        Require(afterOpeningHold==null || steps>=16,"대화를 시작한 입력으로 매입이 즉시 완료됨");
        GameInput.Reset();
    }
    private static void CheckShop(GameManager game,ShopRoom shop)
    {
        var box=(MessageBoxView)typeof(GameManager).GetField("messageBox",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
        game.StopAllCoroutines(); game.playersTurn=true;
        var stock=new List<ShopGood>(); foreach(var good in UnityEngine.Object.FindObjectsOfType<ShopGood>()) if(good.Owner==shop) stock.Add(good);
        Require(stock.Count>=4,"실제 상점 상품 수 부족");
        foreach(var sign in shop.Merchant.GetComponentsInChildren<TextMesh>(true)) Require(sign.text!="SHOP","SHOP 글자 잔존");
        ShopRoom.ObserveEntrance(new Vector3Int(-999,-999,0));
        var entered=ShopRoom.ObserveEntrance(new Vector3Int(shop.Interior.xMin,shop.Interior.yMin,0));
        Require(entered==shop,"상점 진입 감지 누락");
        Drive(entered.Welcome(),true);
        Require(box.LastShown==MessageCatalog.Format(GameMessage.ShopWelcome),"상점 환영 메시지 누락");
        var good1=stock[0]; int price=shop.Ledger.Price(good1.GetInstanceID());
        Position(game,good1.transform.position); GameManager.InteractionHandled=false;
        int count=GameManager.Bag.Count;
        var collider=good1.GetComponentInChildren<Collider2D>(); Require(collider!=null,"상품 콜라이더 없음");
        typeof(Player).GetMethod("OnTriggerStay2D",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game.PlayerComponent,new object[]{collider});
        Require(GameManager.Bag.Count==count && good1.gameObject.activeSelf,"상품 자동 획득 발생");
        Require(box.LastShown.Contains(ItemInstance.DisplayNameOf(good1.gameObject)) &&
            box.LastShown.Contains(price+"G"),"상품을 밟았을 때 이름/가격 표시 누락");
        Pickup(game,good1);
        Require(GameManager.Bag.Count==count+1 && shop.Ledger.Debt==price && shop.Ledger.BlocksExit,"수동 줍기/미결제 차단 실패");
        GameManager.FastForward=true; game.enemyPositions.Clear();
        for(int n=0;n<5;n++) { shop.Merchant.Actor.BeginTurn(); shop.Merchant.Step(); }
        GameManager.FastForward=false;
        Require(GameManager.FloorTilemap.WorldToCell(shop.Merchant.transform.position)==shop.Door,"상인이 출구를 막지 않음");
        GameManager.Stats.AddGold(1000000); int gold=GameManager.Stats.Gold;
        Drive(shop.Talk(),true);
        Require(GameManager.Stats.Gold==gold-price && !good1.ForSale && shop.Ledger.Debt==0,"실제 구매/대금/소유권 이전 실패");
        Require(GameManager.FloorTilemap.WorldToCell(shop.Merchant.transform.position)!=shop.Door,"구매 후 상인이 길을 비키지 않음");
        gold=GameManager.Stats.Gold;
        Drop(game,good1.gameObject,false);
        Require(!good1.ForSale && GameManager.Stats.Gold==gold && InputFocus.PlayerHasFocus,
            "내려놓는 즉시 판매 질문/매입이 발생함");
        Drive(shop.Talk(),false,()=>
        {
            Require(!good1.ForSale && GameManager.Stats.Gold==gold,"판매 질문의 새 결정 전 매입됨");
            Require(box.LastShown.Contains(ShopLedger.BuyOffer(price)+"G") && box.LastShown.Contains("판매하시겠습니까"),
                "대화에 매입가/판매 확인이 없음");
        });
        Require(!good1.ForSale && GameManager.Stats.Gold==gold,"판매 거절 후 소유권/골드 변경");
        Drive(shop.Talk(),true,()=>Require(!good1.ForSale && GameManager.Stats.Gold==gold,"대화 시작 입력으로 즉시 매입됨"));
        Require(good1.ForSale && GameManager.Stats.Gold==gold+ShopLedger.BuyOffer(price),"새 결정 입력 후 매입 실패");
        Pickup(game,good1); Drive(shop.Talk(),false);
        Require(good1.gameObject.activeSelf && good1.ForSale && shop.Ledger.Debt==0 && GameManager.Ground.Contains(good1.transform.position),"실제 상품 반환/재진열 실패");
        int hp=shop.Merchant.Actor.ThisHp; shop.Merchant.Actor.Damaged(999999);
        Require(shop.Merchant.Actor.ThisHp==hp && !shop.Ledger.Hostile,"상인 근접 공격이 대화로 처리되지 않음");
        shop.Merchant.Actor.TakeDirectDamage(1,game.Player.transform.position,true);
        Require(!shop.Ledger.Hostile,"적의 원거리 피해로 상인 적대화");
        var thrown=stock[1]; var outside=shop.Door;
        GameManager.Ground.Remove(thrown.transform.position);
        ShopGood.Taken(thrown.gameObject);
        thrown.transform.position=GridUtil.CellCenter(outside);
        GameManager.Ground.Add(thrown.transform.position);
        ShopRoom.TickAll();
        Require(shop.Ledger.WasUsed(thrown.GetInstanceID()) && shop.Ledger.Debt>0 && shop.Ledger.BlocksExit && !shop.Ledger.Hostile,
            "밖으로 던진 상품의 사용 대금/중립 차단 처리 실패");
        Drive(shop.Talk(),false);
        Require(shop.Ledger.Hostile && shop.Merchant.Actor.IsCombatTarget,"사용 상품 구매 거절 후 상인 적대화 누락");
        pursued=shop.Merchant; phase=1;
        game.StopAllCoroutines(); ShopRoom.LoadNextFloor();
    }
    private static void Require(bool condition,string message)
    { if(!condition) throw new InvalidOperationException(message); }
}
