using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 한 층을 만드는 순서를 정하는 역할.
///
/// 예전에는 이 클래스 하나가 720줄이었고 방 배치, 통로 파기, 물길, 타일 칠하기,
/// 몬스터·아이템·계단·함정 배치를 전부 직접 하고 있었다.
/// 지금은 아래 네 협력 객체를 순서대로 부르기만 한다.
///
///   RoomLayoutBuilder  - 방을 어디에 놓을지
///   DungeonTilePainter - 타일맵에 칠하기
///   CorridorCarver     - 방을 잇는 통로와 물길
///   DungeonPopulator   - 계단 / 아이템 / 몬스터 / 함정 / 플레이어
///
/// Inspector 로 설정하는 값들은 이름을 그대로 두어 씬의 연결이 유지된다.
/// </summary>
public class RandomDung : MonoBehaviour
{
    /// <summary>층 전체가 물로 잠길 확률.</summary>
    private const float FloodedStageChance = 0.1f;

    [Header("맵 크기")]
    public int mapWidth;
    public int mapHeight;

    [Header("방 크기")]
    public int minRoomSize;
    public int maxRoomSize;

    [Header("프리팹")]
    public GameObject playerPrefab;
    public GameObject monsterPrefab;
    public GameObject[] itemPrefabs;
    public GameObject[] trapPrefabs;
    public GameObject stairsPrefab;

    /// <summary>
    /// 적 정의 애셋 목록. <b>도감이 그림을 찾을 때만</b> 쓴다.
    ///
    /// 도감에 뜨는 이름·수치·설명은 전부 시트(MonsterCatalog)에서 오므로 이 칸이
    /// 비어 있어도 도감은 돈다. 다만 <b>그림 자리가 빈 채로</b> 뜬다.
    ///
    /// 정의 애셋은 Resources 밖(Assets/Prefab/Monsters/monsterData)에 있어서
    /// 코드가 스스로 찾아올 수 없다. 그래서 아이템이 itemPrefabs 로 찾는 것처럼
    /// 여기에 끌어다 놓는다. 애셋 자체는 `Tools ▸ 시트에서 적 정의 만들기` 가 만든다.
    /// </summary>
    public MonsterDescription[] monsterDescriptions;

    /// <summary>
    /// 동료 프리팹. 비워 두면 동료가 나오지 않는다.
    ///
    /// 지금은 첫 층에서 이 하나가 플레이어 옆에 붙는다(DungeonPopulator.PlacePartners).
    /// 만나서 들이는 방식이 생기면 여러 종류를 담는 배열로 바뀔 자리다.
    /// </summary>
    public GameObject partnerPrefab;

    [Header("타일맵")]
    public Tilemap floorTilemap;
    public Tilemap Minimap;

    [Header("타일")]
    public TileBase floorTile;
    public TileBase floorTile2;
    public TileBase wallTile;
    public TileBase waterTile;

    /// <summary>맵 끝자락. 비워 두면 보통 벽으로 대신한다(TileRules.BedrockTileNames).</summary>
    public TileBase bedrockTile;

    /// <summary>물로 가득한 층의 맵 끝자락. 비워 두면 위 것으로 대신한다.</summary>
    public TileBase bedrockWaterTile;
    public TileBase minimapTile;

    /// <summary>방의 출입구(문)에 칠할 타일. 반드시 통행 가능한 타일이어야 한다.</summary>
    public TileBase EntranceTile;

    [Header("방마다 배치할 개수")]
    public int minMonstersPerRoom;
    public int maxMonstersPerRoom;
    public int minItemsPerRoom;
    public int maxItemsPerRoom;

    [Header("최소 간격")]
    public float minDistanceBetweenItemsAndStairs = 0.5f;
    public float minDistanceBetweenTrapsAndStairs = 0.5f;
    public float minDistanceBetweenTrapsAndItems = 0.5f;

    private DungeonLayout layout = new DungeonLayout();
    private DungeonPopulator populator;

    /// <summary>적이 순찰 목표로 삼는 지점들 = 모든 방의 문.</summary>
    public List<Vector3> edgePoints { get; private set; } = new List<Vector3>();

    /// <summary>적이 "방 안에 있는지" 판정할 때 쓰는 방 안쪽 영역들.</summary>
    public List<Rect> RoomData { get; private set; } = new List<Rect>();

    /// <summary>
    /// 방들의 안쪽 칸(칸 좌표). 게임 규칙이 자리를 고를 때 쓴다.
    ///
    /// RoomData 와 같은 방을 가리키지만 이쪽은 정수다. 월드 좌표로 자리를
    /// 고르면 칸 중심의 0.5 때문에 조용히 어긋난다.
    /// </summary>
    public List<RectInt> RoomCells { get; private set; } = new List<RectInt>();

    public Vector3Int GetStairsPosition() => populator != null ? populator.StairsPosition : default;

    [Header("생성 재현 (같은 설정·층에서 같은 시드)")]
    public bool useFixedSeed;
    public int fixedSeed = 12345;
    public int LastGenerationSeed { get; private set; }
    public int GenerationAttempts { get; private set; }
    public bool UsedFallback { get; private set; }
    public int GenerationLoops { get; private set; }
    public Vector3Int StartCell { get; private set; }
    public Vector3Int StairsCell { get; private set; }

    /// <summary>미래의 적 탐지 아이템이 켜는 표시. 지형 탐색이나 하우스 발동과는 별개다.</summary>
    public bool DetectAllEnemies { get; set; }
    public IEnumerable<Vector3Int> ConcealedMonsterCells
    {
        get
        {
            if (populator == null) yield break;
            foreach (var house in populator.MonsterHouses)
            foreach (var cell in house.ConcealedCells) yield return cell;
        }
    }

    public GameObject DropEnemyItem(Vector3 position)
        => populator != null ? populator.DropEnemyItem(position) : null;

    public MonsterHouseEncounter TryEnterMonsterHouse(Vector3Int playerCell)
    {
        if (populator == null) return null;
        foreach (var house in populator.MonsterHouses)
            if (house.Plan.TryEnter(playerCell)) return house;
        return null;
    }

    [Header("층별 몬스터 하우스 확률 (비어 있으면 Resources 기본 설정 사용)")]
    public MonsterHouseSpawnSettings monsterHouseSpawnSettings;

    private float HouseChanceForStage(int stage)
    {
        var settings = monsterHouseSpawnSettings != null ? monsterHouseSpawnSettings :
            Resources.Load<MonsterHouseSpawnSettings>("MonsterHouseSpawnSettings");
        return settings != null ? settings.ChanceForStage(stage) : RoomLayoutBuilder.MonsterHouseChance(stage);
    }

    public void GenerateDungeon()
    {
        DetectAllEnemies = false;
        int seed = useFixedSeed ? fixedSeed : Random.Range(0, int.MaxValue);
        var saved = Random.state;
        try
        {
            GenerateTerrain(GameManager.Stage, seed);
            // 지형 재시도 횟수가 아이템 추첨에 영향을 주지 않도록 별도 흐름을 쓴다.
            Random.InitState(unchecked(seed ^ 0x51ed270b));
            var painter = BuildPainter();
            populator = new DungeonPopulator(painter, GameManager.Ground,
                itemPrefabs, trapPrefabs, monsterPrefab, stairsPrefab, playerPrefab,
                merchantPrefab != null ? merchantPrefab : Resources.Load<GameObject>("Merchant"));
            populator.ReserveStart(StartCell);
            populator.PlaceStairs(StairsCell);
            if (GameManager.instance.Player == null)
                GameManager.instance.Player = populator.PlacePlayer(StartCell);
            else
                GameManager.instance.Player.transform.position = painter.CellCenter(StartCell);
            GameManager.instance.PlayerPosition = painter.CellCenter(StartCell);
            populator.PlacePartners(GameManager.instance.Player, partnerPrefab);
            foreach (RoomInfo room in layout.Rooms)
                populator.PopulateRoom(room,
                    Random.Range(minItemsPerRoom, maxItemsPerRoom + 1),
                    Random.Range(minMonstersPerRoom, maxMonstersPerRoom + 1),
                    GameManager.Stage, minDistanceBetweenItemsAndStairs);
            populator.PlaceTraps(layout, minDistanceBetweenTrapsAndStairs, minDistanceBetweenTrapsAndItems);
            MinimapOverlay.Refresh(Minimap, RoomCells, StartCell);
        }
        finally { Random.state = saved; }
    }

    /// <summary>오브젝트 생성 전 지형만 확정한다. 에디터 검증도 같은 경로를 쓴다.</summary>
    public void GenerateTerrain(int stage, int seed)
    {
        var saved = Random.state;
        try
        {
            LastGenerationSeed = seed;
            UsedFallback = false;
            var painter = BuildPainter();
            for (int attempt = 0; attempt < 4; attempt++)
            {
                GenerationAttempts = attempt + 1;
                Random.InitState(unchecked(seed + attempt * 104729));
                painter.Clear();
                layout = new DungeonLayout();
                // 기본형과 순환형. 깊은 층에서도 연결 수가 끝없이 늘지 않는다.
                GenerationLoops = Random.value < 0.5f ? 1 : 3;
                if (!BuildTerrain(painter, stage)) continue;
                if (TryFinalizeTerrain(painter, seed))
                {
                    new DungeonCheck(painter, layout).Report(stage, mapWidth, mapHeight);
                    Debug.Log($"던전 {stage}층 / 시드 {seed} / 추가 연결 {GenerationLoops} / 시도 {GenerationAttempts}");
                    return;
                }
            }
            // 재생성에도 실패하면 장식·특수방 없는 한 방으로 진행을 보장한다.
            UsedFallback = true;
            GenerationLoops = 0;
            painter.Clear();
            layout = new DungeonLayout();
            layout.Rooms.Add(new RoomInfo(1, 1, Mathf.Min(12, mapWidth - 3), Mathf.Min(12, mapHeight - 3)));
            PaintRoomFloor(painter, layout.Rooms[0]);
            painter.FillEmptySpace(mapWidth, mapHeight, false);
            painter.PaintBorder(mapWidth, mapHeight, false);
            if (!TryFinalizeTerrain(painter, seed))
                throw new System.InvalidOperationException("대체 던전도 통행 불가: 맵 크기와 바닥 타일 설정을 확인하세요.");
            Debug.LogWarning($"{stage}층 시드 {seed}: 4회 검증 실패로 단순 던전을 사용합니다.");
        }
        finally { Random.state = saved; }
    }

    public GameObject merchantPrefab;
    [Header("상점방: 조건에 맞는 방이 있을 때 생성 확률")]
    [Range(0f, 1f)] public float shopChance = 0.35f;

    private bool TryFinalizeTerrain(DungeonTilePainter painter, int seed)
    {
        // 후처리 이후에도 입구 양옆이 열리지 않았는지 검사한다.
        foreach (var room in layout.Rooms)
        foreach (var door in room.Doors)
            if (!EntranceRules.IsNarrow(door, cell => !painter.IsEmpty(cell) && painter.IsWalkable(cell))) return false;
        var shopRandom = new System.Random(unchecked(seed ^ 0x34e872));
        ShopPlan.Assign(layout, (float)shopRandom.NextDouble(), shopRandom.Next(Mathf.Max(1,layout.Rooms.Count)), shopChance);
        if (!DungeonRoutes.TryPlan(layout, mapWidth, mapHeight,
            cell => !painter.IsEmpty(cell) && painter.IsWalkable(cell), seed,
            out var start, out var stairs)) return false;
        StartCell = start;
        StairsCell = stairs;
        RoomData = layout.InteriorRects();
        RoomCells = layout.InteriorCellRects();
        edgePoints = layout.DoorWorldPositions();
        return true;
    }

    private bool BuildTerrain(DungeonTilePainter painter, int stage)
    {
        // 1. 방을 흩뿌리고(일부는 몬스터 하우스로 정해진다) 바닥을 칠한다.
        new RoomLayoutBuilder(mapWidth, mapHeight, minRoomSize, maxRoomSize)
            .Build(layout, stage, HouseChanceForStage(stage));

        foreach (RoomInfo room in layout.Rooms)
            PaintRoomFloor(painter, room);

        // 2. 방마다 문을 뚫고 문과 문 사이를 통로로 잇는다.
        //    무작위 가중 신장 트리가 모든 방의 연결을 보장하고, 그 위에 루프가 얹힌다.
        if (!new CorridorCarver(painter, layout).ConnectRooms(stage, GenerationLoops, mapWidth, mapHeight)) return false;

        // 3. 통로가 나란히 붙어 폭 2칸이 된 곳을 1칸으로 깎아 낸다.
        //    반드시 물길·벽 메우기보다 먼저 해야 한다. 깎아 낸 칸이 빈칸으로
        //    남아 있어야 벽으로 메워지기 때문이다.
        var thinner = new CorridorThinner(painter, layout);
        int thinned = thinner.Thin(new TilemapWalkableGrid(floorTilemap), mapWidth, mapHeight);

        // 깎다 말면 폭 2칸짜리 통로가 그대로 남아 화면에서 넓게 뭉개져 보인다.
        // 에러가 아니라 조용히 남는 것이라, 드물게 나오는 이 경우를 눈에 띄게 알린다.
        if (thinner.StoppedAtLimit)
        {
            Debug.LogWarning(
                $"{stage}층: 통로 깎기가 한도({CorridorThinner.MaxRemovals}칸)에 걸려 " +
                "폭 2칸짜리가 남았습니다. 통로가 넓게 뭉개져 보입니다.");
        }
        else if (thinner.HasWideningBlock(mapWidth, mapHeight))
        {
            // 한도 때문이 아니라, 네 칸을 다 지워 봐도 방이 끊겨서 못 깎은 경우다.
            // 끊는 것보다는 넓은 편이 나으므로 잘못은 아니지만, 자주 뜨면
            // 통로가 지나치게 몰려 있다는 뜻이라 알아 둘 만하다.
            Debug.Log(
                $"{stage}층: 폭 2칸짜리 통로를 남겼습니다(지우면 방이 끊기는 자리). " +
                $"깎은 칸 {thinned}개.");
        }

        // 4. 물길(장식).
        new WaterwayDecorator(painter, layout).Decorate();

        // 5. 아직 빈 칸을 벽(드물게 물)으로 메운다.
        bool flooded = Random.value < FloodedStageChance;
        painter.FillEmptySpace(mapWidth, mapHeight, flooded);

        // 5-2. 벽에 막혀 끝나는 짧은 곁가지를 걷어 낸다.
        //      <b>반드시 벽을 칠한 뒤라야 한다</b> — 그 전에는 통행 판정이 빈 칸에도
        //      '갈 수 있다' 고 답해서 엉뚱한 것이 걸린다(CorridorPruner 주석).
        new CorridorPruner(painter).Prune(mapWidth, mapHeight);

        // 5-3. 걷어 낸 자리는 빈 칸으로 남았으므로 다시 벽으로 메운다.
        painter.FillEmptySpace(mapWidth, mapHeight, flooded);

        // 바깥은 허물 수 없게 두껍게 둘러 둔다. 맵 밖으로 나가면 돌아올 길이 없고,
        // 얇게 두르면 가장자리에서 카메라 배경색이 그대로 보인다.
        painter.PaintBorder(mapWidth, mapHeight, flooded);

        // 연결성 검사는 모든 후처리가 끝난 뒤 TryFinalizeTerrain에서 한다.
        return true;

    }

    private DungeonTilePainter BuildPainter()
    {
        // 씬을 새로 불러올 때마다 타일맵 인스턴스가 바뀌므로 매번 다시 찾는다.
        floorTilemap = GameObject.Find("FloorMap").GetComponent<Tilemap>();
        Minimap = GameObject.Find("MniMap").GetComponent<Tilemap>();
        GameManager.FloorTilemap = floorTilemap;

        return new DungeonTilePainter(
            floorTilemap, Minimap,
            floorTile, floorTile2, EntranceTile, wallTile, waterTile,
            bedrockTile, bedrockWaterTile, minimapTile);
    }

    /// <summary>방 안쪽만 바닥으로 칠한다. 테두리는 벽으로 남겨 두고, 문만 나중에 뚫는다.</summary>
    private static void PaintRoomFloor(DungeonTilePainter painter, RoomInfo room)
    {
        for (int x = room.InteriorMinX; x <= room.InteriorMaxX; x++)
        {
            for (int y = room.InteriorMinY; y <= room.InteriorMaxY; y++)
            {
                painter.PaintRoomFloor(new Vector3Int(x, y, 0));
            }
        }
    }
}
