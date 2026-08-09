using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 레이아웃 생성의 지휘자(Orchestrator).
///
/// 이 클래스는 직접 무언가를 만들지 않는다. "무엇을 어떤 순서로 할지" 만 정하고,
/// 실제 알고리즘은 아래 네 인터페이스의 구현체가 담당한다.
/// 덕분에 방 배치 방식이나 길 찾기 방식을 통째로 갈아 끼워도 이 파일은 그대로다.
///
/// 예전에는 인터페이스와 구현 사이에 DefaultXxx 라는 껍데기 클래스가 한 겹 더
/// 있었지만, 안에서 실제 구현을 new 해서 그대로 호출만 하는 통과용이었다.
/// 구현 클래스가 인터페이스를 직접 구현하도록 바꿔 그 층을 없앴다.
/// </summary>
[RequireComponent(typeof(DungeonBlueprint))]
public class DungeonLayoutGenerator : MonoBehaviour
{
    public DungeonBlueprint blueprint;
    public TileType[,] grid;

    private readonly List<Section> sections = new List<Section>();
    private readonly List<Joint> joints = new List<Joint>();
    private readonly List<Branch> branches = new List<Branch>();
    private List<Room> rooms = new List<Room>();

    // 갈아 끼울 수 있는 생성 단계들.
    private IRoomPlacer roomPlacer;
    private IExitAssigner exitAssigner;
    private IConnectionPlanner connectionPlanner;
    private IPathBuilder pathBuilder;

    private DungeonRenderer dungeonRenderer;
    private WaterGenerator waterGenerator;

    private void Awake()
    {
        blueprint = GetComponent<DungeonBlueprint>();
        dungeonRenderer = GetComponent<DungeonRenderer>();
        waterGenerator = GetComponent<WaterGenerator>();

        roomPlacer = new RoomPlacer();
        exitAssigner = new ExitAssigner();
        connectionPlanner = new DefaultConnectionPlanner();
        pathBuilder = new PathBuilder();

        Generate();
    }

    public void Generate()
    {
        InitializeGrid();
        BuildSections();

        rooms = roomPlacer.GenerateRooms(blueprint, sections, grid);
        exitAssigner.AssignExits(rooms, sections, joints);

        PlanBranches();
        CarveBranches();
        GenerateWater();

        dungeonRenderer.Render(grid);
    }

    /// <summary>격자를 전부 벽으로 채운 뒤, 거기서 깎아 나간다.</summary>
    private void InitializeGrid()
    {
        grid = new TileType[blueprint.mapSize.x, blueprint.mapSize.y];

        for (int x = 0; x < blueprint.mapSize.x; x++)
            for (int y = 0; y < blueprint.mapSize.y; y++)
                grid[x, y] = TileType.Wall;
    }

    /// <summary>Blueprint 의 설정값을 런타임 Section 으로 옮긴다.</summary>
    private void BuildSections()
    {
        sections.Clear();

        foreach (SectionDefinition definition in blueprint.sections)
        {
            sections.Add(new Section
            {
                index = definition.index,
                bounds = new RectInt(definition.topLeft, definition.size),
                minRoomSize = definition.minRoomSize,
                maxRoomSize = definition.maxRoomSize,
                forceNoRoom = definition.forceNoRoom,
                roomWeight = definition.roomWeight,
            });
        }
    }

    /// <summary>어떤 구역끼리 이을지 정하고, 그 사이를 잇는 Branch 를 만든다.</summary>
    private void PlanBranches()
    {
        branches.Clear();

        foreach (SectionConnection connection in connectionPlanner.PlanConnections(blueprint, sections))
        {
            Joint from = joints.Find(j => j.sectionIndex == connection.fromSection);
            Joint to = joints.Find(j => j.sectionIndex == connection.toSection);

            if (from == null || to == null) continue;

            branches.Add(new Branch(connection.fromSection, connection.toSection, from, to));
        }
    }

    /// <summary>각 Branch 의 실제 경로를 구해 격자에 바닥으로 새긴다.</summary>
    private void CarveBranches()
    {
        foreach (Branch branch in branches)
        {
            List<Vector3Int> path = pathBuilder.BuildPath(
                branch.fromJoint.position.ToVector3Int(),
                branch.toJoint.position.ToVector3Int(),
                grid,
                rooms);

            branch.path = path.ConvertAll(v => new Vector2Int(v.x, v.y));

            foreach (Vector2Int cell in branch.path)
                grid[cell.x, cell.y] = TileType.Floor;
        }
    }

    private void GenerateWater()
    {
        if (waterGenerator == null) return;
        waterGenerator.Generate(blueprint, grid, rooms, branches);
    }
}
