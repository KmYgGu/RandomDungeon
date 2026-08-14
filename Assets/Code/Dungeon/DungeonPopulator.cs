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

    /// <summary>보물방의 아이템 배수와, 몬스터 하우스의 적 배수.</summary>
    private const int TreasureItemMultiplier = 3;
    private const int MonsterHouseEnemyMultiplier = 4;
    private const int MonsterHouseItemMultiplier = 2;

    /// <summary>몇 층마다 방당 적이 한 마리씩 늘어나는가.</summary>
    private const int StagesPerExtraEnemy = 4;

    private readonly DungeonTilePainter painter;
    private readonly GroundObjectRegistry ground;

    private readonly GameObject[] itemPrefabs;
    private readonly GameObject[] trapPrefabs;
    private readonly GameObject monsterPrefab;
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
        GameObject playerPrefab)
    {
        this.painter = painter;
        this.ground = ground;
        this.itemPrefabs = itemPrefabs;
        this.trapPrefabs = trapPrefabs;
        this.monsterPrefab = monsterPrefab;
        this.stairsPrefab = stairsPrefab;
        this.playerPrefab = playerPrefab;

        WarnAboutUnspawnableItems();
    }

    public Vector3Int StairsPosition => stairsPosition;

    /// <summary>계단 위치. 아이템 배치가 이 위치를 피하므로 가장 먼저 정한다.</summary>
    public void PlaceStairs(DungeonLayout layout)
    {
        if (layout.IsEmpty) return;

        stairsPosition = layout.RandomRoom().RandomInteriorCell();
        SpawnOnGround(stairsPrefab, stairsPosition);
    }

    /// <summary>방 하나를 채운다. 방의 종류에 따라 양이 달라진다.</summary>
    public void PopulateRoom(RoomInfo room, int baseItems, int baseMonsters, int stage, float minDistanceFromStairs)
    {
        int items = baseItems;
        int monsters = baseMonsters + stage / StagesPerExtraEnemy;

        switch (room.Kind)
        {
            case RoomKind.Treasure:
                // 보물방: 아이템이 많고 적은 없다.
                items *= TreasureItemMultiplier;
                if (items == 0) items = TreasureItemMultiplier;
                monsters = 0;
                break;

            case RoomKind.MonsterHouse:
                // 몬스터 하우스: 적이 몰려 있지만 보상도 크다.
                monsters *= MonsterHouseEnemyMultiplier;
                if (monsters == 0) monsters = MonsterHouseEnemyMultiplier;
                items *= MonsterHouseItemMultiplier;
                break;
        }

        SpawnItems(room, items, minDistanceFromStairs);
        SpawnMonsters(room, monsters, stage);
    }

    private void SpawnItems(RoomInfo room, int count, float minDistanceFromStairs)
    {
        for (int i = 0; i < count; i++)
        {
            bool IsSuitable(Vector3Int cell)
                => Vector3Int.Distance(cell, stairsPosition) >= minDistanceFromStairs
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

    private void SpawnMonsters(RoomInfo room, int count, int stage)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryFindSpot(room, IsFreeOfCreatures, out Vector3Int spot)) continue;

            occupiedByCreatures.Add(spot);

            GameObject monster = UnityEngine.Object.Instantiate(
                monsterPrefab, painter.CellCenter(spot), Quaternion.identity);

            SetMonsterStats(monster.GetComponent<Enemy>(), stage);
        }
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
        Vector3Int spot = FindSpotBeside(player.transform.position);

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

    /// <summary>
    /// 플레이어 곁의 빈 칸. 못 찾으면 플레이어 칸을 그대로 돌려준다.
    ///
    /// 겹쳐 놓아도 첫 턴에 동료가 알아서 비켜서지만, 그 한 턴 동안 플레이어와
    /// 겹쳐 보이는 것이 버그로 읽힌다. 여덟 이웃을 먼저 살펴본다.
    /// </summary>
    private Vector3Int FindSpotBeside(Vector3 playerWorld)
    {
        var center = new Vector3Int(
            Mathf.FloorToInt(playerWorld.x), Mathf.FloorToInt(playerWorld.y), 0);

        foreach (Vector2 direction in GridUtil.EightDirections)
        {
            var candidate = new Vector3Int(
                center.x + (int)direction.x, center.y + (int)direction.y, 0);

            if (IsNear(occupiedByCreatures, candidate, MinDistanceBetweenObjects)) continue;
            if (!painter.IsWalkable(candidate)) continue;

            return candidate;
        }

        return center;
    }

    /// <summary>플레이어가 아직 없으면(첫 층이면) 한 명 만들어 놓는다.</summary>
    public GameObject PlacePlayer(DungeonLayout layout)
    {
        if (layout.IsEmpty) return null;

        RoomInfo startRoom = PickStartRoom(layout);

        Vector3Int spot = startRoom.RandomInteriorCell();
        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            spot = startRoom.RandomInteriorCell();
            if (IsFreeOfCreatures(spot)) break;
        }

        occupiedByCreatures.Add(spot);
        return UnityEngine.Object.Instantiate(
            playerPrefab, painter.CellCenter(spot), Quaternion.identity);
    }

    /// <summary>몬스터 하우스에서 시작하지는 않도록 한다.</summary>
    private static RoomInfo PickStartRoom(DungeonLayout layout)
    {
        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            RoomInfo candidate = layout.RandomRoom();
            if (candidate.Kind != RoomKind.MonsterHouse) return candidate;
        }
        return layout.Rooms[0];
    }

    /// <summary>방마다 함정 하나. 계단·아이템·문 근처는 피한다.</summary>
    public void PlaceTraps(DungeonLayout layout, float minDistanceFromStairs, float minDistanceFromItems)
    {
        foreach (RoomInfo room in layout.Rooms)
        {
            // 보물방에는 함정을 두지 않는다. 보상을 주는 방이므로.
            if (room.Kind == RoomKind.Treasure) continue;

            bool IsSuitable(Vector3Int cell)
            {
                if (Vector3Int.Distance(cell, stairsPosition) < minDistanceFromStairs) return false;
                if (IsNear(occupiedByItems, cell, minDistanceFromItems)) return false;

                foreach (Door door in room.Doors)
                {
                    if (Vector3Int.Distance(cell, door.Position) < MinDistanceFromDoors)
                        return false;
                }
                return true;
            }

            if (!TryFindSpot(room, IsSuitable, out Vector3Int spot)) continue;

            GameObject trap = trapPrefabs[UnityEngine.Random.Range(0, trapPrefabs.Length)];
            SpawnOnGround(trap, spot);
        }
    }

    // ── 공통 ───────────────────────────────────────────────────────────────

    private static bool TryFindSpot(RoomInfo room, Func<Vector3Int, bool> isSuitable, out Vector3Int spot)
    {
        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            spot = room.RandomInteriorCell();
            if (isSuitable(spot)) return true;
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
