using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 통로가 그릴 수 있는 모양들. <b>어떤 모양을 고를지는 여기서 정하지 않는다</b> —
/// 좌표를 받아 칸 목록을 만들어 줄 뿐이다.
///
/// 고르는 일(어디서 꺾을지 굴리고, 점수를 매겨 견주는 것)은 CorridorCarver 가 한다.
/// 그래서 이 모양들은 무작위도 타일맵도 모르고, Unity 없이 그대로 확인할 수 있다.
///
/// ── 세 가지 모양 ──────────────────────────────────────────────────
///   ㄱ자   - 한 번 꺾는다. 가장 짧지만 꺾이는 곳이 문에 붙어 답답하다
///   ㄹ자   - 두 번 꺾는다. 양쪽 문에서 모두 곧게 뻗는다
///
/// 옆으로 크게 벗어났다 돌아오는 '우회' 모양도 있었는데 지웠다. 통로가 길어지면서
/// 방 벽을 뚫고 폭 2칸이 되는 곳도 늘어, 층이 어지러워지고 진행이 막히기까지 했다
/// (CorridorCarver 주석). 꺾는 자리를 두 문 사이에서 옮기는 것만으로도 모양은 충분히 달라진다.
/// </summary>
public static class CorridorShapes
{
    /// <summary>한 번 꺾는다. corner 가 두 문 중 하나에 딱 붙는다.</summary>
    public static List<Vector3Int> L(Vector3Int from, Vector3Int to, bool horizontalFirst)
    {
        var path = new List<Vector3Int>();

        Vector3Int corner = horizontalFirst
            ? new Vector3Int(to.x, from.y, 0)
            : new Vector3Int(from.x, to.y, 0);

        AppendLine(path, from, corner);
        AppendLine(path, corner, to);
        return path;
    }

    /// <summary>
    /// 두 번 꺾는다. 주 축을 따라 <paramref name="bend"/> 까지 간 뒤 옆으로 건너가고,
    /// 다시 주 축으로 마저 간다. 양쪽 문에서 통로가 곧게 뻗어 나온다.
    ///
    /// bend 를 두 문의 한가운데로 주면 예전의 ㄹ자와 똑같다. 다른 값을 주면
    /// 꺾이는 자리만 옮겨 가므로, <b>곧게 뻗는다는 성질은 지키면서</b> 모양이 달라진다.
    /// </summary>
    public static List<Vector3Int> Z(Vector3Int from, Vector3Int to, bool primaryHorizontal, int bend)
    {
        var path = new List<Vector3Int>();

        if (primaryHorizontal)
        {
            var first = new Vector3Int(bend, from.y, 0);
            var second = new Vector3Int(bend, to.y, 0);

            AppendLine(path, from, first);
            AppendLine(path, first, second);
            AppendLine(path, second, to);
        }
        else
        {
            var first = new Vector3Int(from.x, bend, 0);
            var second = new Vector3Int(to.x, bend, 0);

            AppendLine(path, from, first);
            AppendLine(path, first, second);
            AppendLine(path, second, to);
        }

        return path;
    }

    /// <summary>
    /// 축이 하나만 다른 두 점 사이를 한 칸씩 채운다.
    ///
    /// 이어 붙일 때 앞 구간의 끝과 뒤 구간의 시작이 같은 칸이라 두 번 담기는데,
    /// 파는 쪽이 "이미 있으면 그대로 둔다" 로 처리하므로 문제가 되지 않는다.
    /// 대신 <b>칸이 이어져 있다</b>는 것은 여기서 보장한다 — 끊기면 갈 수 없는 길이 된다.
    /// </summary>
    public static void AppendLine(List<Vector3Int> path, Vector3Int from, Vector3Int to)
    {
        int stepX = from.x == to.x ? 0 : (to.x > from.x ? 1 : -1);
        int stepY = from.y == to.y ? 0 : (to.y > from.y ? 1 : -1);

        Vector3Int cursor = from;
        path.Add(cursor);

        while (cursor.x != to.x || cursor.y != to.y)
        {
            cursor = new Vector3Int(cursor.x + stepX, cursor.y + stepY, 0);
            path.Add(cursor);
        }
    }
}
