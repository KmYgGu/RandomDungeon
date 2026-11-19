using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목표 칸마다 하나씩 만들어 두고 <b>모두가 나눠 쓰는</b> 걸음 수 지도.
///
/// ── 왜 필요한가 ───────────────────────────────────────────────────
/// 추격은 "적이 몇 마리든 턴마다 탐색 한 번" 이다(GameManager.Chase). 그런데
/// 순찰은 그 규칙 밖에 있었다 — 적마다 지도를 하나씩 들고, 목표를 다시 고를
/// 때마다 층 전체를 새로 훑었다. 맵이 50×50 이면 지도 하나가 2,500칸이고
/// 적은 서른 마리를 넘길 수 있어서(EnemyBudget), 몬스터 하우스가 열린 직후처럼
/// 여럿이 한꺼번에 목표를 고르는 턴에는 그 탐색이 수십 번 겹쳤다.
///
/// 순찰 목표는 전부 <b>문</b>이다(PatrolPlan). 문은 층에 수십 개뿐이고 층이
/// 끝날 때까지 움직이지 않으므로, 같은 문을 향하는 적들은 같은 지도를 보면 된다.
/// 한 번 만든 지도는 그 층 동안 다시 만들지 않는다.
///
/// ── 지형이 바뀌면 ─────────────────────────────────────────────────
/// 지금은 층이 만들어진 뒤 벽이 바뀌지 않는다. 벽을 허무는 수단이 생기면
/// 그 자리에서 <see cref="Clear"/> 를 불러야 한다 — 안 부르면 적이 이미 뚫린
/// 벽을 돌아간다. 틀린 길을 가지는 않고 먼 길을 갈 뿐이라 눈에 잘 안 띈다.
///
/// 지도를 읽기만 하므로(TryGetDistance · StepsToward) 여럿이 함께 들어도 된다.
/// Unity 것은 좌표 타입뿐이라 Unity 없이 그대로 확인할 수 있다.
/// </summary>
public class RouteCache
{
    private readonly Dictionary<Vector3Int, ChaseMap> routes = new Dictionary<Vector3Int, ChaseMap>();

    /// <summary>지금 들고 있는 지도의 수. 나눠 쓰고 있는지 확인할 때 본다.</summary>
    public int Count => routes.Count;

    /// <summary>층이 바뀌었거나 지형이 달라졌을 때.</summary>
    public void Clear() => routes.Clear();

    /// <summary>
    /// 그 자리를 향하는 지도. 처음 묻는 목표면 그때 한 번 만든다.
    /// </summary>
    public ChaseMap To(IWalkableGrid grid, Vector3 targetWorld)
    {
        Vector3Int cell = GridUtil.CellOf(targetWorld);

        if (!routes.TryGetValue(cell, out ChaseMap route))
        {
            route = new ChaseMap();
            route.Rebuild(grid, targetWorld);
            routes[cell] = route;
        }

        return route;
    }
}
