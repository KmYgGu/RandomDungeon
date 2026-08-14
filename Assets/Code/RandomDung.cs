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

    /// <summary>한 층을 처음부터 끝까지 만든다.</summary>
    public void GenerateDungeon()
    {
        int stage = GameManager.Stage;

        DungeonTilePainter painter = BuildPainter();
        painter.Clear();

        layout = new DungeonLayout();
        populator = new DungeonPopulator(
            painter, GameManager.Ground,
            itemPrefabs, trapPrefabs,
            monsterPrefab, stairsPrefab, playerPrefab);

        // 1. 방을 흩뿌리고(일부는 보물방·몬스터 하우스로 정해진다) 바닥을 칠한다.
        new RoomLayoutBuilder(mapWidth, mapHeight, minRoomSize, maxRoomSize)
            .Build(layout, stage);

        foreach (RoomInfo room in layout.Rooms)
            PaintRoomFloor(painter, room);

        // 2. 방마다 문을 뚫고 문과 문 사이를 통로로 잇는다.
        //    최소 신장 트리가 모든 방의 연결을 보장하고, 그 위에 루프가 얹힌다.
        new CorridorCarver(painter, layout).ConnectRooms(stage);

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

        // 5-4. 다 만들어진 층을 훑어 잘못된 자리가 남았는지 알린다. 고치지는 않는다.
        //      걷어 내기가 제대로 돌았으면 여기서 아무것도 나오지 않아야 한다 —
        //      매 층 실제로 확인되는 셈이다.
        new DungeonCheck(painter, layout).Report(stage, mapWidth, mapHeight);

        // 6. 적이 쓸 정보를 넘겨 준다. 통로를 다 판 뒤라야 문이 확정된다.
        RoomData = layout.InteriorRects();
        RoomCells = layout.InteriorCellRects();
        edgePoints = layout.DoorWorldPositions();

        // 7. 계단을 먼저 정한다. 아이템 배치가 계단 위치를 피해야 하기 때문이다.
        //    (예전에는 아이템을 먼저 놓아서, 계단과의 거리 검사가 항상
        //     아직 정해지지 않은 (0,0) 과 비교되어 사실상 동작하지 않았다.)
        populator.PlaceStairs(layout);

        // 8. 방마다 아이템과 몬스터를 놓는다.
        foreach (RoomInfo room in layout.Rooms)
        {
            populator.PopulateRoom(
                room,
                Random.Range(minItemsPerRoom, maxItemsPerRoom + 1),
                Random.Range(minMonstersPerRoom, maxMonstersPerRoom + 1),
                stage,
                minDistanceBetweenItemsAndStairs);
        }

        // 9. 첫 층이라면 플레이어를 만든다.
        if (GameManager.instance.Player == null)
            GameManager.instance.Player = populator.PlacePlayer(layout);

        // 10. 동료. 플레이어가 선 뒤라야 그 옆자리를 고를 수 있다.
        populator.PlacePartners(GameManager.instance.Player, partnerPrefab);

        // 11. 함정.
        populator.PlaceTraps(layout, minDistanceBetweenTrapsAndStairs, minDistanceBetweenTrapsAndItems);
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
