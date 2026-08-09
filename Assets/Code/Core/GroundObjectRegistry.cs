using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 놓여 있는 물건(아이템 / 계단 / 함정)의 위치 목록.
///
/// 예전에는 GameManager 의 public static List&lt;Vector3&gt; GroundSetPositions 라서
/// RandomDung, Player, GameManager 어디서든 Add / Remove 를 직접 호출했다.
/// 어떤 규칙으로 채워지고 비워지는지 한 화면에서 볼 수 없었다.
/// </summary>
public class GroundObjectRegistry
{
    // 같은 칸에 두 번 등록되지 않도록 비교 오차를 둔다.
    private const float SamePositionThreshold = 0.1f;

    private readonly List<Vector3> positions = new List<Vector3>();

    /// <summary>인스펙터 표시나 디버그용 읽기 전용 뷰.</summary>
    public IReadOnlyList<Vector3> Positions => positions;

    public int Count => positions.Count;

    public void Clear() => positions.Clear();

    public void Add(Vector3 position)
    {
        if (!Contains(position))
            positions.Add(position);
    }

    public bool Contains(Vector3 position)
    {
        foreach (Vector3 p in positions)
        {
            if (Vector3.Distance(p, position) < SamePositionThreshold)
                return true;
        }
        return false;
    }

    public bool Remove(Vector3 position)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            if (Vector3.Distance(positions[i], position) < SamePositionThreshold)
            {
                positions.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>디버그용 필드에 넣기 위한 복사본.</summary>
    public List<Vector3> ToList() => new List<Vector3>(positions);
}
