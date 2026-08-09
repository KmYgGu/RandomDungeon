using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어떤 방끼리 이을지 정한다.
///
/// 예전에는 "새 방을 이전 방 중 아무거나 하나에 잇는다" 였다.
/// 연결은 보장됐지만 언제나 순수한 트리라 순환 경로가 없었고,
/// 막다른 방에 몰리면 반드시 왔던 길로만 되돌아가야 했다.
///
/// 여기서는 먼저 최소 신장 트리로 모든 방을 확실히 잇고(연결성 보장),
/// 그 위에 가까운 방들 사이의 간선을 몇 개 더 얹어 루프를 만든다.
/// </summary>
public static class RoomGraph
{
    /// <summary>이을 방 두 개의 인덱스.</summary>
    public readonly struct Edge
    {
        public readonly int A;
        public readonly int B;

        public Edge(int a, int b)
        {
            A = a;
            B = b;
        }
    }

    /// <summary>층이 깊어질수록 우회로가 늘어난다.</summary>
    public static int ExtraLoopsForStage(int stage) => 1 + stage / 4;

    public static List<Edge> Plan(DungeonLayout layout, int extraLoops)
    {
        var result = new List<Edge>();
        int count = layout.Rooms.Count;
        if (count < 2) return result;

        // 가까운 방 쌍부터 살펴본다.
        var candidates = new List<(int distance, int a, int b)>();
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                Vector3Int ci = layout.Rooms[i].Center;
                Vector3Int cj = layout.Rooms[j].Center;
                int distance = Mathf.Abs(ci.x - cj.x) + Mathf.Abs(ci.y - cj.y);
                candidates.Add((distance, i, j));
            }
        }
        candidates.Sort((l, r) => l.distance.CompareTo(r.distance));

        var groups = new UnionFind(count);
        var leftover = new List<Edge>();

        foreach ((int _, int a, int b) in candidates)
        {
            if (groups.Union(a, b))
                result.Add(new Edge(a, b));   // 트리를 이루는 간선
            else
                leftover.Add(new Edge(a, b)); // 이미 이어진 쌍 → 루프 후보
        }

        // 가장 가까운 것부터 몇 개만 루프로 추가한다.
        for (int i = 0; i < extraLoops && i < leftover.Count; i++)
            result.Add(leftover[i]);

        return result;
    }

    /// <summary>서로 이어졌는지 빠르게 확인하기 위한 자료구조.</summary>
    private class UnionFind
    {
        private readonly int[] parent;

        public UnionFind(int size)
        {
            parent = new int[size];
            for (int i = 0; i < size; i++) parent[i] = i;
        }

        private int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        /// <summary>둘을 합쳤으면 true, 이미 같은 무리였으면 false.</summary>
        public bool Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra == rb) return false;

            parent[rb] = ra;
            return true;
        }
    }
}
