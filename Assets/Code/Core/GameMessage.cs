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
    /// <summary>가방이 가득 참.</summary>
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

    /// <summary>던진 물건이 적에게 맞았다. name = 적 이름, amount = 피해량</summary>
    ThrownItemHitEnemy,
    /// <summary>회복 아이템을 맞고 적이 회복했다. name = 적 이름</summary>
    ThrownItemHealedEnemy,
    /// <summary>능력치 아이템을 맞고 적이 강해졌다. name = 적 이름</summary>
    ThrownItemBuffedEnemy,
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
            case GameMessage.PlayerDamaged:
                return $"플레이어에게 {amount}데미지!";
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
            case GameMessage.BagFull:
                return "가방이 가득차 더 이상 넣을 수 없다.";
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
