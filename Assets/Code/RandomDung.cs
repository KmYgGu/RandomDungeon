using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

public class RandomDung : MonoBehaviour
{
    public int mapWidth;
    public int mapHeight;
    public int minRoomSize;
    public int maxRoomSize;

    public GameObject playerPrefab;
    public GameObject monsterPrefab;
    public GameObject[] itemPrefabs;

    // 각 아이템에 대한 가중치 설정
    public float[] itemWeights = { 5f, 5f, 5f, 2f, 1f }; // 가중치 배열

    public GameObject[] trapPrefabs;
    public GameObject stairsPrefab;

    public Tilemap floorTilemap;
    public Tilemap Minimap;

    public TileBase floorTile;
    public TileBase floorTile2;
    public TileBase EntranceTile;
    public TileBase wallTile;
    public TileBase waterTile;
    public TileBase minimapTile;

    public int minMonstersPerRoom;
    public int maxMonstersPerRoom;
    public int minItemsPerRoom;
    public int maxItemsPerRoom;


    public float minDistanceBetweenItemsAndStairs = 0.5f; // 계단 옆에 아이템 생성되는 방지 거리
    public float minDistanceBetweenTrapsAndStairs = 0.5f;
    public float minDistanceBetweenTrapsAndItems = 0.5f;

    public List<Rect> rooms = new List<Rect>();

    //private bool PlayerSpownd = false;

    private List<Vector3> itemPositions = new List<Vector3>();
    // 필드에 추가된 움직이는 오브젝트 리스트
    private List<Vector3Int> movingObjects = new List<Vector3Int>();
    // 방이 연결된 곳인지 확인
    public List<Vector3Int> connectionPoints = new List<Vector3Int>();
    private Vector3Int stairsPosition;

    public List<Rect> RoomData = new List<Rect>();
    public List<Vector3> edgePoints = new List<Vector3>();

    public void GenerateDungeon()
    {
        // 층 수에 따라 최대 방 갯수 설정 (예: 1층 = 최대 2개, 50층 = 최대 15개)
        int baseRooms = Mathf.Clamp(GameManager.Stage * 2, 1, 15);
        int randomAdditionalRooms = Random.Range(0, 4); // 0부터 3까지의 무작위한 값 추가
        int maxRooms = baseRooms + randomAdditionalRooms;


        floorTilemap = GameObject.Find("FloorMap").GetComponent<Tilemap>();
        Minimap = GameObject.Find("MniMap").GetComponent<Tilemap>();
        GameManager.tilemap = GameObject.Find("FloorMap").GetComponent<Tilemap>();

        rooms.Clear();
        itemPositions.Clear();
        movingObjects.Clear();
        edgePoints.Clear();
        connectionPoints.Clear();
        RoomData.Clear();

        floorTilemap.ClearAllTiles();
        Minimap.ClearAllTiles();


        //방 생성기
        for (int i = 0; i < maxRooms; i++)
        {
            int roomWidth = Random.Range(minRoomSize, maxRoomSize);
            int roomHeight = Random.Range(minRoomSize, maxRoomSize);

            int x = Random.Range(0, mapWidth - roomWidth);
            int y = Random.Range(0, mapHeight - roomHeight);

            Rect newRoom = new Rect(x, y, roomWidth, roomHeight);//기존 방만들기

            //방이 겹쳐서 생성되지 않게
            bool overlaps = false;

            // 방 리스트가 비어있지 않을 때에만 루프 실행
            if (rooms.Count > 0)
            {
                foreach (Rect room in rooms)
                {
                    if (newRoom.Overlaps(room))
                    {
                        overlaps = true;
                        break;
                    }
                }
            }

            if (!overlaps && IsDistanceValid(newRoom, rooms, 6))  // 최소 6(5)개의 오브젝트 거리를 보정
            {
                Rect RoomDataTilepos = new Rect(x + 1.5f, y + 1.5f, roomWidth - 1, roomHeight - 1);
                RoomData.Add(RoomDataTilepos);

                rooms.Add(newRoom);
                CreateRoom(newRoom);

                int numMonsters = Random.Range(minMonstersPerRoom, maxMonstersPerRoom + 1);
                //int numMonsters = 1;


                int numItems = Random.Range(minItemsPerRoom, maxItemsPerRoom + 1);
                SpawnItems(newRoom, numItems, numMonsters);
            }
        }

        // 방 연결
        ConnectRooms();

        // 모든 영역을 Floor Prefab으로 채우기
        FillMapWithWalls();

        // 스폰 플레이어
        if (GameManager.instance.Player == null)//playerPositions.Count == 0
        {
            Rect randomRoom = rooms[Random.Range(0, rooms.Count)];
            Vector3Int playerSpawnPosition = GetRandomPositionInRoom(randomRoom);
            bool isColliding;

            do
            {
                // 무작위 위치 선택
                randomRoom = rooms[Random.Range(0, rooms.Count)];
                playerSpawnPosition = GetRandomPositionInRoom(randomRoom);
                // 움직이는 오브젝트 리스트를 체크하여 충돌 확인
                isColliding = IsCollidingWithMovingObjects(playerSpawnPosition);
            }
            while (isColliding);

            // 충돌하지 않는 위치를 찾았을 경우 플레이어 생성
            GameManager.instance.Player = Instantiate(playerPrefab, ApplyTilemapOffset(playerSpawnPosition, floorTilemap), Quaternion.identity);

            // 움직이는 오브젝트 리스트에 위치 추가
            movingObjects.Add(playerSpawnPosition);
        }
        
        // 계단 생성
        SpawnStairsInRandomRoom();

        // 함정 생성
        GenerateTraps();

    }
    //타일맵의 중심 안에 생성되기 위해 기존 좌표에다 보정치 추가
    Vector3 ApplyTilemapOffset(Vector3Int cellPosition, Tilemap tilemap)
    {
        Vector3 worldPosition = tilemap.CellToWorld(cellPosition);
        worldPosition.x += 0.5f;
        worldPosition.y += 0.5f;
        return worldPosition;
    }
    void CreateRoom(Rect room)
    {
        for (int x = (int)room.x; x < room.xMax; x++)
        {
            for (int y = (int)room.y; y < room.yMax; y++)
            {
                //방 주변을 감싸는 벽 생성
                if (x == room.x | x == room.xMax | y == room.y | y == room.yMax)//(x == room.x | x == room.xMax - 1 | y == room.y | y == room.yMax - 1
                {
                    //Instantiate(wallPrefab, new Vector3(x, y, 0), Quaternion.identity);
                    //wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                }
                // 방안에 땅을 생성
                else
                {
                    floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                    Minimap.SetTile(new Vector3Int(x, y, 0), minimapTile);

                }
            }
        }
    }

    void SpawnItems(Rect room, int count, int count2)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3Int spawnPosition = GetRandomPositionInRoom(room);

            // 아이템 생성 위치가 중복되지 않도록 반복해서 위치를 찾음
            do
            {
                spawnPosition = GetRandomPositionInRoom(room);
            }
            while (Vector3Int.Distance(spawnPosition, stairsPosition) < minDistanceBetweenItemsAndStairs ||
                   itemPositions.Contains(spawnPosition));  // 계단 위치 및 다른 아이템과 중복되지 않도록 확인

            GameObject selectedPrefab = SelectItemWithWeight();

            // 아이템을 타일맵에 맞게 위치 조정 후 생성
            Instantiate(selectedPrefab, ApplyTilemapOffset(spawnPosition, floorTilemap), Quaternion.identity);
            Vector3 SetGround = new Vector3(0,0,0);
            itemPositions.Add(spawnPosition);
            SetGround.x = spawnPosition.x + 0.5f;
            SetGround.y = spawnPosition.y + 0.5f;
            GameManager.GroundSetPositions.Add(SetGround);
        }
        // 몬스터 생성 이전에 움직이는 오브젝트 리스트를 체크하여 생성할 위치를 결정합니다.
        for (int i = 0; i < count2; i++)
        {
            // 몬스터 생성 위치를 결정
            Vector3Int spawnPosition;
            bool isColliding;

            do
            {
                // 무작위 위치 선택
                spawnPosition = GetRandomPositionInRoom(room);
                // 움직이는 오브젝트 리스트를 체크하여 충돌 확인
                isColliding = IsCollidingWithMovingObjects(spawnPosition);
            }
            while (isColliding);

            // 충돌하지 않는 위치를 찾았을 경우 몬스터 생성
            GameObject monsterObject = Instantiate(monsterPrefab, ApplyTilemapOffset(spawnPosition, floorTilemap), Quaternion.identity);
            Enemy SetStat = monsterObject.GetComponent<Enemy>();
            int randomStat = Random.Range(1 + GameManager.Stage, 3 + GameManager.Stage);
            SetStat.SetStats(5, randomStat, randomStat - GameManager.Stage);
            // 움직이는 오브젝트 리스트에 위치 추가
            movingObjects.Add(spawnPosition);
        }
    }

    // 가중치를 기반으로 아이템을 선택하는 함수
    private GameObject SelectItemWithWeight()
    {
        float totalWeight = 0f;

        // 전체 가중치 계산
        for (int i = 0; i < itemWeights.Length; i++)
        {
            totalWeight += itemWeights[i];
        }

        // 0과 총 가중치 사이의 무작위 값 생성
        float randomValue = Random.Range(0, totalWeight);

        // 무작위 값이 어느 가중치에 해당하는지 확인
        for (int i = 0; i < itemWeights.Length; i++)
        {
            if (randomValue < itemWeights[i])
            {
                return itemPrefabs[i];
            }
            randomValue -= itemWeights[i];
        }

        // 기본적으로 첫 번째 아이템 반환 (실제로 여기에 도달하면 안됨)
        return itemPrefabs[0];
    }

    private float minDistanceBetweenObjects = 1.0f; // 오브젝트 간 최소 거리

    // 움직이는 오브젝트 리스트 체크 메서드 추가
    bool IsCollidingWithMovingObjects(Vector3Int position)
    {
        foreach (Vector3Int objPosition in movingObjects)
        {
            // 위치 비교
            if (Vector3Int.Distance(objPosition, position) < minDistanceBetweenObjects)
            {
                return true;
            }
        }

        // 충돌하지 않으면 false 반환
        return false;
    }

    //방안에 무작위 위치를 정해줌
    Vector3Int GetRandomPositionInRoom(Rect room)// Vector2 GetRandomPositionInRoom(Rect room)
    {
        int xMin = (int)room.x + 1;
        int xMax = (int)room.xMax - 1;
        int yMin = (int)room.y + 1;
        int yMax = (int)room.yMax - 1;

        //float x = Random.Range(xMin, xMax);
        //float y = Random.Range(yMin, yMax);
        int x = Random.Range(xMin, xMax);
        int y = Random.Range(yMin, yMax);

        //return new Vector2(x, y);
        return new Vector3Int(x, y, 0);
    }

    void SpawnStairsInRandomRoom()
    {
        if (rooms.Count > 0)
        {
            Rect randomRoom;
            Vector3Int spawnPosition;
            bool isCloseToItems;

            do
            {
                randomRoom = rooms[Random.Range(0, rooms.Count)];
                spawnPosition = GetRandomPositionInRoom(randomRoom);
                isCloseToItems = itemPositions.Exists(itemPos => Vector3.Distance(spawnPosition, itemPos) < minDistanceBetweenTrapsAndItems);
            } while (isCloseToItems);

            Vector3 SetGround = new Vector3(0, 0, 0);
            SetGround.x = spawnPosition.x + 0.5f;
            SetGround.y = spawnPosition.y + 0.5f;
            GameManager.GroundSetPositions.Add(SetGround);
            Instantiate(stairsPrefab, ApplyTilemapOffset(spawnPosition, floorTilemap), Quaternion.identity);
            stairsPosition = spawnPosition; // 생성된 계단의 위치를 저장
        }
    }

    //새롭게 생성될 방과 기존 방 간의 최소 거리를 확인
    bool IsDistanceValid(Rect newRoom, List<Rect> existingRooms, int minDistance)
    {
        foreach (Rect room in existingRooms)
        {
            int distanceInObjects = CalculateDistanceInObjects(newRoom, room);
            if (distanceInObjects < minDistance)
            {
                return false; // 최소 거리를 충족하지 않으면 false 반환
            }
        }

        return true; // 모든 기존 방과의 거리가 최소 거리 이상이면 true 반환
    }

    int CalculateDistanceInObjects(Rect roomA, Rect roomB)
    {
        // 방 A와 방 B 사이의 가로 방향 오브젝트의 갯수를 계산
        int objectDistanceX = Mathf.Abs((int)(roomA.center.x - roomB.center.x));

        // 방 A와 방 B 사이의 세로 방향 오브젝트의 갯수를 계산
        int objectDistanceY = Mathf.Abs((int)(roomA.center.y - roomB.center.y));

        // 가로 방향과 세로 방향의 오브젝트 갯수를 합하여 총 오브젝트 갯수 반환
        return objectDistanceX + objectDistanceY;
    }

    //통로를 만들기 위한 계산
    void ConnectRooms()
    {
        // 이미 연결된 방과 해당 방에 대한 to 값을 저장할 딕셔너리
        Dictionary<Vector3Int, Vector3Int> roomConnections = new Dictionary<Vector3Int, Vector3Int>();

        foreach (Rect room in rooms)
        {
            // 방의 중심을 구하기.
            int x = (int)(room.x + room.width / 2);
            int y = (int)(room.y + room.height / 2);
            Vector3Int roomCenter = new Vector3Int(x, y, 0);

            // 방의 중심이 이미 연결되었는지 확인
            if (!connectionPoints.Contains(roomCenter))
            {
                connectionPoints.Add(roomCenter);

                // 무작위로 다른 방과 연결
                if (connectionPoints.Count > 1)//마지막으로 추가된 방의 중심을 시작점 from, 연결할 대상 방의 중심을 to로 설정할 준비
                {
                    int last = connectionPoints.Count - 1;
                    Vector3Int from = connectionPoints[last];
                    Vector3Int to;

                    // 이미 연결된 방인지 확인하고, 이전 to 값을 가져오거나 새로운 값을 결정합니다.
                    if (roomConnections.ContainsKey(roomCenter))
                    {
                        // 이미 결정된 to 값을 가져옴
                        to = roomConnections[roomCenter];
                    }
                    else
                    {
                        // 새로운 다른 방과 연결
                        
                            to = connectionPoints[Random.Range(0, last)];
                        
                    }
                    // 복도는 무조건 연결
                    GenerateHallway(from, to);

                    // 연결된 방과 해당 to 값을 저장
                    roomConnections.Add(roomCenter, to);


                    // 90% 확률로 수도 연결
                    if (Random.value < 0.9f)
                    {
                        // 서로 다른 시작점과 끝점을 선택하여 수로 생성
                        int otherLast = Random.Range(0, last);
                        Vector3Int otherFrom = connectionPoints[otherLast];
                        Vector3Int otherTo = connectionPoints[Random.Range(0, last)];

                        // 시작점과 끝점이 같은 경우 다시 선택
                        while (otherFrom == from && otherTo == to)
                        {
                            otherLast = Random.Range(0, last);
                            otherFrom = connectionPoints[otherLast];
                            otherTo = connectionPoints[Random.Range(0, last)];
                        }

                        GenerateWaterway(otherFrom, otherTo);
                    }
                }
            }
        }
    }

    List<Vector3Int> generatedTiles = new List<Vector3Int>();
    List<Vector3Int> CompareTiles = new List<Vector3Int>();

    void GenerateHallway(Vector3Int from, Vector3Int to)
    {
        CompareTiles.Clear();

        int x = from.x;
        int y = from.y;

        int xDistance = Mathf.Abs(to.x - x);
        int yDistance = Mathf.Abs(to.y - y);

        if (xDistance > yDistance)
        {
            // x 방향으로 절반 이동
            int halfXDistance = x + (int)(Mathf.Sign(to.x - x) * (xDistance / 2));
            while (x != halfXDistance)
            {
                x += (int)Mathf.Sign(to.x - x);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // 이미 타일이 존재하면 중단
                }
            }

            // y 방향으로 to의 y 값까지 이동
            while (y != to.y)
            {
                y += (int)Mathf.Sign(to.y - y);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // 이미 타일이 존재하면 중단
                }
            }

            // 남은 x 방향 거리 이동
            while (x != to.x)
            {
                x += (int)Mathf.Sign(to.x - x);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // 이미 타일이 존재하면 중단
                }
            }
        }
        else
        {
            // y 방향으로 절반 이동
            int halfYDistance = y + (int)Mathf.Sign(to.y - y) * (yDistance / 2);
            while (y != halfYDistance)
            {
                y += (int)Mathf.Sign(to.y - y);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // 이미 타일이 존재하면 중단
                }
            }

            // x 방향으로 to의 x 값까지 이동
            while (x != to.x)
            {
                x += (int)Mathf.Sign(to.x - x);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // 이미 타일이 존재하면 중단
                }
            }

            // 남은 y 방향 거리 이동
            while (y != to.y)
            {
                y += (int)Mathf.Sign(to.y - y);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // 이미 타일이 존재하면 중단
                }
            }
        }

        generatedTiles.AddRange(CompareTiles);
    }

    bool MakeHallwayTile(Vector3Int position)
    {
        // 이미 해당 위치에 floorTile2가 존재하는지 확인
        if (floorTilemap.GetTile(position) == floorTile2)
        {
            return false; // 이미 타일이 존재하면 중단
        }

        floorTilemap.SetTile(position, floorTile2);
        CompareTiles.Add(position);

        if (IsEdgePoint(position))
        {
            Vector3 closestPointFloat = new Vector3(position.x, position.y, 0);
            closestPointFloat.x += 0.5f;
            closestPointFloat.y += 0.5f;
            edgePoints.Add(closestPointFloat);
            floorTilemap.SetTile(position, floorTile2);
        }
        Minimap.SetTile(position, minimapTile);
        return true; // 새로운 타일을 생성하면 계속 진행
    }

    //방의 테두리인지 확인하는 부분
    bool IsEdgePoint(Vector3Int position)
    {
        foreach (Rect room in rooms)
        {
            Vector3Int bottomLeft = new Vector3Int((int)room.xMin, (int)room.yMin, 0);
            Vector3Int topRight = new Vector3Int((int)room.xMax, (int)room.yMax, 0);

            // Check if the position is on the left or right edge
            if ((position.x == bottomLeft.x || position.x == topRight.x) && (position.y >= bottomLeft.y && position.y <= topRight.y))
            {
                return true;
            }

            // Check if the position is on the top or bottom edge
            if ((position.y == bottomLeft.y || position.y == topRight.y) && (position.x >= bottomLeft.x && position.x <= topRight.x))
            {
                return true;
            }
        }
        return false;
    }


    void GenerateWaterway(Vector3Int from, Vector3Int to)
    {
        int x = from.x;
        int y = from.y;

        while (x != to.x)
        {
            // X 방향으로 이동 시 사용할 범위
            //int xRange = GetRandomRange();
            int xRange = Random.Range(1, 3);
            x += (int)Mathf.Sign(to.x - from.x);
            Vector3Int currentPosition = new Vector3Int(x, y, 0);

            // If there is no floor tile at the current position, set water tile
            if (floorTilemap.GetTile(currentPosition) == null)
            {
                edgePoints.Remove(currentPosition);
                floorTilemap.SetTile(currentPosition, waterTile);

                // y 방향으로 위/아래로 물 타일 추가
                AddWaterTilesInRange(currentPosition, true, xRange);
            }
        }

        while (y != to.y)
        {
            // Y 방향으로 이동 시 사용할 범위
            //int yRange = GetRandomRange();
            int yRange = Random.Range(1, 3);

            y += (int)Mathf.Sign(to.y - from.y);
            Vector3Int currentPosition = new Vector3Int(x, y, 0);

            // If there is no floor tile at the current position, set water tile
            if (floorTilemap.GetTile(currentPosition) == null)
            {
                edgePoints.Remove(currentPosition);
                floorTilemap.SetTile(currentPosition, waterTile);

                // x 방향으로 좌/우로 물 타일 추가
                AddWaterTilesInRange(currentPosition, false, yRange);
            }

        }
    }
    void AddWaterTilesInRange(Vector3Int position, bool isYAxis, int range)
    {
        for (int i = 1; i <= range; i++)
        {
            Vector3Int offset1 = isYAxis ? new Vector3Int(0, i, 0) : new Vector3Int(i, 0, 0);
            Vector3Int offset2 = isYAxis ? new Vector3Int(0, -i, 0) : new Vector3Int(-i, 0, 0);

            // 위/아래 또는 좌/우에 물 타일 추가
            Vector3Int pos1 = position + offset1;
            Vector3Int pos2 = position + offset2;

            if (floorTilemap.GetTile(pos1) == null)
            {
                floorTilemap.SetTile(pos1, waterTile);
            }
            if (floorTilemap.GetTile(pos2) == null)
            {
                floorTilemap.SetTile(pos2, waterTile);
            }
        }
    }

    /*int GetRandomRange()
    {
        int range = Random.Range(1, 3); // 1에서 3 사이의 무작위 값
        if (range == 2)
        {
            range = Random.Range(2, 4); // 2 또는 3
        }
        else if (range == 3)
        {
            range = 3; // 무조건 3
        }
        return range;
    }*/

    void FillMapWithWalls()
    {
        if (Random.value > 0.9f)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Vector3Int position = new Vector3Int(x, y, 0);
                    if (floorTilemap.GetTile(position) == null)
                    {
                        floorTilemap.SetTile(position, waterTile);
                    }
                }
            }
        }
        else

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);
                if (floorTilemap.GetTile(position) == null)
                {
                    floorTilemap.SetTile(position, wallTile);
                }
            }
        }
    }

    // 저장된 계단의 위치를 반환하는 함수
    public Vector3Int GetStairsPosition()//public Vector3 GetStairsPosition()
    {
        return stairsPosition;
    }
    //함정 생성
    void GenerateTraps()
    {
        foreach (Rect room in rooms)
        {
            // 방이 무작위로 생성되었는지 확인 
            if (rooms.Contains(room))
            {

                Vector3Int trapPosition;
                bool isCloseToStairs, isCloseToItems, isAtEdge;

                do
                {
                    trapPosition = GetRandomPositionInRoom(room);

                    // 함정 위치가 계단이나 물건에 너무 가깝지 않은지 확인.
                    isCloseToStairs = stairsPosition != null && Vector3Int.Distance(trapPosition, stairsPosition) < minDistanceBetweenTrapsAndStairs;
                    isCloseToItems = itemPositions.Exists(itemPos => Vector3.Distance(trapPosition, itemPos) < minDistanceBetweenTrapsAndItems);
                    isAtEdge = edgePoints.Exists(edge => Vector3.Distance(trapPosition, edge) < 1.5f);

                } while (isCloseToStairs || isCloseToItems || isAtEdge);

                // 조건이 충족되지 않는 위치를 찾은 경우 함정을 배치
                int randomValue = Random.Range(0, trapPrefabs.Length);

                Vector3 SetGround = new Vector3(0, 0, 0);
                SetGround.x = trapPosition.x + 0.5f;
                SetGround.y = trapPosition.y + 0.5f;
                GameManager.GroundSetPositions.Add(SetGround);
                Instantiate(trapPrefabs[randomValue], ApplyTilemapOffset(trapPosition, floorTilemap), Quaternion.identity);
                
            }
        }
    }

}

//방 데이터를 저장하기 위한 방 클래스
public class Room
{
    public int x;
    public int y;
    public int width;
    public int height;
    public int xMax
    {
        get { return x + width; }
    }
    public int yMax
    {
        get { return y + height; }
    }
}
