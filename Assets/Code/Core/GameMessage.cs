/// <summary>
/// 메시지 창에 띄울 수 있는 문구의 종류.
///
/// 예전에는 ShowDamagedTextBox(name, damage, int Switch) 의 Switch 에
/// 0~15 라는 숫자를 직접 넘겼다. 호출부만 봐서는 3 이 무슨 뜻인지 알 수 없었고,
/// 중간에 하나를 끼워 넣으면 뒤 번호가 전부 밀리는 구조였다.
/// 이름 있는 상수로 바꾸면 호출부가 그 자체로 설명이 된다.
/// </summary>
public enum GameMessage
{
    /// <summary>플레이어가 피해를 입음. amount = 피해량</summary>
    PlayerDamaged,
    /// <summary>플레이어가 치명타를 맞음. amount = 피해량</summary>
    PlayerCriticallyDamaged,
    /// <summary>공격이 빗나감.</summary>
    AttackMissed,
    /// <summary>회심의 일격. amount = 피해량</summary>
    CriticalHit,
    /// <summary>적이 피해를 입음. amount = 피해량</summary>
    EnemyDamaged,
    /// <summary>계단 앞. 아래층으로 내려갈지 묻는다.</summary>
    StairsPrompt,
    /// <summary>골드 획득. amount = 골드</summary>
    GoldPickedUp,
    /// <summary>아이템을 주워 가방에 넣음. name = 아이템명</summary>
    ItemPickedUp,
    /// <summary>가방이 가득 차 줍지 못했다. name = 아이템명</summary>
    BagFull,
    /// <summary>줍지 않고 아이템 위에 올라섰다. name = 아이템명</summary>
    SteppedOnItem,
    /// <summary>함정이 작동했다. name = 함정 이름</summary>
    TrapTriggered,
    /// <summary>함정을 밟았지만 아무 일도 없었다. name = 함정 이름</summary>
    TrapDidNothing,
    /// <summary>만복도 20% 이하.</summary>
    HungerRumble,
    /// <summary>만복도 10% 이하.</summary>
    HungerDizzy,
    /// <summary>만복도가 바닥나기 두 번 전.</summary>
    HungerLimit,
    /// <summary>만복도가 바닥나기 한 번 전.</summary>
    HungerMustEat,
    /// <summary>만복도가 바닥났다.</summary>
    HungerStarved,
    /// <summary>발밑에 아무것도 없다.</summary>
    NothingUnderFoot,
    /// <summary>발밑의 아이템을 주웠다. name = 아이템명</summary>
    ItemPickedUpFromGround,
    /// <summary>무엇과 바꿀지 고르라는 물음.</summary>
    SwapWithWhat,
    /// <summary>발밑의 것과 가방의 것을 바꿨다. name = 가방에서 내놓은 것, secondName = 바닥에서 집어 든 것</summary>
    ItemSwapped,
    /// <summary>장비를 착용했다. name = 아이템명</summary>
    Equipped,
    /// <summary>장비를 벗었다. name = 아이템명</summary>
    Unequipped,
    /// <summary>적 처치. name = 적 이름, amount = 획득 경험치</summary>
    EnemyDefeated,

    /// <summary>동료가 적을 때렸다. name = 동료 이름, amount = 피해량</summary>
    PartnerHitEnemy,
    /// <summary>동료가 맞았다. name = 동료 이름, amount = 피해량</summary>
    PartnerDamaged,
    /// <summary>동료가 쓰러졌다. name = 동료 이름</summary>
    PartnerFell,
    /// <summary>계단에서 동료를 두고 내려왔다. name = 동료 이름</summary>
    PartnerLeftBehind,
    /// <summary>동행을 승낙받았다. name = 동료 이름</summary>
    PartnerJoined,
    /// <summary>동행을 거절했다(플레이어가 아니오를 골랐다). name = 동료 이름</summary>
    PartnerDeclined,
    /// <summary>
    /// 동료가 적을 쓰러뜨렸다. 경험치는 동료만 받는다.
    /// name = 동료 이름, secondName = 적 이름, amount = 동료가 받은 경험치
    /// </summary>
    PartnerDefeatedEnemy,
    /// <summary>동료의 레벨이 올랐다. name = 동료 이름</summary>
    PartnerLeveledUp,
    /// <summary>동료에게 말을 걸었다. 다음 레벨까지 남은 경험치. amount = 남은 양</summary>
    PartnerExpRemaining,
    /// <summary>동료가 회복 아이템을 맞고 회복했다. name = 동료 이름</summary>
    PartnerHealed,

    /// <summary>포만도가 이미 최대였음 → 위장이 넓어짐.</summary>
    AteFoodStomachGrew,
    /// <summary>포만도 회복.</summary>
    AteFoodFilled,
    /// <summary>체력이 이미 최대였음 → 최대 체력 증가.</summary>
    AteHealMaxHpUp,
    /// <summary>체력 회복.</summary>
    AteHealRecovered,
    /// <summary>기력(힘) 회복.</summary>
    DrankStrengthRestored,
    /// <summary>최대 힘 증가.</summary>
    UsedMaxStrengthUp,

    /// <summary>아이템을 발밑에 내려놓음.</summary>
    ItemDropped,
    /// <summary>아이템을 던져서 떨어뜨림.</summary>
    ItemThrown,
    /// <summary>던진 것이 물에 빠졌다. name = 아이템명</summary>
    ItemSankIntoWater,
    /// <summary>던진 것이 용암에 타 없어졌다. name = 아이템명</summary>
    ItemBurnedAway,
    /// <summary>던진 것이 구멍으로 떨어져 사라졌다. name = 아이템명</summary>
    ItemFellFarAway,
    /// <summary>놓을 자리가 없어 사라졌다. name = 아이템명</summary>
    ItemBuriedAmongItems,
    /// <summary>던진 아이템이 벽에 부딪힘.</summary>
    ItemHitWall,

    /// <summary>날리기 함정에 밀려 벽에 처박혔다.</summary>
    BlownIntoWall,
    /// <summary>날려져 물에 빠졌다.</summary>
    BlownIntoWater,
    /// <summary>날려져 용암에 빠졌다.</summary>
    BlownIntoLava,
    /// <summary>날려지다 적과 부딪혔다. name = 적 이름</summary>
    BlownIntoEnemy,

    /// <summary>상태이상에 걸렸다. name = 상태 이름</summary>
    ConditionApplied,
    /// <summary>상태이상이 풀렸다. name = 상태 이름</summary>
    ConditionEnded,
    /// <summary>힘이 깎였다. amount = 깎인 양</summary>
    StrengthLost,
    /// <summary>잠들어 있어 이번 턴에 아무것도 하지 못했다.</summary>
    Sleeping,

    /// <summary>끈적끈적해서 쓸 수 없다. name = 아이템명</summary>
    StickyUnusable,
    /// <summary>차고 있는 것이 끈적끈적해서 손에서 떨어지지 않는다. name = 아이템명</summary>
    StickyStuck,
    /// <summary>아이템이 끈적끈적해졌다. name = 아이템명</summary>
    BecameSticky,

    /// <summary>던진 물건이 적에게 맞았다. name = 적 이름, amount = 피해량</summary>
    ThrownItemHitEnemy,
    /// <summary>회복 아이템을 맞고 적이 회복했다. name = 적 이름</summary>
    ThrownItemHealedEnemy,
    /// <summary>능력치 아이템을 맞고 적이 강해졌다. name = 적 이름</summary>
    ThrownItemBuffedEnemy,
    /// <summary>몬스터 하우스 최초 입장.</summary>
    MonsterHouse,

    ShopGreeting,
    ShopBuyPrompt,
    ShopSellPrompt,
    ShopPaid,
    ShopReturned,
    ShopNoGold,
    ShopSold,
    ShopTheft,
    ShopAngry,
    ShopNoSpace,

    ShopPayFirst,

    ShopWelcome,

}

/// <summary>
/// GameMessage 를 실제 문구로 바꾸는 유일한 장소.
///
/// 이 클래스는 '문자열을 만드는 일'만 한다. 패널을 켜거나, 경험치를 주거나,
/// 코루틴을 돌리는 부수효과는 전부 호출하는 쪽(GameManager)의 책임이다.
/// 덕분에 문구를 고칠 때 게임 로직을 건드릴 일이 없다.
/// </summary>
public static class MessageCatalog
{
    /// <summary>
    /// secondName 은 이름이 둘 필요한 문구에만 쓴다(교환처럼 주고받는 경우).
    /// 하나로 충분한 문구가 대부분이라 기본값을 두어 부르는 쪽을 건드리지 않았다.
    /// </summary>
    public static string Format(GameMessage message, string name = null, int amount = 0, string secondName = null)
    {
        switch (message)
        {
            case GameMessage.MonsterHouse:
                return "몬스터 하우스다!";
            case GameMessage.ShopPayFirst: return "상인에게 먼저 계산해 주세요.";
            case GameMessage.ShopWelcome: return "어서 오세요! 상점에 오신 것을 환영합니다!";
            case GameMessage.ShopGreeting: return $"어서 오세요. 상품을 직접 주운 뒤 저에게 계산해 주세요. 바닥에 놓은 물건은 매입도 합니다.";
            case GameMessage.ShopBuyPrompt: return $"상품 대금 {amount}G입니다. 구매하시겠습니까?";
            case GameMessage.ShopSellPrompt: return $"{name}의 매입가는 {amount}G입니다. 판매하시겠습니까?";
            case GameMessage.ShopPaid: return $"{amount}G 받았습니다. 감사합니다! 길을 비켜드리겠습니다.";
            case GameMessage.ShopReturned: return $"구매하지 않은 상품을 가방에서 꺼내 진열대로 돌려놓았다.";
            case GameMessage.ShopNoGold: return $"돈이 부족합니다. 상품을 돌려받겠습니다.";
            case GameMessage.ShopSold: return $"{amount}G에 매입했습니다. 이제 제 상품입니다.";
            case GameMessage.ShopTheft: return $"도둑이다! 상품 값을 치르지 않고 도망치다니!";
            case GameMessage.ShopAngry: return $"감히 상인을 공격하다니! 용서하지 않겠다!";
            case GameMessage.ShopNoSpace: return $"상품을 돌려놓을 자리가 없습니다. 바닥을 비운 뒤 다시 말씀해 주세요.";
            case GameMessage.PlayerDamaged:
                return $"플레이어에게 {amount}데미지!";
            case GameMessage.PlayerCriticallyDamaged:
                return $"회심의 일격! 플레이어에게 {amount}데미지!";
            case GameMessage.AttackMissed:
                return "그러나 공격은 빗나갔다";
            case GameMessage.CriticalHit:
                return $"회심의 일격! 적에게 {amount}데미지!";
            case GameMessage.EnemyDamaged:
                return $"적에게 {amount}데미지!";
            case GameMessage.StairsPrompt:
                return "지하로 내려가겠습니까?";
            case GameMessage.GoldPickedUp:
                return $"{amount}골드를 획득했습니다.";
            case GameMessage.ItemPickedUp:
                return $"{name}을(를) 주워서 가방안에 넣었다.";
            // 무엇을 못 주웠는지 말해 준다. 예전에는 "더 이상 넣을 수 없다" 뿐이라,
            // 여러 개가 겹쳐 놓인 칸에서는 어느 것이 남았는지 알 수 없었다.
            case GameMessage.BagFull:
                return $"가방이 가득차 {name}을(를) 줍지 못했다.";
            case GameMessage.SteppedOnItem:
                return $"{name} 위에 올라탔다.";
            case GameMessage.TrapTriggered:
                return $"{name}이(가) 작동했다!";
            case GameMessage.TrapDidNothing:
                return $"{name}을(를) 밟았지만 아무 일도 일어나지 않았다.";
            case GameMessage.HungerRumble:
                return "배에서 꼬르륵 소리가 났다.";
            case GameMessage.HungerDizzy:
                return "배가 고파서 눈앞이 아찔해지기 시작했다!";
            case GameMessage.HungerLimit:
                return "이제 한계다!";
            case GameMessage.HungerMustEat:
                return "뭔가 먹질 않으면..";
            case GameMessage.HungerStarved:
                return "아사해 버린다!";
            case GameMessage.NothingUnderFoot:
                return "발밑에는 아무것도 없다.";
            case GameMessage.ItemPickedUpFromGround:
                return $"{name}을(를) 주웠다.";
            case GameMessage.SwapWithWhat:
                return "무엇이랑 바꿀까?";
            case GameMessage.ItemSwapped:
                return $"가방의 {name}을(를) 바닥의 {secondName}(으)로 교환했다.";
            case GameMessage.Equipped:
                return $"{name}을(를) 장착했다.";
            case GameMessage.Unequipped:
                return $"{name}을(를) 벗었다.";
            case GameMessage.EnemyDefeated:
                return $"{name}를 쓰러뜨렸다.\n{amount}경험치를 얻었다.";

            case GameMessage.PartnerHitEnemy:
                return $"{name}의 공격! 적에게 {amount}데미지!";
            case GameMessage.PartnerDamaged:
                return $"{name}에게 {amount}데미지!";
            case GameMessage.PartnerFell:
                return $"{name}이(가) 쓰러졌다!";
            case GameMessage.PartnerLeftBehind:
                return $"{name}을(를) 두고 내려왔다.";
            case GameMessage.PartnerJoined:
                return $"{name}이(가) 동료가 되었다!";
            case GameMessage.PartnerDeclined:
                return $"{name}와(과) 헤어졌다.";
            case GameMessage.PartnerDefeatedEnemy:
                return $"{name}이(가) {secondName}를 쓰러뜨렸다.\n{name}이(가) {amount}경험치를 얻었다.";
            case GameMessage.PartnerLeveledUp:
                return $"{name}의 레벨이 올랐다!";
            case GameMessage.PartnerExpRemaining:
                return $"다음 레벨까지 {amount} 경험치.";
            case GameMessage.PartnerHealed:
                return $"{name}의 체력이 회복됐다.";

            case GameMessage.AteFoodStomachGrew:
                return $"{name}를 먹었다.\n위장이 넓어졌다.";
            case GameMessage.AteFoodFilled:
                return $"{name}를 먹었다.\n배가 조금 찼다.";
            case GameMessage.AteHealMaxHpUp:
                return $"{name}를 먹었다.\n체력최대치가 증가했다.";
            case GameMessage.AteHealRecovered:
                return $"{name}를 먹었다.\n체력이 회복됬다.";
            case GameMessage.DrankStrengthRestored:
                return $"{name}를 마셨다.\n힘이 돌아왔다.";
            case GameMessage.UsedMaxStrengthUp:
                return $"{name}를 사용했다.\n힘이 세진것 같다.";

            case GameMessage.ItemDropped:
                return $"{name}를 땅에 두었다.";
            case GameMessage.ItemThrown:
                return $"{name}은 땅에 떨어졌다.";
            case GameMessage.ItemSankIntoWater:
                return $"{name}은 물속에 풍덩 빠져 버렸다.";
            case GameMessage.ItemBurnedAway:
                return $"{name}은 흔적도 없이 타버렸다.";
            case GameMessage.ItemFellFarAway:
                return $"{name}은 보이지 않는 곳까지 멀리 떨어져 버렸다.";
            case GameMessage.ItemBuriedAmongItems:
                return $"{name}은 아이템속에 파묻혀 사라져 버렸다";
            case GameMessage.ItemHitWall:
                return $"{name}벽에 부딪혀 땅에 떨어졌다.";

            case GameMessage.BlownIntoWall:
                return "벽에 세게 부딪혔다!";
            case GameMessage.BlownIntoWater:
                return "물에 빠져 떠내려갔다!";
            case GameMessage.BlownIntoLava:
                return "용암에 빠졌다!";
            case GameMessage.BlownIntoEnemy:
                return $"{name}와(과) 세게 부딪혔다!";

            case GameMessage.ConditionApplied:
                return $"{name} 상태가 되었다!";
            case GameMessage.ConditionEnded:
                return $"{name} 상태가 풀렸다.";
            case GameMessage.StrengthLost:
                return $"힘이 {amount} 깎였다!";
            case GameMessage.Sleeping:
                return "쿨쿨 자고 있다.";

            case GameMessage.StickyUnusable:
                return $"{name}은(는) 끈적끈적해서 쓸 수 없다!";
            case GameMessage.StickyStuck:
                return $"{name}이(가) 손에 붙어 떨어지지 않는다!";
            case GameMessage.BecameSticky:
                return $"{name}에 끈적한 액체가 묻어버렸다!";

            case GameMessage.ThrownItemHitEnemy:
                return $"{name}에게 명중! {amount}데미지!";
            case GameMessage.ThrownItemHealedEnemy:
                return $"{name}의 체력이 회복됐다.";
            case GameMessage.ThrownItemBuffedEnemy:
                return $"{name}이(가) 강해진 것 같다.";

            default:
                return string.Empty;
        }
    }
}
