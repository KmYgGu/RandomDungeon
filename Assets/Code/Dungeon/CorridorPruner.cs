using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 벽에 막혀 끝나는 짧은 곁가지 통로를 걷어 낸다.
///
/// ── 무엇을 걷어 내나 ──────────────────────────────────────────────
/// 던전은 이미 전부 이어져 있다(DungeonCheck 로 재 보면 '끊어진 통로' 도
/// '갈 수 없는 방' 도 나오지 않는다). 눈에 거슬리는 것은 <b>이어져는 있지만
/// 들어갔다 되돌아 나와야 하는</b> 1~3칸짜리 곁가지다. 통로가 가다 만 것처럼 보인다.
///
/// 이웃한 길이 하나뿐인 칸이 그것이다. 하나를 지우면 그 앞칸이 새로 끝이
/// 되므로 더 지울 것이 없을 때까지 되풀이한다.
///
/// ── 문도 걷어 낸다 ────────────────────────────────────────────────
/// 처음에는 문과 그 옆칸을 남겼다. 그랬더니 <b>문 밖의 1칸짜리 토막이 영영 남았다</b> —
/// 옆칸은 문 때문에 보호받고, 문은 그 옆칸 때문에 이웃이 둘이라 끝이 아니었다.
/// 서로가 서로를 붙잡고 있어서 어느 쪽도 지워지지 않는다.
///
/// 그래서 문도 같은 규칙으로 본다. 걱정할 일이 아닌 것이, <b>쓸모 있는 문은 애초에
/// 끝이 아니다</b> — 한쪽에 방 바닥이 있고 다른 쪽에 통로가 있어 이웃이 둘이다.
/// 이웃이 하나뿐인 문은 어디로도 통하지 않는 문이라, 지우면 벽이 말끔해진다.
///
/// 방이 떨어져 나갈 일도 없다. 그 방으로 가는 유일한 문이라면 그 너머에 길이
/// 이어져 있을 테고, 그러면 이웃이 둘이라 지워지지 않는다.
///
/// ── 끊어질 걱정이 없다 ────────────────────────────────────────────
/// 이웃이 하나뿐인 칸은 <b>어느 두 곳 사이의 길목이 될 수 없다.</b> 그 칸을 거쳐
/// 어딘가로 가려면 들어온 길로 도로 나와야 하기 때문이다. 그래서 지워도 남은
/// 곳들의 오가는 관계는 하나도 달라지지 않는다.
///
/// ── <b>반드시 벽을 칠한 뒤에 돌아야 한다</b> ───────────────────────
/// 벽을 칠하기 전에는 "걸어갈 수 있는가" 가 <b>빈 칸에도 true 를 준다</b>
/// (TileRules.KindOfTile 이 이름 없는 타일을 바닥으로 본다). 그때 재면 온 맵이
/// 뚫린 것으로 보여 아무것도 안 걸리거나, 반대로 엉뚱한 것이 걸린다.
/// 실제로 예전에 이 순서를 어겨서 통로를 통째로 지우고 던전을 끊어 먹었다.
///
/// 지운 자리는 빈 칸으로 남으므로, 부르는 쪽이 <b>다시 한 번 벽으로 메워야 한다</b>.
/// </summary>
public class CorridorPruner
{
    /// <summary>안전장치. 이보다 여러 겹으로 파고들지는 않는다.</summary>
    private const int MaxPasses = 32;

    private static readonly Vector3Int[] Cardinals =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
    };

    private readonly DungeonTilePainter painter;

    public CorridorPruner(DungeonTilePainter painter)
    {
        this.painter = painter;
    }

    /// <summary>걷어 낸 칸 수를 돌려준다.</summary>
    public int Prune(int mapWidth, int mapHeight)
    {
        int removed = 0;

        for (int pass = 0; pass < MaxPasses; pass++)
        {
            List<Vector3Int> deadEnds = FindDeadEnds(mapWidth, mapHeight);
            if (deadEnds.Count == 0) break;

            foreach (Vector3Int cell in deadEnds)
            {
                painter.Erase(cell);
                removed++;
            }
        }

        return removed;
    }

    private List<Vector3Int> FindDeadEnds(int mapWidth, int mapHeight)
    {
        var found = new List<Vector3Int>();

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                var cell = new Vector3Int(x, y, 0);

                // 통로와 문 둘 다 걷어 낼 수 있다(아래 주석 참고).
                if (!painter.IsPassage(cell)) continue;

                if (WalkableNeighbours(cell) <= 1) found.Add(cell);
            }
        }

        return found;
    }

    private int WalkableNeighbours(Vector3Int cell)
    {
        int count = 0;
        foreach (Vector3Int direction in Cardinals)
        {
            if (IsOpen(cell + direction)) count++;
        }
        return count;
    }

    /// <summary>
    /// 실제로 걸어 다닐 수 있는 칸인가.
    ///
    /// <b>painter.IsWalkable 만으로는 안 된다.</b> 그쪽은 아무것도 없는 칸에도
    /// '갈 수 있다' 고 답한다(TileRules.KindOfTile 이 이름 없는 타일을 바닥으로 본다).
    ///
    /// 여기서는 그것이 특히 해롭다. 막다른 끝을 지우면 그 자리가 <b>빈 칸</b>이 되는데,
    /// 빈 칸을 길로 세면 그 앞칸이 여전히 이웃 둘로 보여서 <b>연쇄가 한 겹에서 멈춘다.</b>
    /// 실제로 그래서 층마다 딱 한 칸씩 남았다. 지운 자리를 벽으로 메우는 것은
    /// 이 걷어 내기가 다 끝난 뒤이므로(RandomDung 의 5-3), 여기서 스스로 가려야 한다.
    /// </summary>
    private bool IsOpen(Vector3Int cell)
        => !painter.IsEmpty(cell) && painter.IsWalkable(cell);

}
