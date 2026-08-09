using System.Collections.Generic;
using UnityEngine;

// Section 당 방을 배치하고 Grid에 적용하는 클래스
public class RoomPlacer : IRoomPlacer
{
    // DungeonBlueprint 설정, Section 리스트, 그리고 grid를 받아 방 리스트를 반환
    public List<Room> GenerateRooms(DungeonBlueprint blueprint, List<Section> sections, TileType[,] grid)
    {
        List<Room> rooms = new List<Room>();

        // 섹션별로 방을 생성 (겹침 방지)
        foreach (var sec in sections)
        {
            if (sec.forceNoRoom)
                continue;

            // 섹션 가중치에 따라 방 생성 시도
            if (Random.value <= sec.roomWeight)
            {
                const int maxAttempts = 5;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Room candidate = CreateRandomRoomInSection(sec);
                    // 기존 방과 겹치지 않으면 수용
                    if (!rooms.Exists(r => r.area.Overlaps(candidate.area)))
                    {
                        rooms.Add(candidate);
                        CarveRoom(candidate, grid);
                        break;
                    }
                }
            }
        }

        // 최소 방 개수 보장
        int targetMin = blueprint.minRoomCount;
        foreach (var sec in sections)
        {
            if (rooms.Count >= targetMin)
                break;

            if (sec.forceNoRoom)
                continue;
            if (rooms.Exists(r => r.sectionIndex == sec.index))
                continue;

            // 최소 보장을 위해 겹침 없이 생성
            const int maxAttempts = 5;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Room candidate = CreateRandomRoomInSection(sec);
                if (!rooms.Exists(r => r.area.Overlaps(candidate.area)))
                {
                    rooms.Add(candidate);
                    CarveRoom(candidate, grid);
                    break;
                }
            }
        }

        // 최대 방 개수 제한 및 랜덤 제거
        if (rooms.Count > blueprint.maxRoomCount)
        {
            int toRemove = rooms.Count - blueprint.maxRoomCount;
            for (int i = 0; i < toRemove; i++)
            {
                int idx = Random.Range(0, rooms.Count);
                rooms.RemoveAt(idx);
            }
        }

        return rooms;
    }

    // 섹션 내부에서 랜덤 크기/위치의 방 생성
    private Room CreateRandomRoomInSection(Section sec)
    {
        int maxW = Mathf.Min(sec.maxRoomSize.x, sec.bounds.width - 2);
        int maxH = Mathf.Min(sec.maxRoomSize.y, sec.bounds.height - 2);
        int w = Random.Range(sec.minRoomSize.x, maxW + 1);
        int h = Random.Range(sec.minRoomSize.y, maxH + 1);
        int x = Random.Range(sec.bounds.xMin + 1, sec.bounds.xMax - w);
        int y = Random.Range(sec.bounds.yMin + 1, sec.bounds.yMax - h);
        RectInt area = new RectInt(x, y, w, h);
        return new Room(sec.index, area);
    }

    // grid에 방 영역을 Floor로 마킹
    private void CarveRoom(Room room, TileType[,] grid)
    {
        for (int x = room.area.xMin; x < room.area.xMax; x++)
            for (int y = room.area.yMin; y < room.area.yMax; y++)
                grid[x, y] = TileType.Floor;
    }
}


// 방의 출구(Joint)를 할당하는 클래스
public class ExitAssigner : IExitAssigner
{
    public void AssignExits(List<Room> rooms, List<Section> sections, List<Joint> joints)
    {
        joints.Clear();

        foreach (var room in rooms)
        {
            int exitCount = Random.Range(1, 5); // 최대 4방향
            List<int> directions = new List<int> { 0, 1, 2, 3 }; // 좌우하상

            // 방향을 섞은 뒤 앞에서 exitCount개만 사용
            Shuffle(directions);

            for (int i = 0; i < exitCount; i++)
            {
                Vector2Int pos = PickExitPosition(room, directions[i]);
                Joint joint = new Joint(room.sectionIndex, pos);
                room.joints.Add(joint);
                joints.Add(joint);
            }
        }
    }

    private Vector2Int PickExitPosition(Room room, int side)
    {
        int x = 0, y = 0;
        switch (side)
        {
            case 0: // 좌
                x = room.area.xMin;
                y = Random.Range(room.area.yMin, room.area.yMax);
                break;
            case 1: // 우
                x = room.area.xMax - 1;
                y = Random.Range(room.area.yMin, room.area.yMax);
                break;
            case 2: // 아래
                x = Random.Range(room.area.xMin, room.area.xMax);
                y = room.area.yMin;
                break;
            case 3: // 위
                x = Random.Range(room.area.xMin, room.area.xMax);
                y = room.area.yMax - 1;
                break;
        }
        return new Vector2Int(x, y);
    }

    // Fisher-Yates shuffle
    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

