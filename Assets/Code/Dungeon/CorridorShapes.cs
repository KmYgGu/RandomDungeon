using System.Collections.Generic;
using UnityEngine;

/// <summary>ㄱ자·ㄹ자·계단형·우회형의 칸 경로. 선택과 장애물 검사는 CorridorRouting이 맡는다.</summary>
public static class CorridorShapes
{
    /// <summary>짧은 ㄱ자 여러 개를 이어 계단처럼 전진한다.</summary>
    public static List<Vector3Int> Stair(Vector3Int from, Vector3Int to, bool horizontalFirst, int sections)
    {
        var path = new List<Vector3Int>();
        var cursor = from;
        sections = Mathf.Clamp(sections, 2, 4);
        for (int i = 1; i <= sections; i++)
        {
            var next = new Vector3Int(from.x + (to.x - from.x) * i / sections,
                from.y + (to.y - from.y) * i / sections, 0);
            path.AddRange(L(cursor, next, horizontalFirst));
            cursor = next;
        }
        return path;
    }

    /// <summary>문에서 전진한 뒤 옆으로 돌아오는 네 번 굽은 우회로. offset의 부호가 우회 방향이다.</summary>
    public static List<Vector3Int> Detour(Vector3Int from, Vector3Int to, bool horizontal, int offset)
    {
        if (!horizontal)
        {
            var swapped = Detour(new Vector3Int(from.y, from.x, 0), new Vector3Int(to.y, to.x, 0), true, offset);
            for (int i = 0; i < swapped.Count; i++) swapped[i] = new Vector3Int(swapped[i].y, swapped[i].x, 0);
            return swapped;
        }
        int direction = to.x >= from.x ? 1 : -1;
        int stub = Mathf.Min(3, Mathf.Abs(to.x - from.x) / 3);
        int side = offset > 0 ? Mathf.Max(from.y, to.y) + offset : Mathf.Min(from.y, to.y) + offset;
        var points = new[] { from, new Vector3Int(from.x + direction * stub, from.y, 0),
            new Vector3Int(from.x + direction * stub, side, 0),
            new Vector3Int(to.x - direction * stub, side, 0),
            new Vector3Int(to.x - direction * stub, to.y, 0), to };
        var path = new List<Vector3Int>();
        for (int i = 1; i < points.Length; i++) AppendLine(path, points[i - 1], points[i]);
        return path;
    }

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
