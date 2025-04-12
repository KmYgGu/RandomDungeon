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

    // �� �����ۿ� ���� ����ġ ����
    public float[] itemWeights = { 5f, 5f, 5f, 2f, 1f }; // ����ġ �迭

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


    public float minDistanceBetweenItemsAndStairs = 0.5f; // ��� ���� ������ �����Ǵ� ���� �Ÿ�
    public float minDistanceBetweenTrapsAndStairs = 0.5f;
    public float minDistanceBetweenTrapsAndItems = 0.5f;

    public List<Rect> rooms = new List<Rect>();

    //private bool PlayerSpownd = false;

    private List<Vector3> itemPositions = new List<Vector3>();
    // �ʵ忡 �߰��� �����̴� ������Ʈ ����Ʈ
    private List<Vector3Int> movingObjects = new List<Vector3Int>();
    // ���� ����� ������ Ȯ��
    public List<Vector3Int> connectionPoints = new List<Vector3Int>();
    private Vector3Int stairsPosition;

    public List<Rect> RoomData = new List<Rect>();
    public List<Vector3> edgePoints = new List<Vector3>();

    public void GenerateDungeon()
    {
        // �� ���� ���� �ִ� �� ���� ���� (��: 1�� = �ִ� 2��, 50�� = �ִ� 15��)
        int baseRooms = Mathf.Clamp(GameManager.Stage * 2, 1, 15);
        int randomAdditionalRooms = Random.Range(0, 4); // 0���� 3������ �������� �� �߰�
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


        //�� ������
        for (int i = 0; i < maxRooms; i++)
        {
            int roomWidth = Random.Range(minRoomSize, maxRoomSize);
            int roomHeight = Random.Range(minRoomSize, maxRoomSize);

            int x = Random.Range(0, mapWidth - roomWidth);
            int y = Random.Range(0, mapHeight - roomHeight);

            Rect newRoom = new Rect(x, y, roomWidth, roomHeight);//���� �游���

            //���� ���ļ� �������� �ʰ�
            bool overlaps = false;

            // �� ����Ʈ�� ������� ���� ������ ���� ����
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

            if (!overlaps && IsDistanceValid(newRoom, rooms, 6))  // �ּ� 6(5)���� ������Ʈ �Ÿ��� ����
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

        // �� ����
        ConnectRooms();

        // ��� ������ Floor Prefab���� ä���
        FillMapWithWalls();

        // ���� �÷��̾�
        if (GameManager.instance.Player == null)//playerPositions.Count == 0
        {
            Rect randomRoom = rooms[Random.Range(0, rooms.Count)];
            Vector3Int playerSpawnPosition = GetRandomPositionInRoom(randomRoom);
            bool isColliding;

            do
            {
                // ������ ��ġ ����
                randomRoom = rooms[Random.Range(0, rooms.Count)];
                playerSpawnPosition = GetRandomPositionInRoom(randomRoom);
                // �����̴� ������Ʈ ����Ʈ�� üũ�Ͽ� �浹 Ȯ��
                isColliding = IsCollidingWithMovingObjects(playerSpawnPosition);
            }
            while (isColliding);

            // �浹���� �ʴ� ��ġ�� ã���� ��� �÷��̾� ����
            GameManager.instance.Player = Instantiate(playerPrefab, ApplyTilemapOffset(playerSpawnPosition, floorTilemap), Quaternion.identity);

            // �����̴� ������Ʈ ����Ʈ�� ��ġ �߰�
            movingObjects.Add(playerSpawnPosition);
        }
        
        // ��� ����
        SpawnStairsInRandomRoom();

        // ���� ����
        GenerateTraps();

    }
    //Ÿ�ϸ��� �߽� �ȿ� �����Ǳ� ���� ���� ��ǥ���� ����ġ �߰�
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
                //�� �ֺ��� ���δ� �� ����
                if (x == room.x | x == room.xMax | y == room.y | y == room.yMax)//(x == room.x | x == room.xMax - 1 | y == room.y | y == room.yMax - 1
                {
                    //Instantiate(wallPrefab, new Vector3(x, y, 0), Quaternion.identity);
                    //wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                }
                // ��ȿ� ���� ����
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

            // ������ ���� ��ġ�� �ߺ����� �ʵ��� �ݺ��ؼ� ��ġ�� ã��
            do
            {
                spawnPosition = GetRandomPositionInRoom(room);
            }
            while (Vector3Int.Distance(spawnPosition, stairsPosition) < minDistanceBetweenItemsAndStairs ||
                   itemPositions.Contains(spawnPosition));  // ��� ��ġ �� �ٸ� �����۰� �ߺ����� �ʵ��� Ȯ��

            GameObject selectedPrefab = SelectItemWithWeight();

            // �������� Ÿ�ϸʿ� �°� ��ġ ���� �� ����
            Instantiate(selectedPrefab, ApplyTilemapOffset(spawnPosition, floorTilemap), Quaternion.identity);
            Vector3 SetGround = new Vector3(0,0,0);
            itemPositions.Add(spawnPosition);
            SetGround.x = spawnPosition.x + 0.5f;
            SetGround.y = spawnPosition.y + 0.5f;
            GameManager.GroundSetPositions.Add(SetGround);
        }
        // ���� ���� ������ �����̴� ������Ʈ ����Ʈ�� üũ�Ͽ� ������ ��ġ�� �����մϴ�.
        for (int i = 0; i < count2; i++)
        {
            // ���� ���� ��ġ�� ����
            Vector3Int spawnPosition;
            bool isColliding;

            do
            {
                // ������ ��ġ ����
                spawnPosition = GetRandomPositionInRoom(room);
                // �����̴� ������Ʈ ����Ʈ�� üũ�Ͽ� �浹 Ȯ��
                isColliding = IsCollidingWithMovingObjects(spawnPosition);
            }
            while (isColliding);

            // �浹���� �ʴ� ��ġ�� ã���� ��� ���� ����
            GameObject monsterObject = Instantiate(monsterPrefab, ApplyTilemapOffset(spawnPosition, floorTilemap), Quaternion.identity);
            Enemy SetStat = monsterObject.GetComponent<Enemy>();
            int randomStat = Random.Range(1 + GameManager.Stage, 3 + GameManager.Stage);
            SetStat.SetStats(5, randomStat, randomStat - GameManager.Stage);
            // �����̴� ������Ʈ ����Ʈ�� ��ġ �߰�
            movingObjects.Add(spawnPosition);
        }
    }

    // ����ġ�� ������� �������� �����ϴ� �Լ�
    private GameObject SelectItemWithWeight()
    {
        float totalWeight = 0f;

        // ��ü ����ġ ���
        for (int i = 0; i < itemWeights.Length; i++)
        {
            totalWeight += itemWeights[i];
        }

        // 0�� �� ����ġ ������ ������ �� ����
        float randomValue = Random.Range(0, totalWeight);

        // ������ ���� ��� ����ġ�� �ش��ϴ��� Ȯ��
        for (int i = 0; i < itemWeights.Length; i++)
        {
            if (randomValue < itemWeights[i])
            {
                return itemPrefabs[i];
            }
            randomValue -= itemWeights[i];
        }

        // �⺻������ ù ��° ������ ��ȯ (������ ���⿡ �����ϸ� �ȵ�)
        return itemPrefabs[0];
    }

    private float minDistanceBetweenObjects = 1.0f; // ������Ʈ �� �ּ� �Ÿ�

    // �����̴� ������Ʈ ����Ʈ üũ �޼��� �߰�
    bool IsCollidingWithMovingObjects(Vector3Int position)
    {
        foreach (Vector3Int objPosition in movingObjects)
        {
            // ��ġ ��
            if (Vector3Int.Distance(objPosition, position) < minDistanceBetweenObjects)
            {
                return true;
            }
        }

        // �浹���� ������ false ��ȯ
        return false;
    }

    //��ȿ� ������ ��ġ�� ������
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
            stairsPosition = spawnPosition; // ������ ����� ��ġ�� ����
        }
    }

    //���Ӱ� ������ ��� ���� �� ���� �ּ� �Ÿ��� Ȯ��
    bool IsDistanceValid(Rect newRoom, List<Rect> existingRooms, int minDistance)
    {
        foreach (Rect room in existingRooms)
        {
            int distanceInObjects = CalculateDistanceInObjects(newRoom, room);
            if (distanceInObjects < minDistance)
            {
                return false; // �ּ� �Ÿ��� �������� ������ false ��ȯ
            }
        }

        return true; // ��� ���� ����� �Ÿ��� �ּ� �Ÿ� �̻��̸� true ��ȯ
    }

    int CalculateDistanceInObjects(Rect roomA, Rect roomB)
    {
        // �� A�� �� B ������ ���� ���� ������Ʈ�� ������ ���
        int objectDistanceX = Mathf.Abs((int)(roomA.center.x - roomB.center.x));

        // �� A�� �� B ������ ���� ���� ������Ʈ�� ������ ���
        int objectDistanceY = Mathf.Abs((int)(roomA.center.y - roomB.center.y));

        // ���� ����� ���� ������ ������Ʈ ������ ���Ͽ� �� ������Ʈ ���� ��ȯ
        return objectDistanceX + objectDistanceY;
    }

    //��θ� ����� ���� ���
    void ConnectRooms()
    {
        // �̹� ����� ��� �ش� �濡 ���� to ���� ������ ��ųʸ�
        Dictionary<Vector3Int, Vector3Int> roomConnections = new Dictionary<Vector3Int, Vector3Int>();

        foreach (Rect room in rooms)
        {
            // ���� �߽��� ���ϱ�.
            int x = (int)(room.x + room.width / 2);
            int y = (int)(room.y + room.height / 2);
            Vector3Int roomCenter = new Vector3Int(x, y, 0);

            // ���� �߽��� �̹� ����Ǿ����� Ȯ��
            if (!connectionPoints.Contains(roomCenter))
            {
                connectionPoints.Add(roomCenter);

                // �������� �ٸ� ��� ����
                if (connectionPoints.Count > 1)//���������� �߰��� ���� �߽��� ������ from, ������ ��� ���� �߽��� to�� ������ �غ�
                {
                    int last = connectionPoints.Count - 1;
                    Vector3Int from = connectionPoints[last];
                    Vector3Int to;

                    // �̹� ����� ������ Ȯ���ϰ�, ���� to ���� �������ų� ���ο� ���� �����մϴ�.
                    if (roomConnections.ContainsKey(roomCenter))
                    {
                        // �̹� ������ to ���� ������
                        to = roomConnections[roomCenter];
                    }
                    else
                    {
                        // ���ο� �ٸ� ��� ����
                        
                            to = connectionPoints[Random.Range(0, last)];
                        
                    }
                    // ������ ������ ����
                    GenerateHallway(from, to);

                    // ����� ��� �ش� to ���� ����
                    roomConnections.Add(roomCenter, to);


                    // 90% Ȯ���� ���� ����
                    if (Random.value < 0.9f)
                    {
                        // ���� �ٸ� �������� ������ �����Ͽ� ���� ����
                        int otherLast = Random.Range(0, last);
                        Vector3Int otherFrom = connectionPoints[otherLast];
                        Vector3Int otherTo = connectionPoints[Random.Range(0, last)];

                        // �������� ������ ���� ��� �ٽ� ����
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
            // x �������� ���� �̵�
            int halfXDistance = x + (int)(Mathf.Sign(to.x - x) * (xDistance / 2));
            while (x != halfXDistance)
            {
                x += (int)Mathf.Sign(to.x - x);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // �̹� Ÿ���� �����ϸ� �ߴ�
                }
            }

            // y �������� to�� y ������ �̵�
            while (y != to.y)
            {
                y += (int)Mathf.Sign(to.y - y);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // �̹� Ÿ���� �����ϸ� �ߴ�
                }
            }

            // ���� x ���� �Ÿ� �̵�
            while (x != to.x)
            {
                x += (int)Mathf.Sign(to.x - x);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // �̹� Ÿ���� �����ϸ� �ߴ�
                }
            }
        }
        else
        {
            // y �������� ���� �̵�
            int halfYDistance = y + (int)Mathf.Sign(to.y - y) * (yDistance / 2);
            while (y != halfYDistance)
            {
                y += (int)Mathf.Sign(to.y - y);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // �̹� Ÿ���� �����ϸ� �ߴ�
                }
            }

            // x �������� to�� x ������ �̵�
            while (x != to.x)
            {
                x += (int)Mathf.Sign(to.x - x);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // �̹� Ÿ���� �����ϸ� �ߴ�
                }
            }

            // ���� y ���� �Ÿ� �̵�
            while (y != to.y)
            {
                y += (int)Mathf.Sign(to.y - y);
                Vector3Int position = new Vector3Int(x, y, 0);

                if (!MakeHallwayTile(position))
                {
                    return; // �̹� Ÿ���� �����ϸ� �ߴ�
                }
            }
        }

        generatedTiles.AddRange(CompareTiles);
    }

    bool MakeHallwayTile(Vector3Int position)
    {
        // �̹� �ش� ��ġ�� floorTile2�� �����ϴ��� Ȯ��
        if (floorTilemap.GetTile(position) == floorTile2)
        {
            return false; // �̹� Ÿ���� �����ϸ� �ߴ�
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
        return true; // ���ο� Ÿ���� �����ϸ� ��� ����
    }

    //���� �׵θ����� Ȯ���ϴ� �κ�
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
            // X �������� �̵� �� ����� ����
            //int xRange = GetRandomRange();
            int xRange = Random.Range(1, 3);
            x += (int)Mathf.Sign(to.x - from.x);
            Vector3Int currentPosition = new Vector3Int(x, y, 0);

            // If there is no floor tile at the current position, set water tile
            if (floorTilemap.GetTile(currentPosition) == null)
            {
                edgePoints.Remove(currentPosition);
                floorTilemap.SetTile(currentPosition, waterTile);

                // y �������� ��/�Ʒ��� �� Ÿ�� �߰�
                AddWaterTilesInRange(currentPosition, true, xRange);
            }
        }

        while (y != to.y)
        {
            // Y �������� �̵� �� ����� ����
            //int yRange = GetRandomRange();
            int yRange = Random.Range(1, 3);

            y += (int)Mathf.Sign(to.y - from.y);
            Vector3Int currentPosition = new Vector3Int(x, y, 0);

            // If there is no floor tile at the current position, set water tile
            if (floorTilemap.GetTile(currentPosition) == null)
            {
                edgePoints.Remove(currentPosition);
                floorTilemap.SetTile(currentPosition, waterTile);

                // x �������� ��/��� �� Ÿ�� �߰�
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

            // ��/�Ʒ� �Ǵ� ��/�쿡 �� Ÿ�� �߰�
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
        int range = Random.Range(1, 3); // 1���� 3 ������ ������ ��
        if (range == 2)
        {
            range = Random.Range(2, 4); // 2 �Ǵ� 3
        }
        else if (range == 3)
        {
            range = 3; // ������ 3
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

    // ����� ����� ��ġ�� ��ȯ�ϴ� �Լ�
    public Vector3Int GetStairsPosition()//public Vector3 GetStairsPosition()
    {
        return stairsPosition;
    }
    //���� ����
    void GenerateTraps()
    {
        foreach (Rect room in rooms)
        {
            // ���� �������� �����Ǿ����� Ȯ�� 
            if (rooms.Contains(room))
            {

                Vector3Int trapPosition;
                bool isCloseToStairs, isCloseToItems, isAtEdge;

                do
                {
                    trapPosition = GetRandomPositionInRoom(room);

                    // ���� ��ġ�� ����̳� ���ǿ� �ʹ� ������ ������ Ȯ��.
                    isCloseToStairs = stairsPosition != null && Vector3Int.Distance(trapPosition, stairsPosition) < minDistanceBetweenTrapsAndStairs;
                    isCloseToItems = itemPositions.Exists(itemPos => Vector3.Distance(trapPosition, itemPos) < minDistanceBetweenTrapsAndItems);
                    isAtEdge = edgePoints.Exists(edge => Vector3.Distance(trapPosition, edge) < 1.5f);

                } while (isCloseToStairs || isCloseToItems || isAtEdge);

                // ������ �������� �ʴ� ��ġ�� ã�� ��� ������ ��ġ
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

//�� �����͸� �����ϱ� ���� �� Ŭ����
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
