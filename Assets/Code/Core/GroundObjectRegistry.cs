using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 놓여 있는 물건(아이템 / 계단 / 함정)의 위치 목록.
///
/// 예전에는 GameManager 의 public static List&lt;Vector3&gt; GroundSetPositions 라서
/// RandomDung, Player, GameManager 어디서든 Add / Remove 를 직접 호출했다.
/// 어떤 규칙으로 채워지고 비워지는지 한 화면에서 볼 수 없었다.
///
/// ── 칸으로 기억한다 ───────────────────────────────────────────────
/// 한때는 월드 좌표를 목록에 담고 "0.1 보다 가까우면 같은 자리" 로 견줬다.
/// 묻는 것은 언제나 <b>이 칸에 무언가 있는가</b> 인데 답을 float 거리로 내고
/// 있었고, 물을 때마다 목록을 통째로 훑었다. 미니맵과 상점이 이 물음을
/// 물건마다 되풀이하므로 바닥에 놓인 것이 늘수록 제곱으로 느려졌다.
///
/// 지금은 칸 좌표의 집합이다. 받는 쪽은 여전히 월드 좌표라 부르는 곳은
/// 그대로고, 안에서 한 번 칸으로 바꾼다(GridUtil.CellOf).
/// </summary>
public class GroundObjectRegistry
{
    private readonly HashSet<Vector3Int> cells = new HashSet<Vector3Int>();

    public int Count => cells.Count;

    public void Clear() => cells.Clear();

    /// <summary>같은 칸을 두 번 넣어도 하나로 남는다.</summary>
    public void Add(Vector3 position) => cells.Add(GridUtil.CellOf(position));

    public bool Contains(Vector3 position) => cells.Contains(GridUtil.CellOf(position));

    /// <summary>칸 좌표로 직접 묻는다. 이미 칸을 들고 있는 쪽이 쓴다.</summary>
    public bool Contains(Vector3Int cell) => cells.Contains(new Vector3Int(cell.x, cell.y, 0));

    public bool Remove(Vector3 position) => cells.Remove(GridUtil.CellOf(position));
}
