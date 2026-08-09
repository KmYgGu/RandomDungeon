using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRoomPlacer
{
    // 룸설치
    List<Room> GenerateRooms(
        DungeonBlueprint blueprint,
        List<Section> sections,
        TileType[,] grid
    );
}

public interface IExitAssigner
{
    // 출구 설치
    void AssignExits(
        List<Room> rooms,
        List<Section> sections,
        List<Joint> joints
    );
}

public interface IConnectionPlanner
{
    // 방 연결
    List<SectionConnection> PlanConnections(
        DungeonBlueprint blueprint,
        List<Section> sections
    );
}

public interface IPathBuilder// 이미 거의 있음
{
    List<Vector3Int> BuildPath(
        Vector3Int from,
        Vector3Int to,
        TileType[,] grid,
        List<Room> rooms
    );
}