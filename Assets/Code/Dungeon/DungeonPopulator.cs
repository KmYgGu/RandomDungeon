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

            SpawnOnGround(prefab, spot);
        }
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

    private void SpawnOnGround(GameObject prefab, Vector3Int cell)
    {
        ground.Add(GridUtil.CellCenter(cell));
        UnityEngine.Object.Instantiate(prefab, painter.CellCenter(cell), Quaternion.identity);
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
        ItemDescription description = ItemInstance.DescriptionOf(prefab);
        if (description == null) return 0f;

        ItemDefinition definition = description.Definition;
        return definition != null ? definition.DropRate : 0f;
    }
}
