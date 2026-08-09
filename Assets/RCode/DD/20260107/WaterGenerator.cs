using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class WaterGenerator : MonoBehaviour
{

    /*public void Generate(
        TileType[,] grid,
        List<Room> rooms,
        List<Branch> branches
    )*/
    public void Generate(
    DungeonBlueprint blueprint,
    TileType[,] grid,
    List<Room> rooms,
    List<Branch> branches
)

    {
        if (grid == null || rooms == null || branches == null)
            return;

        //int width = grid.GetLength(0);
        //int height = grid.GetLength(1);


        List<Vector2Int> waterSeeds = FindWaterSeeds(grid, rooms, branches);


        foreach (var seed in waterSeeds)
        {
            CarveWaterFrom(seed, grid, rooms, branches);
        }
    }

    private List<Vector2Int> FindWaterSeeds(
        TileType[,] grid,
        List<Room> rooms,
        List<Branch> branches
    )
    {
        List<Vector2Int> result = new List<Vector2Int>();

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                // 이미 통로면 제외 ( 규칙 핵심)
                if (grid[x, y] == TileType.Floor)
                    continue;

                Vector2Int pos = new Vector2Int(x, y);

                // 방 내부면 제외
                if (IsInsideAnyRoom(pos, rooms))
                    continue;

                // 주변 통로나 기존 브랜치(path) 근처만 후보
                bool nearFloorOrBranch = false;
                for (int dx = -2; dx <= 2 && !nearFloorOrBranch; dx++)
                {
                    for (int dy = -2; dy <= 2 && !nearFloorOrBranch; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx <= 0 || ny <= 0 || nx >= width - 1 || ny >= height - 1)
                            continue;

                        // 주변이 통로이거나 수로를 막지 않는 기존 브랜치이면 true
                        if (grid[nx, ny] == TileType.Floor)
                            nearFloorOrBranch = true;
                    }
                }
                if (!nearFloorOrBranch)
                    continue;

                // 임시 규칙: 랜덤으로 일부만 선택
                if (Random.value < 0.002f)
                    result.Add(pos);
            }
        }

        return result;
    }

    private void CarveWaterFrom(
        Vector2Int start,
        TileType[,] grid,
        List<Room> rooms,
        List<Branch> branches
    )
    {
        Vector2Int current = start;

        for (int i = 0; i < 20; i++) // 최대 길이 제한
        {
            // 통로는 절대 덮지 않음
            //if (grid[current.x, current.y] == TileType.Floor)
            //break;
            // 통로를 덮는 순간 중단
            if (IsOnAnyBranch(current, branches))
                break;

            grid[current.x, current.y] = TileType.Water;

            // 랜덤 방향으로 흐르게           
            Vector2Int next = current + RandomDirection();

            if (!IsInsideGrid(next, grid))
                break;

            if (IsInsideAnyRoom(next, rooms))
                break;

            current = next;
        }
    }

    private bool IsOnAnyBranch(Vector2Int pos, List<Branch> branches)
    {
        foreach (var branch in branches)
        {
            if (branch.path.Contains(pos)) // ← 여기서 path를 사용
                return true;
        }
        return false;
    }


    private bool IsInsideAnyRoom(Vector2Int pos, List<Room> rooms)
    {
        foreach (var room in rooms)
        {
            if (room.area.Contains(pos))
                return true;
        }
        return false;
    }

    private bool IsInsideGrid(Vector2Int pos, TileType[,] grid)
    {
        return pos.x > 0 &&
               pos.y > 0 &&
               pos.x < grid.GetLength(0) - 1 &&
               pos.y < grid.GetLength(1) - 1;
    }

    private Vector2Int RandomDirection()
    {
        int r = Random.Range(0, 4);
        return r switch
        {
            0 => Vector2Int.up,
            1 => Vector2Int.down,
            2 => Vector2Int.left,
            _ => Vector2Int.right,
        };
    }
}
