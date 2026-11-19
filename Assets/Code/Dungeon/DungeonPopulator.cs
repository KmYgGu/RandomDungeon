using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다 만들어진 층에 계단 / 아이템 / 몬스터 / 함정 / 플레이어를 놓는다.
/// 방을 만들거나 타일을 칠하는 일은 하지 않는다.
///
/// 방의 종류(RoomKind)에 따라 놓는 양이 달라지는 것도 여기서 정한다.
/// </summary>
public class DungeonPopulator
{
    /// <summary>
    /// 빈자리를 찾는 반복 횟수 상한. 조건을 만족하는 칸이 없을 때
    /// 영영 돌지 않도록 반드시 필요하다.
    /// </summary>
    private const int MaxPlacementAttempts = 50;

    /// <summary>서로 이만큼은 떨어뜨려 놓는다.</summary>
    private const float MinDistanceBetweenObjects = 1f;

    /// <summary>함정을 문에서 이만큼 떨어뜨린다. 문 앞에서 즉사하는 일을 막는다.</summary>
    private const float MinDistanceFromDoors = 1.5f;

    /// <summary>통합 몬스터 하우스의 아이템 배수.</summary>
    private const int MonsterHouseItemMultiplier = 3;

    // 적을 몇 마리 놓을지는 EnemyBudget 이 정한다. 숫자만 다루는 규칙이라
    // 그쪽에 두어야 Unity 없이 확인할 수 있다.

    /// <summary>이번 층에서 일반 방에 이미 놓은 적 수. 층 상한을 지키는 데 쓴다.</summary>
    private int enemiesPlacedThisFloor;
    public List<MonsterHouseEncounter> MonsterHouses { get; } = new List<MonsterHouseEncounter>();

    private readonly DungeonTilePainter painter;
    private readonly GroundObjectRegistry ground;

    private readonly GameObject[] itemPrefabs;
    private readonly GameObject[] trapPrefabs;
    private readonly GameObject monsterPrefab;
    private readonly GameObject merchantPrefab;
    private readonly GameObject stairsPrefab;
    private readonly GameObject playerPrefab;

    private readonly List<Vector3Int> occupiedByCreatures = new List<Vector3Int>();
    private readonly List<Vector3Int> occupiedByItems = new List<Vector3Int>();
    private Vector3Int stairsPosition;

    public DungeonPopulator(
        DungeonTilePainter painter,
        GroundObjectRegistry ground,
        GameObject[] itemPrefabs,
        GameObject[] trapPrefabs,
        GameObject monsterPrefab,
        GameObject stairsPrefab,
        GameObject playerPrefab,
        GameObject merchantPrefab = null)
    {
        this.painter = painter;
        this.ground = ground;
        this.itemPrefabs = itemPrefabs;
        this.trapPrefabs = trapPrefabs;
        this.monsterPrefab = monsterPrefab;
        this.merchantPrefab = merchantPrefab != null ? merchantPrefab : monsterPrefab;
        this.stairsPrefab = stairsPrefab;
        this.playerPrefab = playerPrefab;

        WarnAboutUnspawnableItems();
    }

    private Vector3Int startPosition;
    private bool hasReservedStart;
    public Vector3Int StairsPosition => stairsPosition;

    public void ReserveStart(Vector3Int cell)
    {
        startPosition = cell;
        hasReservedStart = true;
        occupiedByCreatures.Add(cell);
    }

    private bool InStartArea(Vector3Int cell)
        => hasReservedStart && Mathf.Abs(cell.x - startPosition.x) <= 1
            && Mathf.Abs(cell.y - startPosition.y) <= 1;

    public void PlaceStairs(Vector3Int cell)
    {
        stairsPosition = cell;
        SpawnOnGround(stairsPrefab, cell);
    }

    public GameObject PlacePlayer(Vector3Int cell)
        => UnityEngine.Object.Instantiate(playerPrefab, painter.CellCenter(cell), Quaternion.identity);

    /// <summary>방 하나를 채운다. 방의 종류에 따라 양이 달라진다.</summary>
    public void PopulateRoom(RoomInfo room, int baseItems, int baseMonsters, int stage, float minDistanceFromStairs)
    {
        if (room.Kind == RoomKind.Shop)
        {
            if (PopulateShop(room)) return;

            // 상점을 차릴 수 없는 방이었다. 평범한 방으로 되돌려 아래에서 채운다.
            room.Kind = RoomKind.Normal;
        }
        // 이전 버전의 보물방 값이 전달되어도 동일한 하우스로 취급한다.
        if (room.Kind == RoomKind.Treasure) room.Kind = RoomKind.MonsterHouse;
        int items = baseItems;

        // 방당 수와 상한은 EnemyBudget 이 정한다. 층이 깊어져도 끝없이 늘지 않는다.
        int monsters = EnemyBudget.ForRoom(baseMonsters, stage);

        // 몬스터 하우스는 층 상한과 따로 센다(EnemyBudget 주석).
        bool countsTowardFloorLimit = true;

        switch (room.Kind)
        {
            case RoomKind.MonsterHouse:
                // 몬스터 하우스: 적이 몰려 있지만 보상도 크다.
                monsters = EnemyBudget.ForMonsterHouse(baseMonsters, stage);
                items = Mathf.Max(3, items * MonsterHouseItemMultiplier);
                countsTowardFloorLimit = false;
                break;
        }

        if (countsTowardFloorLimit)
            monsters = EnemyBudget.AllowedThisFloor(monsters, enemiesPlacedThisFloor);

        SpawnItems(room, items, minDistanceFromStairs);

        MonsterHouseEncounter house = null;
        if (room.Kind == RoomKind.MonsterHouse)
        {
            house = new MonsterHouseEncounter(room.InteriorCellRect);
            MonsterHouses.Add(house);
        }
        int placed = SpawnMonsters(room, monsters, stage, house);
        if (countsTowardFloorLimit) enemiesPlacedThisFloor += placed;
    }

    /// <summary>
    /// 방을 상점으로 차린다 — 가운데에 물건을 늘어놓고 상인을 세운다.
    /// 차릴 수 없는 방이면 <b>아무것도 놓지 않고</b> false 를 돌려준다.
    ///
    /// 어느 방이 상점이고 어디에 늘어놓는지는 ShopPlan 이 정했다. 여기서는 그
    /// 자리가 실제로 비어 있는지 보고 오브젝트를 만든다. 확인을 전부 마친 뒤에야
    /// 만들기 시작하므로, 도중에 그만두어 반쯤 차린 상점이 남는 일이 없다.
    /// </summary>
    private bool PopulateShop(RoomInfo room)
    {
        // 팔 수 있는 것 = 값이 있고 바닥에도 나오는 것. 돈과 값 없는 물건은 빠진다.
        var stockPrefabs = new List<GameObject>();
        foreach (GameObject prefab in itemPrefabs)
        {
            ItemDescription description = ItemInstance.DescriptionOf(prefab);
            if (description != null && description.SellPrice > 0 && DropRateOf(prefab) > 0)
                stockPrefabs.Add(prefab);
        }

        List<Vector3Int> display = ShopPlan.DisplayCells(room.InteriorCellRect);
        if (stockPrefabs.Count == 0 || display.Count < 4) return false;

        // 진열할 칸이 전부 빈 바닥이어야 한다. 계단이나 먼저 놓인 물건이 끼어
        // 있으면 그 방은 상점이 되지 못한다.
        foreach (Vector3Int cell in display)
        {
            if (painter.IsEmpty(cell) || !painter.IsWalkable(cell)) return false;
            if (cell == stairsPosition || occupiedByItems.Contains(cell)) return false;
        }

        // 상인은 문 바로 안쪽 칸에 가장 가까운 빈 칸에 선다. 그 칸 자체는 비워
        // 둔다 — 거기 서면 빚이 없는 손님까지 드나들지 못한다.
        // 문이 하나뿐인 방만 상점이 되므로(ShopPlan.Eligible) 첫 문이 곧 그 문이다.
        Door door = room.Doors[0];
        Vector3Int inward = door.Position - (door.Outside - door.Position);

        Vector3Int waiting = default;
        bool found = false;

        foreach (Vector2Int cell in room.InteriorCellRect.allPositionsWithin)
        {
            var spot = new Vector3Int(cell.x, cell.y, 0);

            if (display.Contains(spot) || spot == inward || spot == stairsPosition) continue;
            if (!IsFreeOfCreatures(spot)) continue;

            if (!found || (spot - inward).sqrMagnitude < (waiting - inward).sqrMagnitude)
            {
                waiting = spot;
                found = true;
            }
        }

        if (!found) return false;

        // 여기부터는 되돌릴 수 없다. 확인은 위에서 모두 끝났다.
        ShopRoom shop = new GameObject("Shop room").AddComponent<ShopRoom>();
        shop.Configure(room.InteriorCellRect, door.Position, waiting);

        GameObject merchantObject = UnityEngine.Object.Instantiate(
            merchantPrefab, painter.CellCenter(waiting), Quaternion.identity);

        Merchant merchant = merchantObject.AddComponent<Merchant>();
        merchant.Bind(shop);
        shop.Merchant = merchant;
        occupiedByCreatures.Add(waiting);

        // 무엇을 놓을지는 바닥에 떨어지는 것과 같은 가중치로 뽑는다.
        var weights = new List<float>(stockPrefabs.Count);
        foreach (GameObject prefab in stockPrefabs) weights.Add(DropRateOf(prefab));

        float total = WeightedPick.Total(weights);

        foreach (Vector3Int cell in display)
        {
            int index = WeightedPick.Choose(weights, UnityEngine.Random.Range(0f, total));

            GameObject item = SpawnOnGround(stockPrefabs[index], cell);
            EnhanceIfEquipment(item);
            occupiedByItems.Add(cell);

            // 값은 그 물건의 시세 그대로다. 상인은 절반에 사서 제값에 판다(ShopLedger.BuyOffer).
            item.AddComponent<ShopGood>()
                .Bind(shop, cell, ItemInstance.DescriptionOf(item).SellPrice);
        }

        return true;
    }

    private void SpawnItems(RoomInfo room, int count, float minDistanceFromStairs)
    {
        for (int i = 0; i < count; i++)
        {
            bool IsSuitable(Vector3Int cell)
                => cell != stairsPosition && !InStartArea(cell)
                   && Vector3Int.Distance(cell, stairsPosition) >= minDistanceFromStairs
                   && !occupiedByItems.Contains(cell);

            if (!TryFindSpot(room, IsSuitable, out Vector3Int spot)) continue;

            occupiedByItems.Add(spot);
            GameObject prefab = PickWeightedItem();
            if (prefab == null) continue;

            EnhanceIfEquipment(SpawnOnGround(prefab, spot));
        }
    }

    /// <summary>
    /// 새로 놓은 장비에 강화 수치를 굴려 붙인다. 장비가 아니면 아무 일도 없다.
    ///
    /// 프리팹이 아니라 <b>방금 만든 개체</b>에 적는다. 프리팹에 적으면 그 종류가
    /// 전부 같은 값이 되고, 에디터에서는 프리팹 애셋 자체가 바뀌어 버린다.
    ///
    /// 굴리는 것은 여기서 하고 규칙은 ItemEnhancement 가 안다. 그래야 Unity 를
    /// 켜지 않고도 규칙 쪽을 확인할 수 있다.
    /// </summary>
    private static void EnhanceIfEquipment(GameObject item)
    {
        if (item == null) return;

        ItemInstance instance = item.GetComponent<ItemInstance>();
        if (instance == null || instance.description == null) return;

        // 어느 수치가 얼마나 흔한지는 ItemEnhancement 가 정한다(낮을수록 흔하다).
        // 여기서는 0~1 사이의 수 하나만 굴려 넘긴다.
        instance.enhancement = ItemEnhancement.OnSpawn(
            instance.description.Definition, UnityEngine.Random.value);
    }

    /// <summary>
    /// 실제로 놓은 마릿수를 돌려준다.
    ///
    /// 자리를 못 찾아 못 놓는 경우가 있으므로(좁은 방·이미 찬 칸) <b>놓으려 한 수가
    /// 아니라 놓은 수</b>를 세야 층 상한이 맞는다.
    /// </summary>
    private int SpawnMonsters(RoomInfo room, int count, int stage, MonsterHouseEncounter house = null)
    {
        int placed = 0;

        for (int i = 0; i < count; i++)
        {
            if (!TryFindSpot(room, cell => IsFreeOfCreatures(cell) && !InStartArea(cell)
                && cell != stairsPosition, out Vector3Int spot)) continue;

            occupiedByCreatures.Add(spot);

            GameObject monster = UnityEngine.Object.Instantiate(
                monsterPrefab, painter.CellCenter(spot), Quaternion.identity);

            Enemy enemy = monster.GetComponent<Enemy>();
            SetMonsterStats(enemy, stage);
            if (enemy != null)
            {
                if (house != null)
                {
                    enemy.ConfigureSpawnSleep(1f, 1f);
                    house.Add(enemy);
                }
                else enemy.ConfigureSpawnSleep(UnityEngine.Random.value, UnityEngine.Random.value);
            }
            placed++;
        }

        return placed;
    }

    /// <summary>
    /// 적의 수치를 정한다. <b>시트에 적힌 값을 그대로 쓴다.</b>
    ///
    /// 예전에는 SetStats(5, power, power - stage) 로 세 숫자가 전부 여기 박혀
    /// 있었다. Monster 시트에 hp/atk/def 를 적어 두어도 쓰이지 않았고,
    /// 적을 한 종류 더 만들려면 이 줄을 고쳐야 했다.
    ///
    /// ── 층 보정을 두지 않는 이유 ──────────────────────────────────
    /// 난이도는 같은 적을 층마다 부풀려서가 아니라 <b>어떤 적이 나오는가</b>로
    /// 조절한다. 보정을 함께 두면 한 마리가 얼마나 센지가 시트만 봐서는
    /// 알 수 없어지고, 표의 숫자를 고쳐도 실제 세기는 층마다 달라진다.
    /// 깊은 층을 어렵게 하려면 센 적을 표에 더한다.
    /// </summary>
    private static void SetMonsterStats(Enemy enemy, int stage)
    {
        if (enemy == null) return;

        MonsterDefinition definition =
            enemy.description != null ? enemy.description.Definition : null;

        if (definition == null)
        {
            // 정의가 아직 연결되지 않은 프리팹. 옛 방식 그대로 둔다.
            int power = UnityEngine.Random.Range(1 + stage, 3 + stage);
            enemy.SetStats(5, power, power - stage);
            return;
        }

        enemy.SetStats(definition.Hp, definition.Attack, definition.Defense);
    }

    /// <summary>
    /// 명단에 있는 동료를 플레이어 옆에 만든다. 층마다 다시 불린다.
    ///
    /// ── 처음에는 남이다 ───────────────────────────────────────────────
    /// 놓기만 하고 <b>명단에는 올리지 않는다.</b> 말을 걸어 승낙을 받아야 동료가
    /// 된다(GameManager.PromptJoin). 그전까지는 함께 싸우지도, 따라오지도 않고
    /// 적도 건드리지 않는다.
    ///
    /// ── 지금은 시작하자마자 옆에 하나 선다 ────────────────────────────
    /// 원래는 던전에서 확률로 만나는 것이지만, 지금은 <b>따라오기·자리 바꾸기·
    /// 전투·계단을 자주 확인해야 해서</b> 첫 층부터 옆에 세워 둔다. 확률 출현으로
    /// 바꿀 때는 아래 "명단이 비었으면 하나 세운다" 를 그 규칙으로 갈면 된다.
    ///
    /// <b>이미 데리고 있으면 새로 세우지 않는다.</b> 층마다 하나씩 늘어나면
    /// 금세 줄줄이 달고 다니게 되어 자리 바꾸기도 통행도 확인할 수 없다.
    /// </summary>
    public void PlacePartners(GameObject player, GameObject partnerPrefab)
    {
        if (player == null || partnerPrefab == null) return;

        // 데리고 있는 동료들을 다시 세운다(층을 넘어온 것들).
        foreach (PartnerMember member in GameManager.Party.Members)
            SpawnPartnerBeside(player, partnerPrefab, member.Id);

        // 아무도 안 데리고 있으면 남 하나를 세워 둔다.
        if (GameManager.Party.IsEmpty)
            SpawnPartnerBeside(player, partnerPrefab, 0);
    }

    /// <summary>
    /// 플레이어 옆에 동료 하나를 세운다.
    /// </summary>
    /// <param name="expectedId">
    /// 명단에서 온 번호. 0 이면 아직 남이라 맞춰 볼 것이 없다.
    /// </param>
    private void SpawnPartnerBeside(GameObject player, GameObject partnerPrefab, int expectedId)
    {
        if (!TryFindSpotBeside(player.transform.position, out Vector3Int spot))
        {
            Debug.LogWarning("동료를 놓을 빈 칸이 없어 이번 층 배치를 건너뜁니다.");
            return;
        }

        GameObject spawned = UnityEngine.Object.Instantiate(
            partnerPrefab, painter.CellCenter(spot), Quaternion.identity);

        occupiedByCreatures.Add(spot);

        // 명단에 적힌 것이 신원이다. 프리팹이 다른 동료를 가리키고 있으면
        // 체력이 딴 사람 것이 되는데, 에러는 나지 않는다.
        Partner partner = spawned.GetComponent<Partner>();
        int actualId = partner != null && partner.description != null ? partner.description.id : 0;

        if (expectedId != 0 && actualId != expectedId)
        {
            Debug.LogWarning(
                $"동료 프리팹({actualId})과 명단({expectedId})이 다릅니다. " +
                "프리팹의 Description 을 확인하세요.", spawned);
        }
    }

    /// <summary>옆 칸을 살피는 네 방향. 탐색이 칸마다 새로 만들지 않도록 한 번만 둔다.</summary>
    private static readonly Vector3Int[] CardinalSteps =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
    };

    /// <summary>플레이어에서 연결된 빈 칸만 고른다. 자리가 없으면 겹쳐 놓지 않는다.</summary>
    private bool TryFindSpotBeside(Vector3 playerWorld, out Vector3Int spot)
    {
        var center = new Vector3Int(Mathf.FloorToInt(playerWorld.x), Mathf.FloorToInt(playerWorld.y), 0);
        // 이웃이 찼어도 플레이어 위에 겹치지 않고, 연결된 가장 가까운 칸을 찾는다.
        var queue = new Queue<Vector3Int>();
        var seen = new HashSet<Vector3Int> { center };
        queue.Enqueue(center);
        while (queue.Count > 0 && seen.Count <= 4096)
        {
            var cell = queue.Dequeue();
            foreach (Vector3Int direction in CardinalSteps)
            {
                var candidate = cell + direction;
                if (!seen.Add(candidate) || painter.IsEmpty(candidate) || !painter.IsWalkable(candidate)) continue;
                if (candidate != stairsPosition && IsFreeOfCreatures(candidate))
                {
                    spot = candidate;
                    return true;
                }
                queue.Enqueue(candidate);
            }
        }
        spot = default;
        return false;
    }
    /// <summary>일반 방은 함정 하나, 하우스는 최대 네 개. 계단·아이템·문 근처는 피한다.</summary>
    public void PlaceTraps(DungeonLayout layout, float minDistanceFromStairs, float minDistanceFromItems)
    {
        if (trapPrefabs == null || trapPrefabs.Length == 0) return;
        foreach (RoomInfo room in layout.Rooms)
        {
            bool IsSuitable(Vector3Int cell)
            {
                if (cell == stairsPosition || InStartArea(cell) || !IsFreeOfCreatures(cell)) return false;
                if (Vector3Int.Distance(cell, stairsPosition) < minDistanceFromStairs) return false;
                if (IsNear(occupiedByItems, cell, minDistanceFromItems)) return false;

                foreach (Door door in room.Doors)
                {
                    if (Vector3Int.Distance(cell, door.Position) < MinDistanceFromDoors)
                        return false;
                }
                return true;
            }

            int trapCount = room.Kind == RoomKind.MonsterHouse ? 4 : 1;
            var trapCells = new HashSet<Vector3Int>();
            for (int i = 0; i < trapCount; i++)
            {
                if (!TryFindSpot(room, cell => !trapCells.Contains(cell) && IsSuitable(cell), out Vector3Int spot)) continue;
                trapCells.Add(spot);
                GameObject trap = trapPrefabs[UnityEngine.Random.Range(0, trapPrefabs.Length)];
                SpawnOnGround(trap, spot);
            }
        }
    }

    // ── 공통 ───────────────────────────────────────────────────────────────

    private bool TryFindSpot(RoomInfo room, Func<Vector3Int, bool> isSuitable, out Vector3Int spot)
    {
        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            spot = room.RandomInteriorCell();
            if (!painter.IsEmpty(spot) && painter.IsWalkable(spot) && isSuitable(spot)) return true;
        }

        // 무작위 50회가 같은 칸만 뽑더라도 남은 빈자리를 놓치지 않는다.
        for (int x = room.InteriorMinX; x <= room.InteriorMaxX; x++)
        for (int y = room.InteriorMinY; y <= room.InteriorMaxY; y++)
        {
            spot = new Vector3Int(x, y, 0);
            if (!painter.IsEmpty(spot) && painter.IsWalkable(spot) && isSuitable(spot)) return true;
        }
        spot = default;
        return false;
    }

    private bool IsFreeOfCreatures(Vector3Int position)
        => !IsNear(occupiedByCreatures, position, MinDistanceBetweenObjects);

    private static bool IsNear(List<Vector3Int> positions, Vector3Int target, float threshold)
    {
        foreach (Vector3Int position in positions)
        {
            if (Vector3Int.Distance(position, target) < threshold)
                return true;
        }
        return false;
    }

    /// <summary>적의 발밑이나 주변 바닥에 한 개. 기존 아이템·함정·계단과 겹치지 않는다.</summary>
    public GameObject DropEnemyItem(Vector3 world)
    {
        Vector3 origin = painter.CellCenter(painter.WorldToCell(world));
        bool Blocked(Vector3 candidate)
        {
            Vector3Int cell = painter.WorldToCell(candidate);
            return painter.IsEmpty(cell) || !painter.IsWalkable(cell) || ground.Contains(painter.CellCenter(cell));
        }
        if (!ItemThrower.TryFindFreeSpot(origin, Blocked, out Vector3 spot)) return null;
        GameObject prefab = PickWeightedItem();
        if (prefab == null) return null;
        GameObject item = SpawnOnGround(prefab, painter.WorldToCell(spot));
        EnhanceIfEquipment(item);
        MinimapOverlay.TrackSpawnedItem(item);
        return item;
    }

    private GameObject SpawnOnGround(GameObject prefab, Vector3Int cell)
    {
        ground.Add(GridUtil.CellCenter(cell));
        return UnityEngine.Object.Instantiate(prefab, painter.CellCenter(cell), Quaternion.identity);
    }

    /// <summary>
    /// 가중치에 따라 아이템 종류를 고른다. 나올 것이 없으면 null.
    ///
    /// 가중치는 <b>아이템 정의(엑셀 item 시트의 droprate)</b>에서 온다.
    /// 예전에는 인스펙터의 배열 둘을 번호로 짝지었는데, 프리팹만 늘리고
    /// 가중치를 안 늘리면 새 아이템이 영영 안 나왔다. 실제로 장비 3종을
    /// 더했을 때 그렇게 됐고, 에러는 나지 않았다.
    /// </summary>
    private GameObject PickWeightedItem()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return null;

        var weights = new List<float>(itemPrefabs.Length);
        foreach (GameObject prefab in itemPrefabs)
            weights.Add(DropRateOf(prefab));

        float total = WeightedPick.Total(weights);
        if (total <= 0f)
        {
            // 하나도 안 나오는 층이 되어 버린다. 대개 시트에 droprate 를
            // 안 적었거나 프리팹에 정의를 안 이어 둔 것이다.
            Debug.LogWarning("바닥에 놓을 아이템의 가중치가 모두 0 입니다. item 시트의 droprate 를 확인하세요.");
            return null;
        }

        int picked = WeightedPick.Choose(weights, UnityEngine.Random.Range(0f, total));
        return picked >= 0 ? itemPrefabs[picked] : null;
    }

    /// <summary>이 프리팹이 바닥에 나올 가중치. 정의를 못 찾으면 0.</summary>
    private static float DropRateOf(GameObject prefab)
    {
        // 돈은 item 시트의 한 줄이 아니다(GoldPile.DropGrade 주석). 정의 애셋이
        // 없으므로 여기서 갈라 주지 않으면 가중치가 0 이 되어 영영 나오지 않는다.
        if (GoldPile.IsGold(prefab))
            return ItemSheet.ParseDropRate(GoldPile.DropGrade, 0);

        ItemDescription description = ItemInstance.DescriptionOf(prefab);
        if (description == null) return 0f;

        ItemDefinition definition = description.Definition;
        return definition != null ? definition.DropRate : 0f;
    }

    /// <summary>
    /// 바닥에 <b>절대 나오지 않는</b> 아이템 프리팹을 알린다.
    ///
    /// 가중치가 0 이면 그 프리팹은 영영 뽑히지 않는데 에러는 나지 않는다.
    /// 실제로 동전이 이렇게 사라졌다. 다른 아이템이 멀쩡히 나오면 아래
    /// "가중치가 모두 0" 검사에는 걸리지 않으므로, 프리팹마다 따로 본다.
    ///
    /// 층을 만들 때마다 한 번씩 뜬다. 고치기 전까지 계속 눈에 띄어야 한다 —
    /// 조용해지는 순간 다시 몇 판을 돌고 나서야 알아채게 된다.
    /// </summary>
    private void WarnAboutUnspawnableItems()
    {
        if (itemPrefabs == null) return;

        foreach (GameObject prefab in itemPrefabs)
        {
            if (prefab == null || DropRateOf(prefab) > 0f) continue;

            Debug.LogWarning(
                $"{prefab.name}: 바닥에 나오지 않습니다(가중치 0). " +
                "item 시트의 droprate 와 프리팹의 정의 연결을 확인하세요.", prefab);
        }
    }
}
